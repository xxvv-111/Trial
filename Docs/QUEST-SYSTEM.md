# 任务系统（QuestService / QuestConfig / QuestPanel）

> 最后更新：2026-10-22 · 状态：**设计定稿，尚未实现**
>
> **本文是任务系统的权威来源。** 策划层面的意图见 [`PHASE1-DESIGN.md`](PHASE1-DESIGN.md) §4.3.4；
> 本文负责"数据怎么组织、谁推进、UI 怎么接"，并**修订了原规划里的 3 处矛盾和 4 处缺口**（§3）。
> 存档格式见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md)；场景映射见 [`SceneCatalog`](../Assets/_Game/Scripts/Data/SceneCatalog.cs)。

---

## 1. 现状盘点（动手前必须知道）

| 已有 | 状态 |
|---|---|
| `QuestPanel : UIWindow` | ✅ 面板结构完成，但**内容是写死的占位**（`_placeholderQuests`） |
| `Panel_QuestPanel`（Tavern） | ✅ v4 风格分栏：左列 2 个 `Row_Quest` 槽 + 右列详情 + 关闭按钮 |
| 告示板 | ✅ `Interactable.onInteract → QuestPanel.Open`（**只能看，不能接**） |
| `SceneGate.Go()` | ✅ 切场景；2026-10-22 起支持 `SetTarget()` 运行时改目标 |
| `SceneCatalog.asset` | ✅ 场景 → 显示名/副标题/是否安全区（**任务地点用它当 key**） |
| `GameEvents` | ⚠️ **只有 `PlayerDied` / `BossDied`**，没有 `EnemyKilled` / `SceneEntered` |
| `EnemyAIConfig` | ⚠️ **只有 `displayName`，没有稳定 id** ⇒ `KillCount{ enemyId }` 目前无法实现 |
| 存档层 | ❌ **不存在**（见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md)，任务系统是它的第一个消费者） |
| `QuestService` / `QuestConfig` | ❌ 不存在 |

**结论：不能直接开写 `QuestService`，得先补三块地基**（敌人 id + 事件、存档骨架、任务配置）。

---

## 2. 目标与范围

### 2.1 本阶段做

- 目标类型 **3 种**：`KillCount{ enemyId }` / `KillBoss` / `ReachScene{ sceneName }`
- 完整闭环：**酒馆接取 → 前往目标场景 → 进度推进 → 回酒馆交付 → 发奖励**
- 任务进度**进存档**，退出游戏再进不丢
- 接取任务后**酒馆大门的目标场景随任务地点变化**
- 任务面板显示**真实数据**（列表 + 详情 + 按钮状态）

### 2.2 本阶段不做（明确挂起）

- 任务前置链 / 任务图（A 完成才能接 B）
- 对话与叙事（`PHASE1-DESIGN` §1.2 已排除）
- 限时任务 / 失败条件 / 放弃任务
- 动态列表（见 §3 缺口 3 的取舍）

---

## 3. 对既有规划的修订 ⭐ 本节最重要

原规划（`PHASE1-DESIGN.md` §4.3.4 / §7 / §9）方向正确 —— 四层分离、进度靠事件推进、
目标用枚举+参数、存档用扁平 DTO。但**实现前必须修掉下面 3 处矛盾**，否则一定返工。

### 3.1 矛盾 1：存档时机

- §7 建议 4：「只在关键节点存盘（离开酒馆 / 通关 / 死亡结算）」
- §4.3.4：「任务进度**必须**进存档，否则关掉游戏就没了」

但任务进度是在**城堡里**推进的 —— 按 §7 的做法，玩家在城堡打一半退出，进度全丢。

**修订**：改为"**标脏 + 0.5 s 防抖落盘**"，关键节点存盘降级为额外保险。细则见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) §4.3。

### 3.2 矛盾 2：两个单例、两个存盘出口

§10 说 `ProgressService` 是「`PlayerProgress` **唯一**读写入口 + 存读盘」，
而 §9 / §4.3.4 的 `QuestService` 又自己持有 `QuestProgress`。两个单例都碰存档 ⇒ 谁后写谁覆盖，症状是"偶尔丢进度"。

