# 任务系统 · 代码导读（写给新手）

> 最后更新：2026-10-23
>
> 这份文档只回答三件事：**我做了什么**、**每个脚本干什么**、**它们怎么串起来**。
> 设计决策的「为什么」在 [`QUEST-SYSTEM.md`](QUEST-SYSTEM.md)，存档细节在 [`SAVE-SYSTEM.md`](SAVE-SYSTEM.md)，
> 本文是**代码地图**，看完你应该能自己找到任何一处逻辑在哪。

---

## 0. 先建立整体印象：把任务系统当成一家餐厅

代码里的名词都很抽象，先用一个具体的东西对照一下：

| 代码里的东西 | 餐厅里的对应物 | 特点 |
|---|---|---|
| `QuestConfig`（任务配置） | 菜单上的一道菜 | 静态的，写死在纸上，游戏运行中不会变 |
| `QuestCatalog`（任务表） | 整本菜单 | 把所有菜订在一起 |
| `QuestSave`（存档里的任务数据） | 客人的点单记录 | 会变，关店后还要留着 |
| `QuestService`（任务服务） | 服务员 | 听动静、改点单、通知前台 |
| `GameEvents`（事件） | 传菜铃 | 厨房出菜响一下，服务员就过去 |
| `ProgressService`（存档服务） | 收银台的账本 | 负责把点单记录写到纸上（硬盘） |
| UI（还没做） | 菜单板 / 点餐界面 | 给玩家看的 |

**关键分工**：菜单（配置）不会变；点单记录（存档）会变；服务员（Service）负责改记录；
账本（存档系统）负责把记录写到硬盘。**这四个东西各管一件事，谁都不越界。**

---

## 1. 全景图

```
                        ① 静态配置（ScriptableObject 资产，编辑器里配）
                        ┌──────────────────────────────────────────┐
                        │ Resources/Quests/QuestCatalog.asset       │
                        │   └─ quest_01_depart.asset                │
                        │   └─ quest_02_first_blood.asset           │
                        └───────────────────┬──────────────────────┘
                                            │ Resources.Load
                                            ▼
  ② 事件（谁在喊）            ③ 服务（谁在算）              ④ 存档（谁在记）
  ┌──────────────┐          ┌──────────────────┐        ┌──────────────────┐
  │ GameEvents   │─ 订阅 ──►│ QuestService     │─ 写 ──►│ PlayerProgress   │
  │ EnemyKilled  │          │  - 推进目标      │        │   .quests[]      │
  │ BossDied     │          │  - 状态机        │        │        │         │
  │ SceneEntered │          │  - 接取/交付     │        │        ▼         │
  │ PointReached │          │  - 发奖励        │        │ ProgressService  │
  └──────────────┘          └──────────────────┘        │  MarkDirty()     │
         ▲                          ▲                  │  0.5s → save.json│
         │                          │                  └──────────────────┘
  谁在喊：EnemyHealth / BossController / SceneFlow / QuestPointTrigger
```

---

## 2. 脚本清单（先看总表，不用记）

所有路径都在 `Demo-main/Assets/_Game/` 下。**共 17 个文件、约 2530 行**，其中任务本身约 1270 行。

