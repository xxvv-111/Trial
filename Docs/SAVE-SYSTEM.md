# 存档系统（SaveSystem / ProgressService / PlayerProgress）

> 最后更新：2026-10-22 · 状态：**运行时层已落地**（自测 30/30 通过），业务侧尚未接入
>
> **本文是存档系统的权威来源。** 策划层面的"存什么"见 [`PHASE1-DESIGN.md`](PHASE1-DESIGN.md) §7；
> 本文负责"怎么存、谁存、什么时候存"。
> 任务系统怎么用这份存档见 [`QUEST-SYSTEM.md`](QUEST-SYSTEM.md)。

---

## 1. 现状与定位

- 存档层**已实现**（2026-10-22）。运行时 6 个文件在 `Assets/_Game/Scripts/Save/`：
  `SaveData.cs` / `IFileStore.cs` / `DiskFileStore.cs` / `MemoryFileStore.cs` / `SaveSystem.cs` / `ProgressService.cs`；
  编辑器 2 个：`Assets/_Game/Editor/SaveMenu.cs`、`Assets/_Game/Editor/SaveSelfTest.cs`。
- 业务侧**还没接入**：任务 / 装备系统尚未写，目前只有 `ProgressService` 这一个入口可用。
- **第一个消费者是任务系统** —— 任务进度必须跨场景、跨会话保留，否则"打到 2/3 关掉游戏就白打"。
- 所以存档系统要**先于**任务系统落地，但不必一次做完所有业务。

### 1.1 本阶段做（"基本完善"，够用且不返工）

| # | 内容 |
|---|---|
| 1 | `PlayerProgress`：**全部**持久字段（魔晶 / 装备 / 法术 / 锻炼 / 任务 / 统计），后续系统只填自己的部分 |
| 2 | `SaveSystem`：JSON 存读盘 + `version` + 全程容错 + 坏档备份 |
| 3 | `ProgressService`：跨场景唯一读写入口（`DontDestroyOnLoad` 单例，自动创建） |
| 4 | 落盘策略：**标脏 + 0.5 s 防抖落盘**，退出时兜底 flush |
| 5 | 编辑器菜单：打开存档目录 / 打印路径 / 删除存档（调试用） |

### 1.2 本阶段**不做**（明确挂起）

- 多存档槽 / 选档界面 / 云存档
- 自动存档与手动存档两种模式的切换
- 具体的数据迁移实现（只留 `version` 钩子和 `Migrate` 空壳）
- 装备 / 法术 / 锻炼 / 统计的**业务逻辑**（本阶段只保证这些字段能被存下来）

---

## 2. 存储位置与格式

| 项 | 值 |
|---|---|
| 路径 | `Application.persistentDataPath/save.json` |
| Windows 实际位置 | `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\save.json` |
| 序列化 | `JsonUtility`（⚠️ **不用** Newtonsoft，工程没有这个依赖） |
| 格式 | 外层 `SaveFile { int version; SaveHeader header; PlayerProgress progress; }`，`prettyPrint = true`（手改便于排查） |
| 校验 | 正文后追加一行 `#crc32:XXXXXXXX`（CRC-32/IEEE）。该行**缺失视为手改档放行**（打 warning），存在但不匹配才判坏档 |
| 备份 | 覆盖写时旧档自动变 `save.json.bak`；坏档改名 `save.bad.json`（不覆盖、不删除，留人工抢救余地） |
| 编码 | UTF-8 |

⚠️ **改 `CompanyName` / `ProductName` 等于换存档位置** —— 编辑器里表现为"存档突然没了"，别第一反应怀疑代码。

---

## 3. 数据结构

全部是**纯数据类**（`[Serializable]`，无逻辑），可以被 `JsonUtility` 直接序列化。

