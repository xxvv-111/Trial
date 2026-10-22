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

- ⭐ **一条任务 = 一组小目标**（2026-10-22 拍板）：`QuestConfig.objectives` 是列表，每个目标仍是
  `KillCount{ enemyId }` / `KillBoss` / `ReachScene{ sceneName }` 三种之一；**全部目标完成**才算可交付。
  「到达场景」因此不再是一种"任务类型"，而是复合任务里的一步（例：先到覆雪林地 → 再杀 5 只亡灵尸鬼 → 回酒馆交付）。
- ⭐ **前置任务链**（2026-10-22 拍板）：`QuestConfig.prerequisites` 列出必须先完成的任务，全部 `Completed`
  才从 `Locked` 变 `Available`；留空 = 无条件。**可以同时接多条**，多目标并行推进。
- 完整闭环：**酒馆接取 → 前往目标场景 → 目标逐个推进 → 回酒馆交付 → 发奖励 → 解锁下游任务**
- 任务进度**进存档**，退出游戏再进不丢
- 接取任务后**酒馆大门的目标场景随任务地点变化**
- 任务面板**动态列表**（2026-10-22 拍板）：不再固定 2 槽，用 `Row_Quest.prefab` 实例化，条目数随任务数走

### 2.2 本阶段不做（明确挂起）

- ~~任务前置链~~ / ~~动态列表~~ —— **2026-10-22 改为本阶段做**（见 §2.1 / §8）
- 对话与叙事（`PHASE1-DESIGN` §1.2 已排除）
- 限时任务 / 失败条件 / 放弃任务
- 任务分支（同一前置可以解锁多条任务，但**不按玩家选择分叉**）

### 2.3 当前任务表（2026-10-22 定，资产已建）

| # | questId | 标题 | 类别 | 前置 | 地点 | 目标 | 奖励 |
|---|---|---|---|---|---|---|---|
| 1 | `quest_01_depart` | 启程 | 主线 | 无 | Forest | `ReachScene` Forest ×1（前往覆雪林地） | 100 魔晶 |
| 2 | `quest_02_first_blood` | 初次交手 | 支线 | 无 | Forest | `KillCount` swordsman ×1（击败亡灵尸鬼）+ `KillCount` caster ×1（击败亡灵巫鬼） | 100 魔晶 |

资产：`Assets/_Game/Scripts/Data/Quests/`（`QuestCatalog.asset` + `quest_01_depart.asset` + `quest_02_first_blood.asset`）。
校验菜单：`Tools/任务/校验任务表`（当前 **0 问题**）。

> ⚠️ **奖励口径待确认**：「启程」原始需求写的是「100 金币」，但存档里目前只有 `magicCrystal`（魔晶）一种货币，
> 所以暂按 **100 魔晶**落地。若要真正的双货币（金币 / 魔晶），需要给 `PlayerProgress` 加 `gold` 字段、
> 存档版本升 v2 —— 现在业务未接入，改的成本仍为零，但请先确认要不要。

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

### 3.3 矛盾 3：目标不是"一个"，而是"一串"（2026-10-22 升级）

§4.3.4 只列了「击杀 N 只」「击败 Boss」两种，且默认一条任务只有一个目标 —— 于是"前往城堡"只能
被做成一条独立任务，而**用户反馈「到达场景型目标太简单，不配单独成任务」**。

**修订（2026-10-22 拍板）**：任务的目标从**单个**改为**列表**：

```csharp
public List<QuestObjective> objectives;   // 一条任务 1~N 个目标，全部完成才可交付
```

三个直接后果：

1. `ReachScene` 从"任务类型"降级为"目标类型"，典型用法是复合任务的第 1 步：
   `[到达 Castle] → [击杀 5 名 swordsman]`。这样"跑图"不再是独立任务，而是复合任务的开场。
2. **目标必须有稳定 id**（`objectiveId`）：存档按 id 记进度，将来往列表中间插一个目标，
   不会把其它目标的进度冲掉。