| 层 | 文件 | 约多少行 | 一句话作用 |
|---|---|---|---|
| 事件 | `Scripts/Core/GameEvents.cs` | 56 | 全局事件清单（谁死了、进了哪个场景、踩到哪个点） |
| 事件 | `Scripts/Core/SceneFlow.cs` | 77 | 把「场景加载完成」转成一条事件 |
| 配置 | `Scripts/Data/QuestConfig.cs` | 116 | 一条任务长什么样（标题 / 前置 / 目标 / 奖励） |
| 配置 | `Scripts/Data/QuestCatalog.cs` | 186 | 任务表 + 体检（查配置写错） |
| 配置 | `Resources/Quests/*.asset` | — | 具体两条任务的数据 |
| 运行时 | `Scripts/Data/QuestState.cs` | 104 | 给界面读的「任务现在什么样」+ 纯规则函数 |
| 运行时 | `Scripts/Gameplay/Quest/QuestService.cs` | 438 | **核心**：推进进度、接取、交付、发奖励 |
| 运行时 | `Scripts/Gameplay/Quest/QuestPointTrigger.cs` | 79 | 场景里的「探查点」，玩家走进来就上报 |
| 存档 | `Scripts/Save/ProgressService.cs` | 147 | 进度唯一入口（0.5 秒后自动存盘） |
| 存档 | `Scripts/Save/SaveData.cs` | 112 | 存档长什么样（含 `quests` 和两种货币） |
| 存档 | `Scripts/Save/SaveSystem.cs` | 394 | 打包 / 拆包 / 校验 / 版本迁移 |
| 存档 | `Scripts/Save/IFileStore.cs` | 24 | 「存储」的合同（换硬盘/换内存都行） |
| 存档 | `Scripts/Save/DiskFileStore.cs` | 77 | 真正写硬盘的（防断电） |
| 存档 | `Scripts/Save/MemoryFileStore.cs` | 71 | 假硬盘，只给测试用 |
| 工具 | `Editor/SaveMenu.cs` | 83 | 菜单：打开/打印/造测试档/删档 |
| 工具 | `Editor/SaveSelfTest.cs` | 357 | 菜单：存档 31 项自测 |
| 工具 | `Editor/QuestCatalogMenu.cs` | 107 | 菜单：校验任务表 / 打印状态 |
| 工具 | `Editor/QuestRulesSelfTest.cs` | 104 | 菜单：状态机 14 项自测 |

> 为什么这么多文件？因为**一个文件只干一件事**。看起来散，但出问题时你只要定位到一层，
> 比如"进度没存下来"→ 只看存档层（第 5 节），"进度没推进"→ 只看事件层和服务层。

---

## 3. 逐个讲：事件层

### 3.1 `GameEvents.cs` —— 全局事件清单

**它是什么**：一个只有 `event` 和 `Raise` 方法的静态类。任何人想「喊一声」，就调 `GameEvents.RaiseXxx()`；
任何人想「听」，就 `GameEvents.Xxx += 我的方法`。

**任务相关的四条**：

```csharp
EnemyKilled(string enemyId, Vector3 位置)   // 有敌人死了，谁发的：EnemyHealth
BossDied                                    // Boss 死了，谁发的：BossController
SceneEntered(string sceneName)              // 进了场景，谁发的：SceneFlow
PointReached(string pointId)                // 走进探查点，谁发的：QuestPointTrigger
QuestsChanged()                             // 任务数据变了（给 UI 的提醒，暂无人发）
```

**新手最容易犯的错**：只订阅、不退订。切一次场景就多一个订阅者，杀一只怪进度 +2。
所以规定：**常驻对象在 `Awake` 订、`OnDestroy` 退；场景对象在 `OnEnable` 订、`OnDisable` 退。**

### 3.2 `SceneFlow.cs` —— 场景切换的「中继站」

Unity 自己有个事件 `SceneManager.sceneLoaded`，但它有个坑：**订阅它的对象活得不够久就收不到**。
如果订阅者摆在场景里，切场景时它先被销毁了，于是"最后那次加载"永远收不到。

所以做了一个自动创建、永不销毁的 `[SceneFlow]` 对象，由它订阅 `sceneLoaded`，再转发成
`GameEvents.SceneEntered`。里面还有个细节：**第一个场景**的加载可能发生在它创建之前，
所以 `Start()` 里补发一次，用标志位防止重复。

---

## 4. 逐个讲：配置层

### 4.1 `QuestConfig.cs` —— 一条任务的「表格」

它是 `ScriptableObject`，意思是**一个 `.asset` 文件**，可以在 Inspector 里填，不用改代码。
一条任务的字段：

| 字段 | 类型 | 白话解释 |
|---|---|---|
| `questId` | string | 任务的身份证号，例 `quest_01_depart`。**存档里存的就是它，定了别改** |
| `title` | string | 标题，例「启程」 |
| `description` | string | 详情面板的描述 |
| `category` | 枚举 | 主线 / 支线，目前只影响界面上的标记 |
| `prerequisites` | 字符串列表 | 前置任务。**全部完成**才能接；空 = 没有门槛 |
| `objectives` | 列表 | 目标。1 个或多个，**全部完成**才能交付 |
| `targetScene` | string | 任务地点（例 `Forest`），接取后酒馆大门会指过去 |
| `rewards` | 列表 | 奖励：金币 / 魔晶 / 法术 / 装备 |