```csharp
[Serializable]
public class PlayerProgress
{
    public int magicCrystal;                                     // 魔晶
    public List<EquipmentSave> equipmentLevels = new List<EquipmentSave>();
    public List<string> learnedSpellIds = new List<string>();
    public List<TrainingSave> trainingCounts = new List<TrainingSave>();
    public List<QuestSave> quests = new List<QuestSave>();
    public StatsSave stats = new StatsSave();
}

[Serializable] public class QuestSave   { public string questId; public QuestStatus status; public int progress; }
[Serializable] public class EquipmentSave { public string equipmentId; public int level; }
[Serializable] public class TrainingSave  { public string statId; public int count; }
[Serializable] public class StatsSave     { public int clears; public int deaths; public float bestTime; }
```

任务状态枚举（**显式赋值，禁止重排**）：

```csharp
public enum QuestStatus
{
    Locked        = 0,   // 未解锁（前置未完成）
    Available     = 1,   // 可接取
    Active        = 2,   // 进行中
    ReadyToTurnIn = 3,   // 进度已满，回酒馆可交付
    Completed     = 4,   // 已交付
}
```

> ⚠️ **为什么枚举要显式赋值**：`JsonUtility` 存的是**整数**。哪天有人往中间插一个枚举项，
> 所有旧存档的状态就会整体错位一格 —— 而且不报错。要么显式赋值，要么存 `int` 自己映射。

---

## 4. 读写流程

### 4.1 启动（Load）

`ProgressService` 在**任何场景的 `Awake` 之前**创建并完成读档（见 §5 的自动创建方式）。

1. 文件不存在 → **用默认值**（新玩家），**不立刻写盘**（等第一次真正变化再写）
2. 读取成功、`version == Current` → 直接用
3. `version < Current` → 调 `Migrate()`（本阶段是空壳，直接按新格式读）
4. `version > Current` → **拒绝加载**，打 error，用默认值（旧版本客户端读到新档，宁可重来也别写坏）
5. 解析异常 / 内容为 null → 把坏文件**改名备份**为 `save.bad.json`，用默认值，打 error

### 4.2 存盘（Save）

| 触发 | 行为 |
|---|---|
| 业务数据变化（任务进度、魔晶增减……） | 调 `ProgressService.MarkDirty()` → **标脏** |
| 标脏后 | `Update` 里用 **0.5 s（unscaled）** 防抖，到期写一次盘 |
| 关键节点（离开酒馆 / 通关 / 死亡结算） | 调 `SaveNow()` **立刻写**，不等防抖 |
| `OnApplicationQuit` / `OnDisable` | 兜底 `SaveNow()`（防"最后一秒的改动没落盘"） |

#### 4.2.1 落盘方式：原子写（2026-10-22 补）

`DiskFileStore.WriteAllTextAtomic` 的四个步骤**顺序不可调换**：

1. 写 `save.json.tmp`（`UTF8Encoding(false)`，**不写 BOM**）
2. `fs.Flush(true)` —— 刷到物理盘，不是只进 OS 缓存
3. 主档存在 → `File.Replace(tmp, save.json, save.json.bak)`（旧档自动变 `.bak`）；不存在 → `File.Move`
4. `finally` 清残留 `.tmp`（成功时 tmp 已被消耗，是空操作）

直接 `File.WriteAllText` 在断电 / 崩溃 / 磁盘满时会留下半截文件，下次启动就是坏档。
原子写保证**任何时刻磁盘上都至少有一份完整档**：断电只会留下 `.tmp` 残片，主档完好。

⚠️ **不要每帧 Save**：写盘是 IO，几十 KB 的文件每帧写会造成明显卡顿。
⚠️ **防抖要用 `unscaledDeltaTime`**：暂停/顿帧时 `timeScale = 0`，用缩放时间会永远等不到落盘。

### 4.3 为什么不是"只在关键节点存"

`PHASE1-DESIGN.md` §7 的建议 4 写的是"只在关键节点存盘"，但**任务进度是在城堡里推进的** ——
玩家打到一半直接退出游戏，进度就没了。而同一份文档 §4.3.4 又要求"任务进度必须进存档"。