3. 存档结构从 `progress: int` 变成 `objectives: List<{objectiveId, progress}>`。
   ⚠️ **现在正是改的时机** —— 存档层刚落地、业务尚未接入、不存在任何玩家旧档，改结构成本为零。

### 3.4 缺口 1：敌人没有 id

`KillCount{ enemyId }` 需要一个**稳定的敌人标识**，而现在 `EnemyAIConfig` 只有 `displayName`（"拳击手"），
`EnemyHealth` 也不对外暴露 config。

**需要先补**：
1. `EnemyAIConfig.enemyId`（唯一字符串，例 `"swordsman"` / `"caster"` / `"boss_crichton"`）
2. `EnemyHealth.ConfigId`（只读属性，转发 `_config.enemyId`；配置为空时返回 `""` 并打 warning）
3. `GameEvents.EnemyKilled(string enemyId, Vector3 position)`

> ✅ **2026-10-22 已落地**。实做时把原设计的 `EnemyKilled(EnemyHealth, string, int exp)` 改成了两个参数：
> **去掉 `exp`**（工程里没有经验系统，凭空加个字段没人填）；**去掉 `EnemyHealth` 实例**（否则
> `Game.Core` 要依赖 `Game.Gameplay`，而 Gameplay 本就依赖 Core ⇒ 将来拆 asmdef 必成环）。
> 位置信息留给掉落物用；任务只需要 `enemyId`。已填的 id：`swordsman` / `caster` / `boss_crichton`。
>
> ⚠️ **Boss 也走 `EnemyHealth.Die`** ⇒ 击杀 Boss 会**同时**发 `EnemyKilled("boss_crichton")` 与 `BossDied`。
> 所以任务写 `KillBoss` 或 `KillCount{ targetId = boss_crichton }` 都能统计到，选一种即可（别两条都配，会重复计数）。

⚠️ **别用 `displayName` 当 id**：那是给人看的，改名就断任务。

### 3.5 缺口 2：`QuestUpdated(questId, cur, need)` 驱动不了现有 UI

现在的面板是「左列任务列表 + 右列详情」，需要的是**列表快照**；而这个事件只给单条任务的进度数字，
而且状态变化（接取 / 可交付 / 已完成）也需要刷新。

**修订**：事件退化成**脏通知** `QuestsChanged()`，UI 收到后主动向 `QuestService` 拉
`IReadOnlyList<QuestState>` 重建列表。**不要**为每种变化定义一种事件。

### 3.6 缺口 3：UI 只有 2 个任务槽 → **已决定做动态列表**

实测 `QuestPanel._entryLabels` 就是 **2 个**元素（`Row_Quest_1` / `Row_Quest_2`）。

**2026-10-22 拍板：走方案 B（动态列表）**。理由不是"以后可能更多"，而是**前置链一进来就必然超过 2 条**：
玩家同时要看得到「已接 2 条 + 可接 1 条 + 锁着 2 条」，2 个槽位连列表都列不全。

做法：把现有两行抽成 `Row_Quest.prefab`（复用 `UIBuilderKit` 的排版），`QuestPanel` 持有
`List<Row_Quest>`，用实例化 + 回收；条目数不超过池容量时只切 `SetActive`，不反复 `Instantiate`。

⚠️ 锁着的任务**要显示但置灰**（附一行"需先完成：XXX"），不要隐藏 —— 否则玩家不知道后面还有内容。

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
    public string questId;              // 唯一，⭐ 存档里存的就是它，定下别改
    public string title;                // 列表与详情标题
    [TextArea] public string description;
    public QuestCategory category;      // Main / Side（只影响 UI 标记）

    // ⭐ 前置任务：列出的任务全部 Completed 才可从 Locked → Available；空 = 无条件
    public List<string> prerequisites = new List<string>();

    // ⭐ 一条任务 = 1~N 个目标，全部完成才 ReadyToTurnIn
    public List<QuestObjective> objectives = new List<QuestObjective>();

    public string targetScene;          // 任务地点（对应 SceneCatalog 的 sceneName）；可空 = 不动大门
    public List<QuestReward> rewards;   // MagicCrystal / Spell / Equipment
}