**修订**：职责硬切 ——
- `ProgressService`：**唯一**持久化出口，持有 `PlayerProgress`（含 `quests`），负责落盘
- `QuestService`：**纯逻辑**，订阅事件、推进进度、判定可交付，**绝不碰文件**

### 3.3 矛盾 3：目标类型漏了"到达场景"

§4.3.4 只列了「击杀 N 只」「击败 Boss」两种。但：

- `QuestPanel` 的占位数据第一条就是 **「前往城堡」**（到达场景型）
- 2026-10-22 的新需求「接取任务后大门目标随任务地点变」本质上也是它

**修订**：补第 3 种 `ReachScene{ sceneName }`。实现极便宜 —— 监听场景加载完成即可。

### 3.4 缺口 1：敌人没有 id

`KillCount{ enemyId }` 需要一个**稳定的敌人标识**，而现在 `EnemyAIConfig` 只有 `displayName`（"拳击手"），
`EnemyHealth` 也不对外暴露 config。

**需要先补**：
1. `EnemyAIConfig.enemyId`（唯一字符串，例 `"swordsman"` / `"caster"` / `"boss_crichton"`）
2. `EnemyHealth.ConfigId`（只读属性，转发 `_config.enemyId`）
3. `GameEvents.EnemyKilled(EnemyHealth, string enemyId, int exp)`

⚠️ **别用 `displayName` 当 id**：那是给人看的，改名就断任务。

### 3.5 缺口 2：`QuestUpdated(questId, cur, need)` 驱动不了现有 UI

现在的面板是「左列任务列表 + 右列详情」，需要的是**列表快照**；而这个事件只给单条任务的进度数字，
而且状态变化（接取 / 可交付 / 已完成）也需要刷新。

**修订**：事件退化成**脏通知** `QuestsChanged()`，UI 收到后主动向 `QuestService` 拉
`IReadOnlyList<QuestState>` 重建列表。**不要**为每种变化定义一种事件。

### 3.6 缺口 3：UI 只有 2 个任务槽

实测 `QuestPanel._entryLabels` 就是 **2 个**元素（`Row_Quest_1` / `Row_Quest_2`）。

**取舍**（待拍板，见 §8）：
- **方案 A（推荐 V1）**：固定 2 槽 + 限制"同时最多接 2 条"，零架构成本
- **方案 B**：做 `Row_Quest.prefab` + 动态实例化 + 对象池，一步到位但要多写一层列表管理

### 3.7 缺口 4：「接取 / 交付」这个动作没有设计

告示板的 `onInteract` 现在只挂了 `QuestPanel.Open` —— 能看不能接。

**修订**：面板内加一个主按钮，随状态切换：

| 状态 | 按钮文案 | 点击行为 |
|---|---|---|
| `Available` | 接取 | 转 `Active`，同时 `gate.SetTarget(quest.targetScene)` |
| `Active` | （置灰）进行中 | — |
| `ReadyToTurnIn` | 交付 | 发奖励 → 转 `Completed` |
| `Completed` | （置灰）已完成 | — |

**"可交付"的判定点**：建议进度打满**只转 `ReadyToTurnIn`，不自动完成** —— 必须回酒馆点交付，
否则"接取 → 出去打 → 回酒馆交付"这条循环就不存在了。

---

## 4. 数据模型

### 4.1 配置（SO）：`QuestConfig`

```csharp
[CreateAssetMenu(fileName = "QuestConfig", menuName = "Game/QuestConfig")]
public class QuestConfig : ScriptableObject
{
    public string questId;          // 唯一，⭐ 存档里存的就是它，定下别改
    public string title;            // 列表与详情标题
    [TextArea] public string description;

    public QuestObjectiveType objectiveType;
    public string targetId;         // KillCount → enemyId；ReachScene → sceneName；KillBoss → 留空
    public int    needCount = 1;    // KillCount 用；其余填 1

    public string targetScene;      // 任务地点（对应 SceneCatalog 的 sceneName），大门会切到这里
    public List<QuestReward> rewards;   // 魔晶 / 装备等级 / 解锁法术
}

public enum QuestObjectiveType { KillCount = 0, KillBoss = 1, ReachScene = 2 }
```