**目标**是嵌在里面的小结构：

```csharp
QuestObjective {
    objectiveId;   // 目标的身份证号，例 kill_enemy1。⭐ 存档按它记进度，别用下标
    type;          // KillCount 击杀 / KillBoss 打 Boss / ReachScene 到场景 / ReachPoint 到点
    targetId;      // 杀什么 / 去哪个场景 / 哪个点
    needCount;     // 需要几次
    hint;          // 界面上显示的一行字，例「击败 1 个亡灵尸鬼」
}
```

**为什么目标要有自己的 id？** 因为存档里要记「第几个目标打了几只」。如果用下标（第 0 个、第 1 个），
你以后在配置里往中间插一个新目标，所有人存档里的进度就整体错位了 —— 而且不报错，只是数据悄悄错。

### 4.2 `QuestCatalog.cs` —— 任务表 + 体检

一张表装所有任务，另外提供一个 `Validate()`（体检）。它会检查：

1. `questId` 有没有重复
2. 标题有没有空
3. 有没有任务一个目标都没有
4. 同一个任务里 `objectiveId` 有没有重复
5. `needCount` 是不是 ≥ 1
6. 击杀目标有没有填敌人 id
7. 到达场景目标有没有填场景名，且这个场景在 `SceneCatalog` 里登记过
8. 前置任务是否存在、有没有写自己
9. **前置有没有成环**（A 要 B 完成、B 要 A 完成 → 两个永远锁着）

第 9 条用了一个小算法叫 **DFS（深度优先搜索）**：从一条任务出发，顺着「前置」一路往上找，
如果又找回了自己，就是环。菜单 `Tools/任务/校验任务表` 就是跑这个。

> 为什么值得写体检？因为这类错误的表现是「某条任务永远锁着，界面上一句话都没有」，
> 跟 bug 无法区分。造环的实测输出：`前置任务是环：quest_01_depart → quest_02_first_blood → quest_01_depart`。

### 4.3 `Resources/Quests/` 里的三个资产

| 资产 | 内容 |
|---|---|
| `QuestCatalog.asset` | 指向下面两条任务 + 指向 `SceneCatalog`（用来校验场景名） |
| `quest_01_depart.asset` | 启程：主线、无前置、Forest、走到 `point_forest_scout`、奖励 100 金币 |
| `quest_02_first_blood.asset` | 初次交手：支线、无前置、Forest、杀 enemy1 ×1 + enemy2 ×1、奖励 100 魔晶 |

**为什么放在 `Resources` 文件夹？** 因为 `QuestService` 是运行时**自动创建**的，
没有 Inspector 面板可以拖引用，只能用 `Resources.Load<QuestCatalog>("Quests/QuestCatalog")` 按路径加载。

---

## 5. 逐个讲：运行时层

### 5.1 `QuestState.cs` —— 「任务现在什么样」+ 纯规则

两个小类 + 一个工具类：

- `ObjectiveState`：一个目标现在打了几只 / 要几只。
- `QuestState`：一条任务现在的状态 + 它的目标列表。
- `QuestRules`：三个**纯函数**（不碰文件、不碰 Unity 对象）。

```csharp
AllObjectivesDone(目标列表)        // 是不是全打满了
PrerequisitesDone(配置, 查状态)    // 前置是不是全完成了
Recompute(当前状态, 全打满, 前置完成)  // 算出新状态
```

**为什么要单独抽 `QuestRules`？** 因为状态机是最容易写错的地方，而它又是纯逻辑 ——
抽出来就能**不进 Play、不造存档、毫秒级测 14 项**。这是"把难的东西变简单"的典型手法。

状态机就是这张表：

```
Locked ──前置完成──► Available ──玩家点接取──► Active
                                                  │
                                          目标全部打满
                                                  ▼
                                           ReadyToTurnIn ──玩家点交付──► Completed（终态）
```

三条铁律：`Completed` 不会回退；打满**只到**「可交付」，不自动完成；`Locked → Available` 只看前置。

### 5.2 `QuestService.cs` —— 核心（438 行，值得慢慢看）

它是**唯一**知道任务规则的地方。分成五块：

**① 启动初始化**（`Start` 里）