[Serializable]
public class QuestReward
{
    public RewardKind kind;             // MagicCrystal = 0 / Spell = 1 / Equipment = 2
    public string id;                   // Spell / Equipment 用；MagicCrystal 留空
    public int amount = 1;
}

[Serializable]
public class QuestObjective
{
    public string objectiveId;          // ⭐ 稳定 id（例 "kill_swordsman"）；存档按它记进度，别改
    public QuestObjectiveType type;
    public string targetId;             // KillCount → enemyId；ReachScene → sceneName；KillBoss → 留空
    public int needCount = 1;           // KillCount 用；其余填 1
    public string hint;                 // 详情面板那一行文案，如"击杀 5 名剑兵"
}

public enum QuestObjectiveType { KillCount = 0, KillBoss = 1, ReachScene = 2 }
```

> **枚举 + 参数**，不要为每种目标写一个类 —— 原规划这条是对的，保留。
> 加新目标类型 = 加枚举项 + 在 `QuestService` 加一个 `case`。

### 4.2 运行时：`QuestState`

```csharp
public class QuestState
{
    public string questId;
    public QuestStatus status;              // Locked / Available / Active / ReadyToTurnIn / Completed
    public List<ObjectiveState> objectives; // 与 QuestConfig.objectives 按 objectiveId 对齐
    public QuestConfig Config;              // 运行时按 questId 查表得到（不存盘）

    public bool AllObjectivesDone { get; }  // 所有 objectives 都 progress >= need
}

public class ObjectiveState
{
    public string objectiveId;
    public int progress;
    public int need;
}
```

### 4.3 存档：`QuestSave`

见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) §3 —— 存 `questId / status / objectives[{objectiveId, progress}]`，
**不存**标题描述等静态内容（那些从 `QuestConfig` 读，改文案不用改存档），
**也不存**前置任务（那是配置，不是进度）。

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
- 启动时把 `ProgressService.Progress.quests` 与任务表对齐，分三种情况：
  1. 表里有、存档没有 → 新建 `QuestSave`，状态按前置算：**前置全 `Completed` → `Available`，否则 `Locked`**
  2. 存档里有、表里没有（任务被删）→ **保留数据不动**，UI 不显示（删掉就找不回来了）
  3. 两边都有 → 补上配置里新增的目标（`progress = 0`），剔掉配置里已删除的目标
- ❗ **启动时校验前置图**（一次即可，失败打 error 不崩）：自环、环、引用了不存在的 `questId`。
  不校验的后果是玩家看到一条**永远锁着**的任务，而没有任何提示
- 进度变化 → 写 `QuestSave` → `ProgressService.MarkDirty()` → 广播 `QuestsChanged`
- 某条任务转为 `Completed` 后，**重算所有 `Locked` 任务**：前置满足了就提升为 `Available`（并发 `QuestsChanged`）

### 5.1 谁订阅什么

| 事件 | 谁发 | `QuestService` 的反应 |
|---|---|---|
| `GameEvents.EnemyKilled(string enemyId, Vector3 position)` | `EnemyHealth.Die`（只广播一次，带 `_killedBroadcast` 守卫） | 遍历**所有 `Active` 任务的每个目标**：`KillCount` 且 `targetId == enemyId` → 该目标 progress++ |
| `GameEvents.BossDied` | `BossController.EnterDeath` | 同上：类型 `KillBoss` 的目标 → progress = need |
| `GameEvents.SceneEntered(string sceneName)` | 场景加载后 | 同上：类型 `ReachScene` 且 `targetId == sceneName` → progress = need |

每次推进后重算整条任务：**全部目标 `progress >= need` → `ReadyToTurnIn`**（仍不自动 `Completed`）。

⚠️ **`QuestService` 必须只订阅一次**。它是 `DontDestroyOnLoad` 单例 ⇒ 切场景不会重建 ⇒
在 `Awake` 里订阅、`OnDestroy` 里退订即可。**不要**写在 `OnEnable`（场景切换会重复叠加，
症状是"杀一只怪进度 +2"，这正是 `EnemyHealth.Died` 的 T7 问题）。

---

## 6. 事件扩展

在现有 `Core/GameEvents.cs`（只有 `PlayerDied` / `BossDied`）上新增：

```csharp
public static event Action<string, Vector3> EnemyKilled;   // enemyId, 死亡位置（⚠️ id 可能为空）
public static event Action<string> SceneEntered;            // sceneName
public static event Action QuestsChanged;                   // 任务数据变了（脏通知）
```

> ✅ **2026-10-22 已落地**：`SceneEntered` 由 `Core/SceneFlow.cs` 转发 —— 它是常驻对象
> （`BeforeSceneLoad` 自动创建 + `DontDestroyOnLoad`），并在 `Start` 里对首个场景补发一次
> （首个场景的 `sceneLoaded` 可能在订阅之前就已触发）。
> ❗ **不能让场景里的对象直接订阅 `sceneLoaded`**：那个订阅者会在切场景时被销毁，
> 于是最后那次加载永远收不到。
> `QuestsChanged` 目前还没有发射方，等 `QuestService`（第 4 步）。

⚠️ **静态事件必须成对订阅/退订**（`OnEnable` 订 / `OnDisable` 退，或常驻单例的 `Awake`/`OnDestroy`）。
历史坑：T7（症状是"杀一只怪进度 +2"）。

---

## 7. 流程与联动

### 7.1 主循环

```
酒馆 · 告示板（Interactable → QuestPanel.Open）
   └─ 动态列表：Locked（置灰，带"需先完成 XXX"）/ Available / Active / ReadyToTurnIn
        └─ 选一条 Available → 点「接取」
             ├─ QuestStatus = Active
             ├─ objectives 全部 progress = 0 → MarkDirty
             └─ ⭐ 大门的 targetScene = quest.targetScene（SceneGate.SetTarget）