> **枚举 + 参数**，不要为每种任务写一个类 —— 原规划这条是对的，保留。
> 加新目标类型 = 加枚举项 + 在 `QuestService` 加一个 `case`。

### 4.2 运行时：`QuestState`

```csharp
public class QuestState
{
    public string questId;
    public QuestStatus status;   // Locked / Available / Active / ReadyToTurnIn / Completed
    public int progress;
    public QuestConfig Config;   // 运行时按 questId 从任务表查出来的引用（不存盘）
}
```

### 4.3 存档：`QuestSave`

见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) §3 —— 只存 `questId / status / progress`，
**不存**标题描述等静态内容（那些从 `QuestConfig` 读，改文案不用改存档）。

---

## 5. 服务层

```
QuestConfig (SO)  ──┐
                    ├──> QuestService（纯逻辑，DontDestroyOnLoad）
GameEvents ─────────┘         │
  EnemyKilled / BossDied      │ 写
  / SceneEntered              ▼
                        ProgressService（唯一存盘出口）
                              │
                              ▼
                        PlayerProgress.quests  →  save.json
```

- `QuestService` 持有**任务表**（`QuestConfig[]`，可以在 Inspector 里配，或一个 `QuestCatalog` SO）
- 启动时把 `ProgressService.Progress.quests` 与任务表对齐：表里有、存档里没有的补成 `Available`
- 进度变化 → 写 `QuestSave` → `ProgressService.MarkDirty()` → 广播 `QuestsChanged`

### 5.1 谁订阅什么

| 事件 | 谁发 | `QuestService` 的反应 |
|---|---|---|
| `GameEvents.EnemyKilled(EnemyHealth, string enemyId, int exp)` | `EnemyHealth.Die` | `KillCount` 且 `targetId == enemyId` → progress++ |
| `GameEvents.BossDied` | `BossController.EnterDeath` | `KillBoss` → progress = need |
| `GameEvents.SceneEntered(string sceneName)` | 场景加载后 | `ReachScene` 且 `targetId == sceneName` → progress = need |

⚠️ **`QuestService` 必须只订阅一次**。它是 `DontDestroyOnLoad` 单例 ⇒ 切场景不会重建 ⇒
在 `Awake` 里订阅、`OnDestroy` 里退订即可。**不要**写在 `OnEnable`（场景切换会重复叠加，
症状是"杀一只怪进度 +2"，这正是 `EnemyHealth.Died` 的 T7 问题）。

---

## 6. 事件扩展

在现有 `Core/GameEvents.cs`（只有 `PlayerDied` / `BossDied`）上新增：

```csharp
public static event Action<EnemyHealth, string, int> EnemyKilled;   // 敌人, enemyId, 经验
public static event Action<string> SceneEntered;                    // sceneName
public static event Action QuestsChanged;                           // 任务数据变了（脏通知）
```

⚠️ **静态事件必须成对订阅/退订**（`OnEnable` 订 / `OnDisable` 退，或单例的 `Awake`/`OnDestroy`）。
历史坑：T7。

---

## 7. 流程与联动

### 7.1 主循环

```
酒馆 · 告示板（Interactable → QuestPanel.Open）
   └─ 面板列表里选一条 Available → 点「接取」
        ├─ QuestStatus = Active
        ├─ progress = 0 → MarkDirty
        └─ ⭐ 大门的 targetScene = quest.targetScene（SceneGate.SetTarget）

城堡 · 推进
   └─ 击杀 / 到达 → progress++ → 满则 ReadyToTurnIn（不自动完成）→ MarkDirty

酒馆 · 告示板
   └─ 选 ReadyToTurnIn → 点「交付」
        ├─ 发奖励（魔晶 / 装备等级 / 解锁法术）
        ├─ QuestStatus = Completed → MarkDirty
        └─ 大门目标回落（SceneGate.ClearTarget 或指向下一条任务）
```

### 7.2 与大门 / SceneCatalog 的联动