把「配置」和「存档」对齐：
- 配置里有、存档里没有 → 建一条记录，状态按前置算（前置全完成 → `Available`，否则 `Locked`）
- 两边都有 → 按 `objectiveId` 对齐目标：新增的补 0，配置里删掉的剔掉，进度夹在 `[0, need]`
- 存档里有、配置里没有（任务被删）→ **保留不动**，删了就找不回来了

> ⚠️ 为什么放 `Start` 不放 `Awake`：`ProgressService` 和 `QuestService` 都是「进游戏前」创建的，
> 谁先谁后没有保证。放 `Awake` 里可能拿到 null。

**② 听事件 → 推进**（四个 `OnXxx` 方法）

```
收到 EnemyKilled(enemyId)  → 找所有 Active 任务里 type=KillCount 且 targetId 匹配的目标 → +1
收到 PointReached(pointId) → 找 type=ReachPoint 且 targetId 匹配的目标 → +1
...
```

推进完重算状态（全满就变 `ReadyToTurnIn`），写回存档，`MarkDirty()`，广播 `QuestsChanged`。

**③ 接取 `Accept(questId)`**

只有 `Available` 能接，接之前**再验一次**前置（防止状态被别处改过）。

**④ 交付 `TurnIn(questId)`**

只有 `ReadyToTurnIn` 能交 → 发奖励 → 变 `Completed` → **重算所有 `Locked` 任务**
（前置满足了就变 `Available`，任务链就是这样一环一环解开的）。

**⑤ 发奖励 `GrantRewards`**

金币加 `gold`、魔晶加 `magicCrystal`、法术加进已学列表、装备加等级。

**它绝对不写文件** —— 只调 `ProgressService.MarkDirty()`。这条纪律很重要：如果两个系统各自
`File.WriteAllText`，谁后写谁覆盖，症状是"偶尔丢进度"，极难查。

### 5.3 `QuestPointTrigger.cs` —— 场景里的「探查点」

**为什么需要它**：`ReachScene` 只能表达"进了这张地图"。而「启程」这个任务是去**探查上山通路**，
进 Forest 显然不算完成，必须走到路线末端。所以加了一个更细的目标类型 + 这个组件。

**怎么用（游戏里）**：在路线末端建一个空物体 → 加 `SphereCollider`（勾 `Is Trigger`）→ 挂这个组件 →
`pointId` 填 `point_forest_scout`。玩家走进范围就发 `GameEvents.PointReached`。

**代码里做了什么**：只用玩家层（第 8 层）过滤，避免怪物或子弹误触发；只上报一次；
漏填 id 打 warning。拼错了它救不了你 —— 只能靠复制粘贴。

---

## 6. 逐个讲：存档层（为了任务系统先做的地基）

任务进度得存下来，所以先有了一层存档。它 6 个文件，可以按「从外到里」理解：

```
QuestService ─► ProgressService ─► SaveSystem ─► IFileStore ─► DiskFileStore ─► 硬盘
   改数据         唯一入口+防抖      打包+校验      合同          真写盘
```

| 文件 | 白话作用 | 关键点 |
|---|---|---|
| `ProgressService.cs` | 进度的**唯一入口**，进游戏自动创建、永不销毁 | 改完数据调 `MarkDirty()`，0.5 秒后自动存；暂停时也照存 |
| `SaveData.cs` | 存档的数据结构（所有字段） | 字段名就是硬盘上的名字，**改名 = 丢数据** |
| `SaveSystem.cs` | 把对象变文字（打包）、把文字变对象（拆包） | 带版本号、带指纹校验、坏档能降级 |
| `IFileStore.cs` | 「存储」的合同：能存、能读、能换名 | 有了它，测试可以换成假硬盘 |
| `DiskFileStore.cs` | 真正写硬盘 | **原子写**：先写草稿，写完才替换正本，旧正本留作备份 |
| `MemoryFileStore.cs` | 假硬盘（存在内存里） | 测试专用，还能模拟"写到一半断电" |

**三个必须知道的设计**：

1. **原子写**：直接 `File.WriteAllText` 时断电会留下半截文件。所以流程是
   「写 `.tmp` → 刷到物理盘 → 把旧正本改名成 `.bak` → 把 `.tmp` 改名成正本」。
   任何时刻硬盘上都有一份完整档。