**本文以"标脏 + 防抖落盘"为准**，关键节点存盘降级为"额外保险"。

---

## 5. ProgressService 单例

```csharp
public sealed class ProgressService : MonoBehaviour
{
    public static ProgressService Instance { get; private set; }
    public PlayerProgress Progress { get; private set; }

    public event Action Changed;        // 数据变了（UI 可以订阅刷新）

    public void MarkDirty();            // 标脏，0.5 s 后落盘
    public void SaveNow();              // 立刻落盘
    public void ResetToDefault();       // 调试用：清空进度（不删文件）
}
```

### 5.1 自动创建（关键设计）

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Bootstrap()
{
    if (Instance != null) return;
    var go = new GameObject("[ProgressService]");
    DontDestroyOnLoad(go);
    go.AddComponent<ProgressService>();
}
```

**为什么不用"在 MainMenu 场景里摆一个"**：开发时经常**直接打开 Castle / Tavern 按 Play**，
那种启动路径根本不会经过 MainMenu ⇒ 服务不存在 ⇒ 任务系统全线空引用。
`BeforeSceneLoad` 保证**任何启动方式**都有，且只创建一次。

⚠️ 这是本工程**第一个 `DontDestroyOnLoad` 单例**（`InputService` 是**每场景一个**，见其类注释）。
两者模式不同，别照抄。

### 5.2 单一出口原则

- **只有 `ProgressService` 能写 `PlayerProgress` 并落盘。**
- `QuestService` / `EquipmentService` / `TrainingService` 全部是**纯逻辑**：改数据走
  `ProgressService.Instance.Progress.xxx`，改完调 `MarkDirty()`，**自己绝不碰文件**。
- UI 只**读**，不写。

> 违反这条的后果：两个服务各自 `File.WriteAllText`，谁后写谁覆盖，症状是"偶尔丢进度"，极难查。

---

## 6. 容错清单

| 情况 | 处理 |
|---|---|
| 文件不存在，但 `.bak` 在 | 从 `.bak` 恢复，打 warning |
| 文件不存在，`.bak` 也没有 | 默认值，不写盘 |
| JSON 损坏 / 截断 / CRC 不匹配 | 主档改名 `save.bad.json` → 试 `.bak` → 都失败则默认值 + error |
| `progress == null` | ⚠️ **实测不可达**：`JsonUtility` 不会把嵌套对象反序列化成 null，`{"progress":null}` 只是空进度（自测 C4/C5 已锁定该行为） |
| `version` 比当前新 | 拒绝加载，默认值，打 error，**且不备份、不覆盖**（那是未来的档，覆盖就真没了） |
| `version` ≤ 0 / 缺字段 | 按 v1 处理并走迁移链 |
| `List` 为 null / 元素为 null / 重复 ID | `SaveSystem.Normalize()` 统一补空、剔除、去重 |
| 枚举越界 | `Normalize()` 归零为 `Locked` 并打 warning |
| 文件头带 BOM | 读取前 `TrimStart('\uFEFF')`，否则 `JsonUtility` 抛 `ArgumentException` |
| 磁盘只读 / 无权限 | 捕获异常、`Save()` 返回 false、打 error，**不影响游戏继续跑**（内存里照样有进度） |

---

## 7. 编辑器工具

`Assets/_Game/Editor/SaveMenu.cs`（菜单 `Tools/存档/…`，已实现）：

- **打开存档目录** — `EditorUtility.RevealInFinder`
- **打印存档路径与大小** — 主档 / `.bak` / `.bad.json` / `.tmp` 逐个报路径、字节数、最后写入时间
- **写入测试数据** — 造一份魔晶 999、带 1 个任务的档（Play 模式中会警告会被覆盖）
- **删除存档（含 .bak/.bad）** — 带二次确认（`EditorUtility.DisplayDialog`）

### 7.1 自动化自测（30 项，毫秒级）

`Assets/_Game/Editor/SaveSelfTest.cs`，菜单 `Tools/存档/自测（30 项）`。
全部走 `MemoryFileStore` 与 `%TEMP%` 目录，**不碰玩家真实存档**。实测 86 ms 跑完。

| 组 | 条数 | 覆盖 |
|---|---|---|
| A 往返 | 6 | 满数据 / 空 List / null List / 中文 / 2000 字符 / int 极值 |
| B 默认值 | 3 | NotFound、默认进度、不凭空写盘 |
| C 坏档 | 7 | 空文件 / CRC 篡改 / 截断 / 空白 / BOM 合法档 / `progress:null` / `{}` |
| D 版本 | 4 | 999 拒绝、0 升级、缺 version、负数钳位 |
| E 结构 | 5 | 缺字段、多余字段、枚举 99、null 元素、重复 ID |
| F 原子写 | 5 | 生成文件、`.bak` 内容、无 `.tmp` 残留、中断后主档不变、失败返回 false |

---

## 8. 验收清单

- [ ] 首次运行（无 `save.json`）不报错，进度为默认值，且**不会**凭空生成文件
- [ ] 改 `magicCrystal` → 0.5 s 内文件出现，内容正确
- [ ] **退出 Play 再进** → 值还在（这是存档系统的核心验收）
- [ ] 手动把 `save.json` 改成乱码 → 启动不崩，生成 `save.bad.json`，进度回默认
- [ ] 把 `version` 改成 `999` → 拒绝加载并打 error，进度回默认
- [ ] 从 **Castle** 直接 Play（不经过 MainMenu）→ `ProgressService.Instance` 存在且唯一
- [ ] 从 Castle 走到 Tavern（切场景）→ `Instance` 是**同一个对象**，进度不丢
- [ ] 暂停（`timeScale = 0`）期间改数据 → 仍能落盘（验证用的是 unscaled 防抖）

> 前四条里的坏档 / 版本号场景已由 `Tools/存档/自测（30 项）` 自动化覆盖，
> 剩下的是**只能在 Play 模式或打包后验证**的部分（生命周期、单例唯一性、强杀进程）。

---

## 9. 已知坑

1. **`JsonUtility` 不支持 `Dictionary`** —— 会静默存成空对象/空数组，不报错。只能用 `List<T>` + 手动查。
2. **`JsonUtility` 不支持多态** —— 基类引用存子类字段会被截断。存档里不要出现接口/基类字段。
3. **字段改名 = 丢数据** —— Unity 按字段名恢复。改名必须配 `version` 升级 + `Migrate`。
4. **枚举重排 = 状态错乱**（见 §3 的显式赋值）。
5. **`persistentDataPath` 随产品名变化** —— 改 ProductName 会"丢档"。
6. **别在 `Update` 里存盘** —— IO 开销。
7. **别用 `Time.deltaTime` 做防抖** —— 暂停时 `timeScale = 0`，永远不落盘。
8. **`JsonUtility` 不会把嵌套对象反序列化成 null** —— `{"progress":null}` 拿到的是默认实例，
   所以「`progress == null` 就是坏档」这个判断实际不会触发（2026-10-22 实测）。
9. **带 BOM 的文本会让 `JsonUtility.FromJson` 抛 `ArgumentException`** —— 自己写文件时用 `UTF8Encoding(false)`，读的时候再兜一次 `TrimStart('\uFEFF')`。
10. **`File.Replace` 要求源和目标在同一卷** —— `.tmp` 必须和目标文件同目录，不能写到 `%TEMP%` 再挪过去。

---

## 10. 与任务系统的接口

任务系统只依赖三件事，**不直接碰文件**：

1. `ProgressService.Instance.Progress.quests` —— 读/写 `QuestSave` 列表
2. 写完后 `ProgressService.Instance.MarkDirty()`
3. 订阅 `ProgressService.Instance.Changed` 刷新 UI（可选，任务系统自己也有 `QuestsChanged`）

详见 [`QUEST-SYSTEM.md`](QUEST-SYSTEM.md) §6。