- 任务的「地点」= `QuestConfig.targetScene`，取值必须是 `SceneCatalog.asset` 里登记过的 `sceneName`
- 接取时调 `SceneGate.SetTarget(...)`；没有进行中的任务时调 `ClearTarget()` 回落到 Inspector 默认值
- 大门上的提示文案（`Interactable.prompt`）建议也随任务变（"前往城堡" / "前往森林"），
  但 `prompt` 是 `[SerializeField]` 固定值 —— 需要给 `Interactable` 加一个运行时改文案的接口（待办）

---

## 8. 待拍板的 4 件事

| # | 问题 | 推荐 |
|---|---|---|
| 1 | 任务槽位固定 2 条，还是做动态列表？ | **V1 固定 2 槽**，限制同时最多接 2 条；动态列表留到任务数量真的超过 2 条时再做 |
| 2 | 能否同时接多条任务？ | **可以**（上限见 #1），不设前置链 |
| 3 | 「可交付」是进度打满自动转，还是必须回酒馆点交付？ | **必须回酒馆点交付**，否则"回酒馆交付"这条循环消失 |
| 4 | V1 是否加「到达场景」型目标？ | **加**，成本极低且"前往城堡"这条任务本来就需要 |

---

## 9. 落地顺序（每步独立可验收）

| 步 | 内容 | 验收 |
|---|---|---|
| 0 | **存档骨架**（`PlayerProgress` / `SaveSystem` / `ProgressService`） | 见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) §8 |
| 1 | `EnemyAIConfig.enemyId` + `EnemyHealth.ConfigId` + `GameEvents.EnemyKilled` | 杀怪能打出 `enemyId`（Console 验证） |
| 2 | `GameEvents.SceneEntered` + `QuestsChanged` | 进场景能收到 `sceneName` |
| 3 | `QuestConfig` SO + `QuestCatalog` + 2~3 条示例任务资产 | Inspector 里能看到配置 |
| 4 | `QuestService`（订阅 → 推进 → 状态机 → 广播） | 在 Console 里手动模拟事件，进度正确推进并落盘 |
| 5 | `QuestPanel` 接服务（列表 + 详情 + 按钮） | 面板显示真实任务，能接取 / 交付 |
| 6 | 大门联动（接取时 `SetTarget`） | 接「前往森林」后，酒馆大门真的去了森林 |
| 7 | 端到端联调 + 存档验证 | 接任务 → 打怪 → 退出游戏 → 重进 → 进度还在 → 回酒馆交付得奖励 |

---

## 10. 验收清单

- [ ] 告示板能列出**真实**任务，不再是占位文本
- [ ] 接取后状态变 `Active`，面板按钮变灰
- [ ] 接取后酒馆大门的目标场景 = 任务地点；没有任务时回落到默认
- [ ] 杀对应怪 / 到达目标场景 → 进度 +1，UI 实时刷新
- [ ] 进度打满 → `ReadyToTurnIn`，**不会**自动完成
- [ ] 回酒馆交付 → 奖励到账、状态 `Completed`
- [ ] **退出游戏重进**，任务状态与进度都还在
- [ ] 杀一只怪只加一次进度（防 T7 重复订阅）
- [ ] 场景里没有 `QuestService` 相关对象（它是自动创建的单例）

---

## 11. 相关文件

| 要动的东西 | 看这里 |
|---|---|
| 存档格式 / 落盘策略 | [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) |
| 策划意图 / 奖励数值 | [`PHASE1-DESIGN.md`](PHASE1-DESIGN.md) §4.3.4 |
| 面板视觉规范 | [`UI-STYLE.md`](UI-STYLE.md) §弹窗骨架 |
| 面板现有结构 | [`UI-PLAN.md`](UI-PLAN.md) §1.2 |
| 场景 key / 显示名 | `Assets/_Game/Scripts/Data/SceneCatalog.cs` + `.asset` |
| 大门切场景 | `Assets/_Game/Scripts/Gameplay/Interaction/SceneGate.cs` |
| 交互框架 | `Assets/_Game/Scripts/Gameplay/Interaction/` |