2. **指纹（CRC32）**：文件最后一行是 `#crc32:9A3F21B7`。读的时候重算一遍，对不上就是被改过/截断。
   这一行**不存在**时程序认为"你手改的"，放行并打 warning —— 方便调试。
3. **防抖**：不是每次改数据都写盘（写盘很慢），而是做记号 + 等 0.5 秒没新改动再写。
   ⚠️ 用的是 `unscaledDeltaTime`，因为暂停时 `timeScale = 0`，用普通时间永远等不到。

---

## 7. 逐个讲：编辑器工具（不用进 Play 就能验）

| 菜单 | 做什么 | 现在的状态 |
|---|---|---|
| `Tools/存档/自测（31 项）` | 存档系统的 31 项检查：往返、坏档、版本、原子写 | **31/31 通过，约 130 ms** |
| `Tools/存档/打印存档路径与大小` | 告诉你存档到底在哪、多大 | 可用 |
| `Tools/存档/写入测试数据` | 造一份"魔晶 999 + 一个任务"的档 | 可用 |
| `Tools/存档/删除存档` | 删档（带二次确认） | 可用 |
| `Tools/任务/校验任务表` | 跑上面说的 9 项体检 | **2 条任务、0 问题** |
| `Tools/任务/打印任务表` | 把配置逐条打出来 | 可用 |
| `Tools/任务/状态机自测` | 14 项纯逻辑检查 | **14/14 通过** |
| `Tools/任务/打印任务状态（需 Play）` | 运行时每条任务的状态和目标进度 | 需 Play |

---

## 8. 一次完整流程（从接取到交付）

```
① 玩家在酒馆点告示板 → 面板列表里看到「启程」状态 Available
       │（UI 还没做，这一步目前只能靠代码调 Accept）
       ▼
② QuestService.Accept("quest_01_depart")
       ├─ 检查：状态必须是 Available、前置必须全完成
       ├─ 状态 → Active，写回 PlayerProgress.quests
       ├─ ProgressService.MarkDirty()   → 0.5 秒后写进 save.json
       └─ GameEvents.RaiseQuestsChanged() → 通知 UI 刷新
       ▼
③ 玩家走大门去 Forest
       └─ SceneManager 加载完 → SceneFlow 收到 → GameEvents.SceneEntered("Forest")
              └─ QuestService：没有 ReachScene 目标，什么也不做（正常）
       ▼
④ 玩家一路打怪
       └─ 敌人死亡 → EnemyHealth 发 EnemyKilled("enemy1")
              └─ QuestService：任务里是 ReachPoint 目标，不匹配 → 什么也不做
       ▼
⑤ 玩家走进探查点
       └─ QuestPointTrigger 发 PointReached("point_forest_scout")
              └─ QuestService：目标 targetId 匹配 → progress 0→1
                     ├─ 全部目标完成 → 状态 ReadyToTurnIn
                     ├─ 写回存档 + MarkDirty
                     └─ RaiseQuestsChanged
       ▼
⑥ 玩家回酒馆点「交付」
       └─ QuestService.TurnIn("quest_01_depart")
              ├─ 检查：状态必须是 ReadyToTurnIn
              ├─ 发奖励：progress.gold += 100
              ├─ 状态 → Completed（终态）
              ├─ 重算所有 Locked 任务（前置满足的 → Available）
              ├─ 写回存档 + MarkDirty
              └─ RaiseQuestsChanged
       ▼
⑦ 0.5 秒后 save.json 落盘；退出游戏再进，进度还在
```

---

## 9. 我改动过的**既有**文件（不是新建的）

| 文件 | 改了什么 | 为什么 |
|---|---|---|
| `Scripts/Data/EnemyAIConfig.cs` | 加 `enemyId` 字段 | 任务要按稳定 id 统计击杀，不能拿中文显示名（改名就断） |
| `Scripts/Gameplay/Enemy/EnemyHealth.cs` | 加 `ConfigId` 属性；死亡时发 `EnemyKilled`；加"只上报一次"的守卫 | 让击杀能被任务听到；守卫防止一帧多次判定导致进度 +2 |
| `Scripts/Data/EnemyAIConfig.asset` 等 3 个 | `displayName` 改为亡灵尸鬼 / 亡灵巫鬼 / 骷髅骑士 | 专名定稿 |
| `Scripts/Data/BossConfig.cs` | **试过拆成独立配置，当天回退了** | 数值被拆到两个资产，调参要记两个地方，不划算 |
| `Scripts/Data/SceneCatalog.asset` | 显示名改为麋鹿酒馆 / 覆雪林地 / 灰烬堡 | 场景显示名统一 |