城堡 · 推进（多目标并行）
   └─ 击杀 / 到达 → 命中对应目标 → 该目标 progress++
        └─ 全部目标达标 → ReadyToTurnIn（不自动完成）→ MarkDirty

酒馆 · 告示板
   └─ 选 ReadyToTurnIn → 点「交付」
        ├─ 发奖励（魔晶 / 装备等级 / 解锁法术）
        ├─ QuestStatus = Completed → MarkDirty
        ├─ ⭐ 重算所有 Locked 任务：前置满足了就转 Available（前置链由此解开）
        └─ 大门目标回落（SceneGate.ClearTarget 或指向下一条任务）
```

示例：「铁匠的请求」= 目标 [到达 Castle] → [击杀 5 × swordsman] → [击杀 boss_crichton]；
目标 `hint` 分别是"前往城堡" / "击杀 5 名剑兵" / "击败克里顿"。

### 7.2 与大门 / SceneCatalog 的联动

- 任务的「地点」= `QuestConfig.targetScene`，取值必须是 `SceneCatalog.asset` 里登记过的 `sceneName`
- 接取时调 `SceneGate.SetTarget(...)`；没有进行中的任务时调 `ClearTarget()` 回落到 Inspector 默认值
- 大门上的提示文案（`Interactable.prompt`）建议也随任务变（"前往城堡" / "前往森林"），
  但 `prompt` 是 `[SerializeField]` 固定值 —— 需要给 `Interactable` 加一个运行时改文案的接口（待办）

---

## 8. 已拍板的 4 件事（2026-10-22 13:40）

| # | 问题 | 结论 | 与原推荐的差异 |
|---|---|---|---|
| 1 | 任务槽位固定 2 条，还是动态列表？ | **动态列表** | ⚠️ **推翻原推荐**（原推荐固定 2 槽） |
| 2 | 能否同时接多条任务？能不能有前置？ | **可以同时接多条**；新增 **`prerequisites` 前置任务**，未全部完成则 `Locked` 不可接，可为空 | ⚠️ **新增能力**（原规划明确写"不做前置链"） |
| 3 | 「可交付」自动转还是必须回酒馆点交付？ | **必须回酒馆点交付** | 一致 |
| 4 | V1 要不要「到达场景」型目标？ | **要，但不做成独立任务** —— 它只是复合任务里的一个目标 | ⚠️ **改性质**（从"任务类型"降为"目标类型"） |

**我自己拍的两个实现细节**（不影响玩法，可随时推翻）：

1. **多目标并行推进**，不是"必须按顺序完成"。理由：数据结构最简（目标之间无状态依赖），
   而且"先到城堡再杀怪"这种顺序感用**文案**（`hint` 带序号）就能表达，不必写进状态机。
   将来真需要强顺序，加一个 `int stage` 字段就行。
2. **前置判定用"全部完成"（ALL）**，不是"任一完成"。任务链几乎都是 AND 语义；
   OR 语义的情况交给"两条独立任务"表达更清楚。

> ⚠️ 第 2 条带来一个**必须做的事**：`QuestCatalog` 启动时校验前置图（自环 / 环 / 引用了不存在的
> `questId`）。不校验的后果是任务永远锁着且无提示，跟 bug 无法区分（见 §5）。

---

## 9. 落地顺序（每步独立可验收）

| 步 | 内容 | 验收 |
|---|---|---|
| 0 | **存档骨架**（`PlayerProgress` / `SaveSystem` / `ProgressService`） | 见 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md) §8 |
| 1 | ~~`EnemyAIConfig.enemyId` + `EnemyHealth.ConfigId` + `GameEvents.EnemyKilled`~~ ✅ **2026-10-22 已完成** | 杀一只怪，Console 打出 `[EnemyKilled] swordsman（剑兵）@ (x, y, z)`；配置漏填则打 warning |
| 2 | ~~`GameEvents.SceneEntered` + `QuestsChanged`~~ ✅ **2026-10-22 已完成** | 进场景在 Console 看到 `[SceneEntered] Castle`；`QuestsChanged` 无订阅方时调用不报错 |
| 3 | ~~`QuestConfig` SO（含 `prerequisites` / `objectives[]`）+ `QuestCatalog` + 前置图校验 + 示例任务~~ ✅ **2026-10-22 已完成** | 菜单 `Tools/任务/校验任务表` 通过（2 条任务、0 问题）；故意写一个环应该报错 |
| 4 | `QuestService`（订阅 → 多目标推进 → 状态机 → 前置解锁 → 广播） | Console 手动模拟事件，多目标各自推进正确、全满转 `ReadyToTurnIn`、交付后下游从 `Locked` 变 `Available` |
| 5 | `Row_Quest.prefab` + `QuestPanel` 动态列表（列表 + 详情 + 目标清单 + 按钮） | 列表条目随任务数变化，锁着的置灰并显示原因 |
| 6 | 大门联动（接取时 `SetTarget`） | 接「前往森林」后，酒馆大门真的去了森林 |
| 7 | 端到端联调 + 存档验证 | 接任务 → 打怪 → 退出游戏 → 重进 → 进度还在 → 回酒馆交付得奖励 |

---

## 10. 验收清单

- [ ] 告示板能列出**真实**任务，不再是占位文本
- [ ] 接取后状态变 `Active`，面板按钮变灰
- [ ] 接取后酒馆大门的目标场景 = 任务地点；没有任务时回落到默认
- [ ] 杀对应怪 / 到达目标场景 → 对应**那一个**目标的计数 +1，UI 实时刷新
- [ ] **多目标任务**：只有全部目标达标才转 `ReadyToTurnIn`，缺一个不转
- [ ] **前置链**：前置未完成的任务显示为 `Locked` 且不可接；前置交付后自动变 `Available`
- [ ] 前置图里有环 / 引用不存在的 id → 启动时 console 有 error，**但游戏不崩**
- [ ] 动态列表：任务数 1 / 2 / 5 时列表都对，不出现重叠或空行
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
