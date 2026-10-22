# 存档系统（SaveSystem / ProgressService / PlayerProgress）

> 最后更新：2026-10-22 · 状态：**设计定稿，尚未实现**
>
> **本文是存档系统的权威来源。** 策划层面的"存什么"见 [`PHASE1-DESIGN.md`](PHASE1-DESIGN.md) §7；
> 本文负责"怎么存、谁存、什么时候存"。
> 任务系统怎么用这份存档见 [`QUEST-SYSTEM.md`](QUEST-SYSTEM.md)。

---

## 1. 现状与定位

- 工程目前**完全没有存档层**（`ProgressService` / `PlayerProgress` / `SaveSystem` 三个类都不存在）。
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
| 格式 | 外层 `SaveFile { int version; PlayerProgress progress; }`，`prettyPrint = true`（手改便于排查） |
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
| 文件不存在 | 默认值，不写盘 |
| JSON 损坏 / 截断 | 备份为 `save.bad.json`，默认值，打 error |
| `progress == null` | 同上（`JsonUtility` 遇到空对象会给 null） |
| `version` 比当前新 | 拒绝加载，默认值，打 error（**不要**尝试解析） |
| `List` 字段为 null | 加载后统一 `?? new List<T>()` 补一遍 |
| 磁盘只读 / 无权限 | 捕获异常、打 error，**不影响游戏继续跑**（内存里照样有进度） |

---

## 7. 编辑器工具

`Assets/_Game/Editor/SaveMenu.cs`（菜单 `Tools/存档/…`）：

- **打开存档目录** — `EditorUtility.RevealInFinder`
- **打印存档路径与大小** — 方便确认到底读的哪个文件
- **删除存档** — 带二次确认（`EditorUtility.DisplayDialog`）

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

---

## 9. 已知坑

1. **`JsonUtility` 不支持 `Dictionary`** —— 会静默存成空对象/空数组，不报错。只能用 `List<T>` + 手动查。
2. **`JsonUtility` 不支持多态** —— 基类引用存子类字段会被截断。存档里不要出现接口/基类字段。
3. **字段改名 = 丢数据** —— Unity 按字段名恢复。改名必须配 `version` 升级 + `Migrate`。
4. **枚举重排 = 状态错乱**（见 §3 的显式赋值）。
5. **`persistentDataPath` 随产品名变化** —— 改 ProductName 会"丢档"。
6. **别在 `Update` 里存盘** —— IO 开销。
7. **别用 `Time.deltaTime` 做防抖** —— 暂停时 `timeScale = 0`，永远不落盘。

---

## 10. 与任务系统的接口

任务系统只依赖三件事，**不直接碰文件**：

1. `ProgressService.Instance.Progress.quests` —— 读/写 `QuestSave` 列表
2. 写完后 `ProgressService.Instance.MarkDirty()`
3. 订阅 `ProgressService.Instance.Changed` 刷新 UI（可选，任务系统自己也有 `QuestsChanged`）

详见 [`QUEST-SYSTEM.md`](QUEST-SYSTEM.md) §6。