**没有动**：任何场景、任何预制体、`ProjectSettings`（改产品名会换存档目录）。

---

## 10. 名词表（遇到不懂的回来查）

| 词 | 大白话 |
|---|---|
| 序列化 / 反序列化 | 把对象变成一串文字 / 把文字变回对象 |
| ScriptableObject | Unity 的"配置文件"类型，存成 `.asset`，能在 Inspector 里填 |
| 单例（Singleton） | 全局只有一个的对象，用 `Instance` 访问 |
| `DontDestroyOnLoad` | 切场景时不要销毁我（常驻对象） |
| 脏标记（dirty） | "我改过了，记得存"的记号 |
| 防抖（debounce） | 攒一会儿再干活，避免频繁执行 |
| 原子写 | 要么整体成功、要么当没发生，不会留下半截文件 |
| CRC 校验 | 文件的指纹，用来发现内容被改过 |
| 幂等（idempotent） | 做一次和做十次结果一样（例如"到达场景"设成已到达） |
| 枚举（enum） | 一组固定选项，例 `KillCount / KillBoss / ReachScene / ReachPoint` |

---

## 11. 现在能做什么、还不能做什么

**已经能跑通（逻辑层）**

- 两条任务配置可读、可校验（0 问题）
- 击杀敌人 / 进场景 / 踩探查点 → 会推进对应目标
- 目标全满 → 自动变「可交付」
- 接取、交付、发奖励的 API 都在，且会写进存档
- 退出游戏再进，任务状态和目标进度还在（由存档层保证）
- 存档系统 31 项自测、状态机 14 项自测，全绿

**还没做**

- **没有界面**：玩家点不到"接取/交付"，目前只能代码调 `Accept` / `TurnIn`
- **大门联动**：接取后不会自动把酒馆大门指向任务地点（第 6 步）
- **教学引导**：这条主线途中遇怪当教学，但引导 UI 还没做
- **没在 Play 模式跑过**：探索点、击杀推进都只是逻辑上通，需要你实际玩一次

---

## 12. 你可以自己验的三件事（不用写代码）

1. **配置有没有写错**：菜单 `Tools/任务/校验任务表` → 期望绿色 `校验通过：2 条任务`。
2. **存档有没有问题**：菜单 `Tools/存档/自测（31 项）` → 期望绿色 `31/31 全部通过`。
3. **状态机有没有问题**：菜单 `Tools/任务/状态机自测` → 期望绿色 `14/14 全部通过`。

想亲手弄坏一次看看？把 `quest_01_depart.asset` 的前置填成 `quest_02_first_blood`、
再把 `quest_02_first_blood` 的前置填成 `quest_01_depart`，再跑一次校验 —— 会看到它准确报出环的路径。
（看完记得改回来，或者用 `git checkout` 恢复。）

---

## 13. 常见疑问

**Q：为什么任务不直接写死在代码里？**
A：因为任务内容会反复改（文案、数量、奖励）。写死在代码里，改一个字就要重新编译；
做成资产，策划在 Inspector 里改完保存就生效。

**Q：为什么不把进度存在 `QuestConfig` 里？**
A：配置是"全玩家共用一份"的资产，进度是"每个玩家不同"。放一起会导致 A 玩家过关后 B 玩家一开局就是完成状态。

**Q：为什么进度满了不自动算完成？**
A：因为「回酒馆交付」是这条循环的终点。自动完成的话，玩家就再也不用回酒馆了，那条动线消失。

**Q：为什么任务表要放 `Resources`？**
A：因为服务是自动创建的，没有 Inspector 面板能拖引用。`Resources.Load` 是按路径加载，不需要引用。

**Q：`questId` / `objectiveId` / `enemyId` 到底能不能改？**
A：**不能**。它们是存档里的键，改了旧档就找不到对应记录，表现是"进度突然清零"。

**Q：为什么两个自测都只有几十项，不写更全？**
A：自测是按"最可能出错的地方"写的，不是覆盖率比赛。目前覆盖的是：存档往返/坏档/版本迁移/原子写/状态机。
