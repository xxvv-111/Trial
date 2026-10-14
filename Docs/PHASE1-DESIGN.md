# 第一阶段策划案（Phase 1 Design）

> **本文件是什么**：把《试炼》**第一阶段**要做的东西落成可执行方案 —— 每个部分都写明**设计意图 + 技术建议 + 可扩展性 + 验收标准**。
>
> **本文件不是什么**：不是整个项目的 GDD。类型定位、美术管线、寻路手册等**仍以各自文档为准**。

| 项 | 值 |
|---|---|
| 版本 | v0.1（草案，待你确认标 ⚠️ 的假设） |
| 日期 | 2026-10-09；**2026-10-15 补写 §5.4 Boss（招式表 / 状态机 / 韧性条 / Animator 连线）** |
| 类型 | **3D 动作 RPG（ARPG）** —— 第三人称斜俯视 · 后置跟随（口径见 `GDD.md` §1） |
| 第一阶段目标 | **打通"中枢 → 战斗 → 掉落 → 成长 → 回中枢"的完整闭环** |
| 相关文档 | `GDD.md`（总策划）、`ROADMAP.md` §14（ARPG 扩展排期）、`ANIM-GUIDE.md`（手 K 动画）、`NAVMESH-GUIDE.md`（寻路）、`PROJECT-CONTEXT.md`（现状与坑） |

---

## 0. 口径对齐（与现有文档的关系）

> ⚠️ **先解决一个可能打架的地方**：`GDD.md` §9 目前写的是"**单一大型主场景**"。
> 本策划案新增了**酒馆场景**，因此准确表述应修正为：

| 场景 | 角色 | 说明 |
|---|---|---|
| `MainMenu.unity` | 开始界面 | 已有（标题 + 开始/退出） |
| **`Tavern.unity`** | **安全中枢（新增）** | 无战斗。NPC 交互、装备升级、法术学习、锻炼、接任务 |
| `Castle.unity` | **唯一战斗主场景**（城堡） | 已有，10-07 定稿；NavMesh 已烤 |

⇒ **"单一大型主场景"修正为"单一大型主战斗场景 + 一个安全中枢场景"**。本文件是酒馆场景的**权威来源**。

**新增系统的权威归属**：酒馆 / NPC 交互 / 掉落 / 双轨成长 / 法术 / 装备 / 任务 = **本文件**；
玩家基础动作、武器、魔力、敌人通用架构 = `GDD.md`（本文件只写**第一阶段增量**）。

---

## 1. 第一阶段范围

### 1.1 做什么（In Scope）

| # | 内容 | 现有基础 |
|---|---|---|
| 1 | 开始界面 → **酒馆** → **城堡** 的完整场景流程 | `MainMenu.unity` ✅ / 酒馆 ❌ / 城堡 ✅ |
| 2 | 玩家动作集：移动 / **跳跃** / 待机 / 奔跑 / **行走** / 死亡 / 四段攻击 | 缺跳跃、行走未接入 |
| 3 | 酒馆：**NPC 交互框架** + 4 类 NPC（装备升级 / 法术学习 / 锻炼 / 任务） | ❌ 全无 |
| 4 | 城堡：**两种小怪**（近战追逐 / 远程保持距离 + 倒退动作） | ✅ 已有原型（`Monster1`/`Monster2`） |
| 5 | **掉落**：怪物死亡掉 **魔晶** | ❌ 全无（`PlayerMana.Restore` 预留了接入点） |
| 6 | **双轨成长**：局内击杀升级 + 局外魔晶养成 | ❌ 全无 |
| 7 | **Boss**：初见动画 / 三种普攻 / 远距离突刺追击 / 移动·待机·死亡 | 模型已有（`Boss_Crichton`），逻辑 ❌ |
| 8 | 存档（永久成长 + 统计） | ❌ 全无（方案已定：JSON，见 `GDD.md` §11.2） |

### 1.2 明确不做（本阶段，避免范围膨胀）

- ❌ 多把武器 / 武器切换（长枪仍在额外项目）
- ❌ 装备的**掉落**（第一阶段装备只从 NPC 处"升级"，不掉新装备）
- ❌ 完整任务链与叙事对话（第一阶段只做"接取 → 完成 → 交付"的最小闭环）
- ❌ 技能树 / 天赋树（法术是**平铺列表**，不做前置依赖）
- ❌ 商店买卖 / 经济系统（魔晶只用于消耗型升级，不做交易）
- ❌ Boss 的瞬移版（第一阶段用**直线突刺**替代，见 §5.4）

### 1.3 完成定义（Definition of Done）

> 从开始界面点"开始游戏" → 进酒馆 → 用魔晶升 1 级装备、学 1 个法术、锻炼加 1 次数值、接 1 个任务
> → 进城堡 → 杀掉两种小怪各 3 只、捡到魔晶、触发至少 1 次升级 → 打 Boss（看到初见动画、被三种攻击各命中一次、被突刺追一次、击败）
> → 回酒馆，魔晶与等级**保留**。
> **全程无报错、无卡死、控制台 0 警告。**

---

## 2. 场景流程与跨场景数据 ★ 可扩展性核心

### 2.1 流程

```
[MainMenu] ──开始游戏──> [Tavern 酒馆·安全中枢]
                              │  ▲
                        出发下城│  │回城（胜利 / 主动撤退）
                              ▼  │
                     [Castle 城堡·战斗]
                              │
                         玩家死亡 ──> [失败结算] ──> 回酒馆（保留已获得的魔晶？见待定项 A）
```

### 2.2 ⚠️ 跨场景数据的落点（最容易做错的地方）

**问题**：酒馆里升的数值、攒的魔晶，跨场景必须还在。Unity 换场景会**销毁所有普通物体**，所以"挂在玩家身上的脚本"承载不了永久数据。

**建议：三层分离（这是本阶段最重要的架构决定）**

| 层 | 类 | 生命周期 | 职责 |
|---|---|---|---|
| **持久数据层** | `PlayerProgress`（**纯 C# 类**，可 `JsonUtility` 序列化） | 永久 | 魔晶数量、装备等级、已学法术 id、锻炼次数、任务状态、统计。**只有它进存档** |
| **服务层** | `ProgressService`（`MonoBehaviour` + `DontDestroyOnLoad` 单例） | 跨场景 | 唯一读写 `PlayerProgress` 的入口；负责存/读盘、变更广播 |
| **运行时层** | `PlayerStats`（挂在玩家预制体上） | 单场景 | 战斗内数值 = 由 `ProgressService.Current` **派生** + 局内临时加成 |

> ⚠️ **不要**把永久数据放在场景物体上，**也不要**每帧去读盘。

### 2.3 技术建议

| # | 建议 | 理由 / 现状 |
|---|---|---|
| 1 | 新增 `Core/GameFlow.cs`：`SceneId` 枚举（`MainMenu` / `Tavern` / `Castle`）+ `Load(SceneId)` | 现在 `GameManager.RestartRun()` 直接写死 `SceneManager.LoadScene(SceneManager.GetActiveScene().name)`，加场景会散落魔法字符串 |
| 2 | 场景切换用**同步** `SceneManager.LoadScene`（先不引 Addressables） | 工程没有 Addressables；第一阶段场景小，同步加载够用。**但把加载收口到 `GameFlow`**，将来换异步只改一处 |
| 3 | `GameManager` 保持"**战斗场景的结算器**"，不要扩成全局管理器 | 它现在订阅 `GameEvents.PlayerDied/BossDied`、管结算面板；职责边界清晰就别动（详见 §2.4） |
| 4 | 跨场景事件用 `GameEvents`（静态门面）+ 订阅方 `OnEnable/OnDisable` 成对 | 现有约定，见 `Core/GameEvents.cs` 注释；⚠️ 静态事件跨场景**必须成对退订**（`EnemyHealth.Died` 的 T7 就是这类隐患） |
| 5 | 进入战斗场景时 `ProgressService` 把 `PlayerProgress` **快照**给玩家；离开时**回收** | 避免"战斗内修改污染存档"，也让"玩家死亡是否保留魔晶"变成一个显式开关（见待定项 A） |

### 2.4 可扩展性

- 加第 3 个场景（如"第二个城堡"）只需：`SceneId` 加一项 + 场景文件 + `GameFlow` 一行配置 → **不改任何调用方**。
- 想加"读取中"界面：在 `GameFlow.Load` 内层换成异步 + 加载界面，**其余代码零改动**。

---

## 3. 玩家角色（动作集）

### 3.1 动作清单与现状

| 动作 | 现状 | 第一阶段要做的 |
|---|---|---|
| 移动（八向） | ✅ `PlayerMotor`（相对相机、唯一位置写入者） | — |
| 待机 Idle | ✅ | — |
| **行走 Walk** | ⚠️ **动画有、未接入**（`OVR - Roskva_SwordInHand.fbx` 的 `Walk`，2.47 s） | 接入 + 速度分档 |
| 奔跑 Run | ✅ | — |
| **跳跃 Jump** | ❌ **零实现** | 新增（⚠️ 有 4 个前置，见 §3.3） |
| 死亡 Death | ✅ 动画有 | 复核与死亡链路（**T8**） |
| 四段攻击 | ✅ `combo_01_1~4` + `OnAttackHit` 关键帧判定 | — |

> ⚠️ **行走 vs 奔跑**：不是两套输入，而是**同一个移动键的速度分档**。
> 建议：`Speed` 连续参数 + `MoveSpeed` 阈值切换 `Walk`(慢) / `Run`(快) 动画；
> 第一阶段简单做法 —— **Shift 切换档位**（`Sprint` 动作在 `.inputactions` 里**已存在**，可直接用）。

### 3.2 玩家状态机

现有 `PlayerState` 枚举 = `Idle, Run, Dash, Attack, Special, Hit, Death`（七状态）。

**第一阶段建议扩为（新增 3 个）**：

```
Idle, Walk, Run, Dash, Jump, Fall, Land, Attack, Special, Hit, Death
                 └──────── 新增 ────────┘
```

⚠️ 加状态时**必须同步三处**（`PlayerState.cs` 注释里写死了这条）：
① `PlayerFSM.Awake` 的三张字典表项；② `PlayerAC.controller` 的状态与转场；③ 状态枚举本身。

### 3.3 跳跃的 4 个前置（**必须先清，否则做了也跑不起来**）

| # | 前置 | 现状（已实测） |
|---|---|---|
| 1 | **修 T1** | `PlayerMotor` 用 `enabled = false` 停移动会**把重力一起停掉** —— 跳跃期间角色会"悬空不掉"。必须拆分"移动抑制"与"重力常驻" |
| 2 | **补 `Jump` 输入** | `.inputactions` 的 Player map **确实没有 `Jump`**（现有仅 Move/Look/Attack/Interact/Crouch/Previous/Next/Sprint/Dash/Special/Scroll）→ 加 Action + 重新生成 `PlayerControls.cs` + `InputService` 暴露访问器 + 输入缓冲 |
| 3 | **补动画** | 需要 `Jump` / `Fall` / `Land` 三组（Humanoid 可重定向，见 `ANIM-GUIDE.md`） |
| 4 | **垂直碰撞** | 城堡是整图，台阶/城墙高差要实测；`CharacterController` 的 `slopeLimit` / `stepOffset` 要调 |

### 3.4 技术建议

| # | 建议 | 理由 |
|---|---|---|
| 1 | 跳跃**仍走 `PlayerMotor`**，禁止任何地方直接写 `transform.position` | 项目铁律：「位置只有一个写入者」（`PlayerDash` 曾因绕过 CC 而**穿墙**，见 T27） |
| 2 | 垂直速度独立成 `PlayerMotor._verticalVelocity`，与水平位移解耦 | 这样"停移动"只抑制水平，重力照常 —— **这就是 T1 的正确修法** |
| 3 | 用 `InputService` 的**输入缓冲**模式接跳跃（`HasBufferedJump` / `ConsumeJump`） | 现有 3 个动作（攻击/冲刺/特攻）都是这个模式，保持一致；跳跃尤其需要"落地前一帧按键也生效" |
| 4 | `Land` 用**事件**而非时长（动画末尾挂事件回到 `Idle`） | 与敌人出手帧的做法一致（`TickAttack`），比写死时长稳 |

### 3.5 可扩展性

- 想加**翻滚 / 格挡 / 蓄力重击**：加状态 + clip + 字典表项，**架构不动**。
- 想加**二段跳**：`JumpCount` 计数即可，`PlayerState` 不用改（仍是 `Jump`）。

---

## 4. 中枢：酒馆场景

### 4.1 场景构成

```
Tavern.unity
├── 环境（室内：吧台 / 桌椅 / 壁炉 / 门口）
├── 玩家出生点（SpawnPoint）
├── NPC ×4（见 §4.3）
├── UI Canvas
│   ├── 魔晶计数（常驻）
│   ├── 交互提示（"按 F 交谈"）
│   └── 各 NPC 的功能面板（4 个，按需打开）
└── 出城触发器（走到门口 → 确认 → 进城堡）
```

> ⚠️ **美术来源**：酒馆是**新场景**，工程内**没有室内资源**。
> **建议**：优先复用 `LeartesStudios/SiegeOfPonthus` 里已有的建筑/道具件搭**白盒 + 材质**，
> 不要为第一阶段新建模（详见 §11 风险表）。

### 4.2 NPC 交互框架 ★ 可扩展性核心

**目标**：以后加第 5 个 NPC（比如铁匠、赌徒）应该**只写一个类**，不改框架。

**建议的接口设计**

```csharp
// 所有可交互物的统一契约
public interface IInteractable
{
    string Prompt { get; }        // 提示文案："按 F 交谈"
    bool CanInteract { get; }     // 不满足条件时置灰（如魔晶不够）
    void Interact(GameObject actor);
}
```

**配套组件**

| 组件 | 职责 | 技术建议 |
|---|---|---|
| `InteractionDetector`（挂玩家） | 每帧找最近的 `IInteractable` | 用**触发器**（`OnTriggerEnter/Exit` 收集候选）而不是每帧 `OverlapSphere`；候选按距离排序取最近 |
| `InteractPromptUI`（挂 Canvas） | 显示 `Prompt` | 订阅 `InteractionDetector` 的"目标变化"事件，别每帧刷 Text（避免 GC） |
| `NpcInteractable`（抽象基类，挂 NPC） | 实现 `IInteractable`，`Interact` 里打开自己的面板 | **抽象基类 + 4 个子类**：`UpgradeNpc` / `SpellNpc` / `TrainingNpc` / `QuestNpc` |
| `NpcPanelBase`（UI 基类） | 面板开/关 + 暂停输入 | ⚠️ **复用现有暂停套路**：`InputService.SetGameplayInputEnabled(false)` + `Time.timeScale = 0`（只做后者拦不住鼠标，这是踩过的坑） |

> ⭐ **`Interact` 输入已经存在**！`.inputactions` 的 Player map 里有 `Interact`，只是 `InputService` **还没暴露**。
> 建议加 `InteractPressedThisFrame`（并可考虑走输入缓冲），成本几乎为零。

### 4.3 四类 NPC 的功能设计

#### 4.3.1 装备升级 NPC

| 项 | 设计 |
|---|---|
| 玩法 | 消耗**魔晶**提升装备等级；等级 → 属性（攻击力 / 生命上限 / 魔力上限） |
| 第一阶段装备 | **固定几件**（如"剑""护甲""法戒"），不做掉落、不做替换 |
| ⚠️ 关键 | 升级的是**装备等级**（存进 `PlayerProgress`），不是"换一件新装备" |

**技术建议**

```
EquipmentConfig (SO)     : 装备定义（id / 名称 / 图标 / 每级加成的曲线）
EquipmentUpgradeEntry    : 运行时数据（equipmentId + level）—— 进存档
EquipmentService         : 查询/升级的唯一入口；升级 = 查曲线 → 累加属性 → 存盘
```

- 每级加成**用曲线（`AnimationCurve` 或 `float[]`）而不是公式硬编码** → 数值调整不用改代码。
- 升级消耗**也要可配置**（`costCurve`），不要写 `cost = level * 100`。

**可扩展性**：将来加"装备掉落"，只需新增 `EquipmentDrop`（产出 `EquipmentConfig` 引用）+ 背包 id 列表，**服务层不动**。

#### 4.3.2 法术学习 NPC

| 项 | 设计 |
|---|---|
| 玩法 | 消耗魔晶**解锁**法术；已学法术可装备到"法术槽"（第一阶可只留 1 槽） |
| 与现有代码的关系 | ⚠️ **现有"特殊攻击"是挂在武器上的**（`WeaponConfig.specialAttackType` / `specialManaCost`，实现是 `Fireball`）。第一阶段要**把它抽出来变成独立法术** |

**技术建议**

```
SpellConfig (SO)   : id / 名称 / 图标 / 消耗魔力 / 冷却 / 前摇 / 预制体 / 伤害 / 判定形状
SpellInventory     : 已学 id 列表（List<string>，⚠️ JsonUtility 不支持 Dictionary）+ 已装备槽
SpellCaster (组件) : 施法状态机（前摇 → 出手 → 后摇 → 冷却），出口仍走 PlayerMana.TrySpendSpecial
```

- ⚠️ **保留 `PlayerMana.TrySpendSpecial` 作为唯一魔力出口**（现有铁律：动作不得自行扣魔力）。
- 弹道复用现成件：`Fireball`（爆炸 AOE）/ `Bullet`（单体、只打玩家）—— 新的投射法术**不用新写弹道**。
- 出手沿用**动画事件**（`SpecialStateBehaviour` 已有 `OnSpecialCast` 一类的钩子）—— 与"出手帧靠事件不靠计时器"的既定口径一致。

**可扩展性**：加一个新法术 = 新建一个 `SpellConfig` 资产 + 一个预制体，**零代码**（除非是全新行为类型，那才需要新的 `SpellBehaviour` 子类）。

#### 4.3.3 锻炼 NPC（提升数值）

| 项 | 设计 |
|---|---|
| 玩法 | 消耗魔晶**永久**提升基础数值（生命 / 攻击力 / 魔力），可**分项**、**有次数上限** |
| 与"装备"的区别 | 装备 = **有等级的物件**（将来可换）；锻炼 = **直接加在角色身上**的永久加成 |

**技术建议**：两者**共用同一套属性管线**（见 §5.3 的 `StatBlock` / `StatModifier`），
锻炼只是"往修正器列表里加一条 `Permanent` 来源的修正"。⚠️ **不要**为锻炼单独写一套加属性代码。

#### 4.3.4 任务 NPC

| 项 | 设计 |
|---|---|
| 第一阶段任务类型 | **只做 2 种**：① 击杀 N 只某类怪；② 击败 Boss |
| 玩法 | 接取 → 去城堡完成 → 回酒馆交付 → 得奖励（魔晶 / 装备等级 / 解锁法术） |
| ⚠️ 注意 | 任务进度**必须进存档**（否则关掉游戏就没了） |

**技术建议**

```
QuestConfig (SO)   : id / 标题 / 描述 / 目标类型 / 目标数量 / 奖励列表
QuestProgress      : 运行时（questId + 状态 + 当前进度）—— 进存档
QuestService       : 订阅 GameEvents（怪物死亡 / Boss 死亡）→ 推进进度 → 广播"任务可交付"
```

- ⚠️ **进度推进必须订阅事件，不要让战斗代码直接调任务系统** —— 否则城堡会依赖酒馆的逻辑，将来拆场景就崩。
- 目标类型用**枚举 + 参数**（`KillCount{ enemyId }` / `KillBoss`），别为每种任务写一个类。

**可扩展性**：加"收集 N 个魔晶""限时通关"等新目标类型 → 加枚举项 + 在 `QuestService` 加一个 case。

### 4.4 验收

- [ ] 走到 NPC 前出现提示；按 F 打开面板；关闭后操作恢复正常（**鼠标能转视角、时间恢复**）
- [ ] 魔晶不足时按钮置灰且给出原因
- [ ] 4 个 NPC 的功能均能改到 `PlayerProgress` 并**存盘**
- [ ] 退出 Play 再进来，改动仍在

---

## 5. 战斗场景：城堡

### 5.1 地图与区域

**复用现有** `Assets/Scenes/Castle.unity`（城堡整图，3672 预制体实例，NavMesh 已烤到 `URP_SiegeOfPonthus/NavMesh-NavMeshPoot.asset`）。

| 项 | 设计 |
|---|---|
| 区域划分 | 用现有 `RoomController` + `RoomTrigger` + `DoorController`（"进入 → 锁门 → 清怪 → 开门"）**原样复用**，语义从"房间"改叫"战斗区域" |
| 区域数量（第一阶段） | **建议 3 个**：① 教学区（近战怪 ×2）② 混编区（近战 ×2 + 远程 ×2）③ Boss 区 |
| 敌人生成 | ⚠️ 现有敌人是**场景内预摆放**（`SetActive` 管理），没有运行时生成。第一阶段**建议保持预摆放**，不做刷怪器（省事且可控） |

**技术建议**：区域难度/敌人配置**不要写死在 `RoomController` 里** —— 建议加一个 `EncounterConfig`（SO）描述"本区域激活哪些敌人"，`RoomController` 只读配置。这样调难度不用动场景。

### 5.2 小怪 ×2

| | 近战怪（`Monster1`） | 远程怪（`Monster2`） |
|---|---|---|
| 行为 | **追逐玩家** → 进入攻击距离出手 | **保持距离**（太近后撤 / 太远靠近）→ 舒适区施法 |
| 状态机 | `Idle / Alert / Chase / Attack / Reposition / Hit / Death` | `Idle / Alert / Chase / Attack / Hit / Death`（**无 Reposition**，"保持距离"折进 `Chase`） |
| 现状 | ✅ 已实现（含 `Reposition`） | ✅ 已实现（含 kiting） |
| ⭐ 本阶段新增 | 动画补齐 + 数值定稿 | **"保持距离时用倒退动作"** |

#### ⭐ 关于"保持距离时用倒退动作"

**好消息：逻辑已经是对的。** `EnemyCaster` 的后撤走的是 `EnemyLocomotion.BackstepTo()`，
它的特点就是**把朝向攥在自己手里**（全程 `agent.updateRotation = false`），所以观感是**倒退着走**而不是"转过身背对玩家走开"。
实测（Play 模式手动步进 20 fps）：距离 2.50 → 4.19 m 持续拉开，**朝向偏角全程 0.0°**。

**缺的只是动画**：现在后撤时播的还是"原地飞行"（`flyinplace`）。第一阶段要补一个**倒退动作 clip**。

| 技术建议 | 说明 |
|---|---|
| 新增动画参数 | `SpeedZ`（Float，**带符号**）= 相对朝向的前向速度。> 0 播前进、< 0 播倒退 |
| 或复用 `Speed` + 一个 `Backstep`（Bool） | ⚠️ 前者更通用，后者更省事 —— 建议前者（将来能自然扩展到"侧移"） |
| ⚠️ 不建议"把 Walk 倒放" | `Animator.speed` 设负值虽可行，但**倒退的动作幅度**和倒放不是一回事，观感会怪；且 Wraith 是**悬浮怪**，它的"倒退"应是**向后漂浮** |
| 敌人状态机不动 | 只是 `EnemyLocomotion` 多喂一个参数给 Animator |

#### ⚠️ 数据/代码现状提醒

- 敌人用 **Humanoid**（`WraithAvatar`）；动画从 `Polygonmaker/Monsters Series/.../Animations/Anim_Wraith@*.FBX` 提成 `.anim`。
- **出手帧靠动画事件**（`TickAttack`），漏挂的后果是**静默的**（不触发 → 判定体不开 → 打不出伤害）。
- 数值资产已分开：`EnemyAIConfig.asset`（近战）/ `EnemyCasterConfig.asset`（远程）。

### 5.3 掉落与双轨成长 ★ 本阶段最关键的数值设计

#### 5.3.1 ⚠️ 先明确一个必须你拍板的设计问题

你说的两件事**同时存在**，必须区分清楚，否则数值会失控：

| 轨 | 触发 | 作用范围 | 建议 |
|---|---|---|---|
| **A. 局内升级**（击杀怪物直接升级，提升基础数值） | 击杀给经验 → 满则升级 | **本次下城有效** | ⚠️ **回酒馆后重置为 Lv1** |
| **B. 局外养成**（魔晶 → 装备升级 / 法术 / 锻炼） | 在酒馆消耗魔晶 | **永久，进存档** | 跨战斗累积 |

> ⚠️ **假设**：A 是**临时的**（类似 Roguelite 的局内成长），B 是**永久的**。
> 理由：两条都永久 ⇒ 玩家刷几轮后数值爆炸，Boss 会瞬间失去意义。
> **若你要 A 也永久**，请明确 —— 那需要重新设计经验曲线并砍掉 B 的一部分，否则两条线会互相稀释。
> 📌 见 §13 待定项 A。

#### 5.3.2 掉落

| 项 | 设计 |
|---|---|
| 掉落物 | **魔晶**（唯一素材，第一阶段不做多币种） |
| 数量 | 小怪 1~3 个 / Boss 15~25 个（**概率 + 区间都进配置**） |
| 拾取 | 走近自动拾取（**不需要按键**，减少操作负担） |

**技术建议**

```
LootTable (SO)  : 条目列表 { itemId, 权重, 数量区间 }  —— 一个 LootTable 可被多种怪复用
LootSpawner     : 挂在敌人预制体上；订阅自己的 EnemyHealth.Died → 按 LootTable 掷点 → 生成 Pickup
Pickup (组件)   : 弹跳 + 磁吸（靠近玩家时加速飞过来）+ OnTriggerEnter 入账
```

| # | 建议 | 理由 |
|---|---|---|
| 1 | **入口用 `EnemyHealth.Died`**（已有 `static event Action<EnemyHealth>`） | 不用改 `EnemyHealth` 的死亡流程，挂个组件订阅即可 |
| 2 | ⚠️ **`Died` 是 static 事件**（T7：跨场景重开有重复订阅风险）→ `LootSpawner` 必须 `OnEnable/OnDisable` **成对**订阅 | 现有 `RoomController` 就是这么兜的 |
| 3 | 掉落物**走 `ObjectPool`**（已有） | 大量小怪同时死亡时避免 GC 峰值 |
| 4 | 掉落**不进存档**（场景内的临时物件） | 只有"入账后的魔晶数量"进存档 |
| 5 | ⭐ 顺手把 `PlayerMana.Restore()` **接上** | 该入口**当前无任何调用方**，是预留好的接入点。让"魔力结晶"成为一种掉落物，既补上魔力回复，也验证了掉落管线 |

#### 5.3.3 属性管线（**双轨成长的地基**）

⚠️ **不要**为"等级加成""装备加成""锻炼加成"各写一段加属性的代码。**一套修正器管线**全部覆盖：

```csharp
public enum StatType { MaxHp, MaxMana, Attack, Defense, MoveSpeed, CritChance }

public enum ModifierSource { Base, Level, Equipment, Training, Buff }

[System.Serializable]
public class StatModifier          // ⚠️ 必须是普通可序列化类，JsonUtility 不支持多态
{
    public StatType type;
    public ModifierSource source;
    public float value;            // 加法 / 乘法由 op 决定
    public bool isPercent;         // false = 加法，true = 百分比
}
```

- **最终值 = 基础值 → 累加所有加法修正 → 乘以 (1 + 所有百分比之和)**
- `PlayerStats` 里维护一个 `List<StatModifier>`；任何来源只管"加/删自己那几条"，**不直接改最终值**。
- ⚠️ `JsonUtility` **不支持 `Dictionary`**、不支持多态 → 用 `List<StatModifier>`；`StatType` 用枚举而不是字符串。

**可扩展性**（这一段是"充分可扩展"的关键）：
- 加"暴击" → `StatType` 加一项 + 计算处加一行。
- 加"临时 buff 药水" → `ModifierSource.Buff` + 一个到期移除的计时器，**其余不动**。
- 加"套装效果" → 依然是往列表里加修正。
- 报告里可以写："**属性系统采用修正器（Modifier）管线，新增成长来源不侵入战斗代码**" —— 这是很好的设计亮点。

#### 5.3.4 局内升级

```
ExpCurve (SO)   : 每级所需经验（用 float[] 或 AnimationCurve，别写公式）
PlayerLevel     : 运行时等级/经验；订阅 GameEvents.EnemyKilled → 加经验 → 满则升级 → 重算数值
```

- ⚠️ 升级必须**重算 `PlayerStats` 并广播**（HUD 生命条要立刻变长），
  建议走事件（`GameEvents.PlayerLevelUp`）而不是让 HUD 每帧轮询。

### 5.4 Boss

**模型已就位**：`Assets/_Game/Prefabs/Enemy/Boss_Crichton.prefab`（独立预制体，4.47 m，Humanoid，
`Boss_CrichtonAvatar` 有效；⚠️ 剑的绑定骨 `Bip001-Prop1` 已重定向到 `Bip001-R-Hand`）。

#### 5.4.1 需求 → 设计映射

| 需求（✅ 2026-10-15 定稿） | 设计 | 备注 |
|---|---|---|
| 战斗范围 / 攻击范围 | 直接复用 `EnemyAIConfig.aggroRange` / `attackRange`（**水平距离**判定） | ⚠️ Boss 4.47 m 高，必须用水平距离；用 3D 距离会让"攻击范围"永远判不中 |
| 未进战斗待机 | `NIdle`（动画 `N_idle`） | 玩家进入战斗范围即转 `CombatIdle` |
| 靠近玩家 | `Chase`（动画 `walkfront`），走 `EnemyLocomotion.ChaseTarget` | — |
| 三段普通攻击 | `Attack`：**a / b / c 带权随机**（`attackWeightA/B/C`） | 出手帧用动画事件 `TickAttack()` 开**当前招式**对应的判定体 |
| **b → c 二连段** | 选中 b 时按 `comboChance` 概率接一段 c | ⚠️ `AttackC` 触发器被"单出 c"与"二连第二段"**共用**；接 c 前必须复位 `AttackB` 残留触发器，否则 `hit` 播完会"诈尸"补挥 |
| 攻击 CD（时长随机） | `attackCdMin` ~ `attackCdMax` 随机；⭐ **进入动画第一帧就开始计时** | 计时挂在 `OnAIUpdate`（**与状态无关**）⇒ 破韧打断也不会漏掉 CD |
| CD 期间的行为 | **始终面向玩家 + `walkleft` 侧移绕圈**（`strafeRadius`，方向可随机） | 用 `EnemyLocomotion.BackstepTo` 而非 `MoveToOrStep`，否则会背对玩家走开 |
| 特殊攻击（**双 CD 归零**才触发） | `Special`：近距 → **sa1**（`SpecialNear`，原地不移动）；远距 → **sa2**（`SpecialFar`，先追后打） | ⭐ **优先级必须高于普通攻击**，否则普攻会在 CD 归零那一帧抢走窗口 |
| sa2 的"追击 + 攻击"在同一动画里 | 追击段位移由**代码**在 `specialApproachTime` 内推进 | ⚠️ 本工程**根运动全局关闭** ⇒ 做这条动画时**追击段只做姿态、不要放位移**，否则动画位移 + 代码位移叠加，Boss 会冲过玩家 |
| **韧性条（破韧）** | 受击累积削韧 = 伤害 × `poisePerDamageMultiplier`（默认 **2**）→ 满则 `Hit` 硬直**打断一切** | 破韧后 `poiseBreakImmunityTime`（0.6 s）免疫；韧性自然回复默认关闭 |
| 脱战 | 离开战斗范围超过 `disengageTime` → `NIdle`，并清空双 CD 与韧性 | 不补这条的话 Boss 会在 `walkleft` 里绕着空气转 |
| 死亡 | `Death`（`IsDead = true` + 锁死状态机）→ `GameEvents.RaiseBossDied()` | ⚠️ 要**延迟隐藏**（复用 `EnemyHealth._deathHideDelay` 的做法） |
| ⏸️ 初见动画 / 2 阶段 | **后续**，V1 不做 | 相机接管仍要自己写轻量 `CameraCue`（工程**无 Cinemachine**） |

#### 5.4.2 状态机（✅ 2026-10-15 已实现；复用现有基类，**不要新写一套**）

✅ **已落地**：`Assets/_Game/Scripts/Gameplay/Enemy/BossController.cs`（7 状态，`EnemyStateMachine<EState>` 表驱动）
＋ `Assets/_Game/Scripts/Data/BossConfig.cs`（`BossConfig : EnemyAIConfig`）。与 `GDD.md` §7.3 是同一份口径。

```
NIdle      ──玩家进战斗范围────────────────> CombatIdle
CombatIdle ──双 CD 归零────────────────────> Special（优先级最高）
           ──普攻 CD 就绪 & 在攻击范围──────> Attack（a / b / c 带权随机）
           ──普攻 CD 就绪 & 不在攻击范围────> Chase
           ──普攻 CD 未就绪────────────────> 原地绕玩家侧移（walkleft，始终面向玩家）
           ──离开战斗范围 > disengageTime──> NIdle（清双 CD 与韧性）
Chase      ──双 CD 归零────────────────────> Special
           ──进攻击范围────────────────────> Attack（CD 未就绪则回 CombatIdle）
           ──否则──────────────────────────> walkfront 正面接近
Attack     ──动画播完──────────────────────> CombatIdle（b 命中 comboChance 则先接 c）
Special    ──动画播完──────────────────────> CombatIdle
Hit        ──动画播完──────────────────────> CombatIdle（不在战斗范围则 NIdle）
任意        ──韧性条削满───────────────────> Hit（打断一切）──> CombatIdle
任意        ──HP ≤ 0───────────────────────> Death（锁死状态机）──> GameEvents.RaiseBossDied()
```

**技术建议**

| # | 建议 | 理由 |
|---|---|---|
| 1 | 继承 **`EnemyStateMachine<TState>`**（纯 C# 表驱动基类，M3.2 已落地） | 加第四次"状态集合 + 转移条件 + 数值"即可；报告里是"架构红利"的实证 |
| 2 | 复用 **`EnemyAIController`**（非泛型共用层）+ **`EnemyLocomotion`**（移动分层） | 别再写一遍寻路/受体/感知 |
| 3 | 🔄 **Boss 平时不被打断，但韧性条削满会破韧**：`canBeInterrupted` 仍设 **false**（普通受击只闪白），另订阅 `EnemyHealth.Damaged` 累积削韧 | ⚠️ **2026-10-15 口径变更**：原为"Boss 只闪白、不引入硬直条" → 现引入**韧性条（破韧）**，见 GDD §6.1 / §7.3 |
| 4 | **初见动画的相机接管**：⚠️ 工程**未安装 Cinemachine** | 建议自己写一个轻量 `CameraCue`（拿 `CameraFollow` 的引用 → 临时切到"看向 Boss 的机位" → 播完切回），**不要**为一段动画引入新包 |
| 5 | 招式出手**用动画事件**（同 `TickAttack`） | 与既有口径一致；前摇 → 事件开判定体 → 后摇 |
| 6 | **判定体复用 `Hitbox` / `HitboxController`**（敌我共用） | GDD 设计支柱第 4 条"敌我同规则" |
| 7 | Boss 招式动画：⚠️ **`Boss_Crichton` 的 FBX 内 0 动画** | 需手 K 或重定向。⚠️ 用重定向时**剑必须跟着手走**（Prop1 已在右手下，但招式动画的骨骼曲线要覆盖到剑） |
| 8 | 招式数值进 **`BossConfig`（SO）** ✅ **已建**：`Assets/_Game/Scripts/Data/BossConfig.cs` | 字段清单见 §5.4.4 |

#### 5.4.3 初见演出的实现建议（⚠️ 最容易翻车的一处）

- ⚠️ **演出期间必须处理输入**：建议**保留相机接管 + 锁玩家输入**（`InputService.SetGameplayInputEnabled(false)`），
  而不是单纯 `Time.timeScale = 0`（那只冻逻辑，拦不住鼠标 —— 踩过的坑）。
- ⚠️ **演出必须可跳过**（按任意键 / ESC），否则重复挑战时会很烦。
- ⚠️ **演出结束后要保证状态干净**：输入恢复、`timeScale` 还原、相机回位。建议把这三件事写在**同一个** `EndIntro()` 里（幂等）。

#### 5.4.4 数值配置与 Animator 连线（✅ 2026-10-15 已实现）

⚠️ **所有数字都在 SO 里填，代码里不许出现具体数值**（§6 的既定口径）。下表列出的是**字段清单**，默认值只是占位。

**`BossConfig : EnemyAIConfig` 字段**

| 分组 | 字段 | 含义 |
|---|---|---|
| 继承 | `hp` / `aggroRange` / `attackRange` / `moveSpeed` / `damage` / `flashTime` | 血量 / **战斗范围** / **攻击范围** / 追击速度 / 伤害 / 闪白时长 |
| 继承 | `canBeInterrupted` | ⚠️ **必须 false** —— Boss 的打断只走韧性条，不走普通受击 |
| 脱战 | `disengageTime` | 离开战斗范围多久回 `N_idle`（0 = 永不脱战） |
| 移动 | `turnSpeed` / `strafeRadius` / `randomStrafeDirection` | 转身速度（建议 240，别用默认 720）/ 绕圈半径（0 = 不绕）/ 绕圈方向是否随机 |
| 普攻 CD | `attackCdMin` / `attackCdMax` / `attackTimeout` | CD 随机区间 / 兜底超时（建议 = 最长攻击动画 + 0.5） |
| 三选一 | `attackWeightA` / `attackWeightB` / `attackWeightC` / `attackHitboxActiveTime` | 三段权重（全 0 退化为等概率）/ 判定体开启时长 |
| 二连段 | `comboChance` / `comboHitboxActiveTime` | 接 c 的概率 / 第二段判定体时长 |
| 特殊攻击 | `specialCd` / `specialTimeout` / `specialNearMaxDistance` / `specialFarStopDistance` / `specialApproachTime` / `specialApproachSpeed` / `specialKeepFacingPlayer` / `specialHitboxActiveTime` | 特殊 CD / 兜底 / 近远分界 / sa2 停靠距离 / **sa2 追击时长** / **sa2 追击速度** / 追击时是否面朝玩家 / 判定体时长 |
| 韧性 | `maxPoise` / `poisePerDamageMultiplier` / `poiseRecoverPerSecond` / `poiseRecoverDelay` / `hitTimeout` / `poiseBreakImmunityTime` | 韧性上限 / **削韧倍率（默认 2）** / 自然回复速度（默认 0） / 回复延迟 / 硬直兜底超时 / **破韧免疫期** |
| 调试 | `drawGizmos` | Scene 视图画战斗 / 攻击 / 特殊攻击距离圈 |

**韧性条 UI**：订阅 `BossController.PoiseChanged(cur, max)` 事件，或直接读 `BossController.Poise01`（0..1）。

**Animator 参数（10 个，逐字与代码一致）**：`Speed`(Float) / `Strafe`(Bool) / `InCombat`(Bool) / `AttackA`(Trigger) / `AttackB`(Trigger) / `AttackC`(Trigger) / `SpecialNear`(Trigger) / `SpecialFar`(Trigger) / `Hit`(Trigger) / `IsDead`(Bool)。

> ⚠️ 基类的 `PlayHitAnim` / `PlayDeathAnim` 引用的是名为 `Attack` 的参数，Boss 用不上，**不要调它们**。
> ⚠️ 每段攻击 / 特殊攻击动画的**命中帧必须挂动画事件 `TickAttack`**（无参）—— 漏挂是**静默**的：打不出伤害但不报错。可选在 b 的后摇起点挂 `OnActionEnd` 让二连段提前接上。

**Animator 状态机连线**（9 个顶层状态，其中 `Locomotion` 是含 3 个状态的子状态机）

| 状态 | 动画 | 循环 |
|---|---|---|
| `N_idle` | N_idle | ✅ |
| `Locomotion`（子状态机）→ `CombatIdle` / `WalkFront` / `WalkLeft` | 战斗待机 / walkfront / walkleft | ✅ |
| `AttackA` / `AttackB` / `AttackC` | a / b / c | ❌ |
| `SpecialNear` / `SpecialFar` | sa1 / sa2 | ❌ |
| `Hit` | hit | ❌ |
| `Death` | 死亡动画 | ❌ |

| 从 → 到 | 条件 | Has Exit Time | Duration |
|---|---|---|---|
| `Entry → CombatIdle` | — | 关 | — |
| `CombatIdle → WalkFront` | `Speed > 0.1` 且 `Strafe == false` | 关 | 0.15 |
| `CombatIdle → WalkLeft` | `Speed > 0.1` 且 `Strafe == true` | 关 | 0.15 |
| `WalkFront → WalkLeft` / `WalkLeft → WalkFront` | `Strafe` 取反 | 关 | 0.15 |
| `WalkFront / WalkLeft → CombatIdle` | `Speed < 0.1` | 关 | 0.15 |
| `N_idle → Locomotion` | `InCombat == true` | 关 | 0.25 |
| `Locomotion → N_idle` | `InCombat == false` | 关 | 0.25 |
| `AnyState → AttackA / AttackB / AttackC` | 各自的 `Attack*` 触发器 | 关 | 0.1 |
| `AnyState → SpecialNear / SpecialFar` | 各自的触发器 | 关 | 0.1 |
| `AnyState → Hit` | `Hit` 触发器 | 关 | **0.05**（破韧要快） |
| `AnyState → Death` | `IsDead == true` | 关 | 0.1 |
| `AttackA / B / C`、`Special*`、`Hit` → `Locomotion` | — | **开，Exit Time 0.9~0.95** | 0.15 |
| `AttackB → AttackC` | `AttackC` 触发器 | 关 | 0.1 |

> ⚠️ **`AttackB → AttackC` 这条必须排在 `AttackB → Locomotion` 上面** —— Unity 按列表**从上到下**取第一条满足条件的转移，排反了二连段永远接不出来。
> ⚠️ 所有 **Any State** 转移都要**取消勾选 `Can Transition To Self`**，否则动作播到一半会自我重入、从头开始（看起来像抽搐）。
> ⚠️ 动作 → `Locomotion` 的 Exit Time **别设到 0.6 以下**：脚本判"动作结束"是 `normalizedTime >= 1`，设太低会出现"脚本还在攻击状态、动画已经回移动"的错位感。
> 📌 `Hit` 的 `Interruption Source` 建议设为 **Current State**，让破韧能打断正在播的动作。

**预制体还要补**（`Boss_Crichton.prefab` 目前**只有 `Animator`**）

1. `EnemyHealth` —— `_config` 指向 **BossConfig**（血量从这里读）
2. `BossController` —— `_config` 指向**同一个** BossConfig
3. `HitboxController`
4. 4 个判定体子物体：`Hitbox_AttackA` / `Hitbox_AttackB` / `Hitbox_AttackC` / `Hitbox_Special`，各挂 `Hitbox`（`_team = Enemy`、`_targetLayers` 勾 Player 层 = 8）。**建议挂在武器挂点 `Bip001-Prop1` 下**，判定盒才会跟着剑走
5. ⚠️ **`CapsuleCollider` + `NavMeshAgent`** —— 两样现在都没有，`EnemyLocomotion` 会退化成 transform 直线，Boss 会**穿墙**

> ⚠️ 韧性条属于**新增战斗机制**，落地时同步改了 `GDD.md` §6.1 / §7.3 / §7.4 / §15.2（原文写的是"不引入硬直条 / 破防"）。

### 5.5 验收

- [ ] 两种小怪行为符合设计（近战追、远程保持距离且**倒退**）
- [ ] 击杀必掉魔晶，拾取后 UI 计数变化
- [ ] 局内升级触发时生命/攻击**立刻**变化
- [ ] Boss：进战斗范围转战斗待机 → `walkfront` 靠近 → **a / b / c 三段**各能观测到 → b 有一定概率接出 **c 二连**
- [ ] Boss：攻击 CD 期间持续面向玩家并 `walkleft` 绕圈；两条 CD 归零时**优先**打出特殊攻击（近距 sa1 / 远距 sa2）
- [ ] Boss：削满韧性 → `hit` **破韧硬直打断动作** → 硬直后有免疫期、不会连续硬直
- [ ] Boss：长时间离开战斗范围能脱战回 `N_idle`（双 CD 与韧性清零）
- [ ] ⏸️ Boss：初见动画 → 击败后 `BossDied` 触发胜利结算（**初见动画为后续**）

---

## 6. 首版数值表（**全部进 SO，代码里不许出现数字**）

> ⚠️ 以下为**起始值**，不是平衡值。所有条目都应在配置资产里可改。

| 对象 | 数值 | 来源 |
|---|---|---|
| 玩家生命 | 100 | 现有 `PlayerConfig.maxHp` |
| 玩家魔力 | 100 | 现有 `PlayerConfig.maxMana` |
| 普攻 4 段伤害 | 12 / 15 / 10 / 20 | 现有 `Weapon_Sword.asset` |
| 冲刺 | 0 消耗 + 0.5 s 无敌 | 现有 |
| 近战小怪 | HP 30 / 伤害 10 | 现有 `EnemyAIConfig` |
| 远程小怪 | HP 30 / 伤害 8 / 冷却 2.2 / 舒适距离 6 | 现有 `EnemyCasterConfig` |
| **局内升级所需经验** | 50 × 1.35^(n-1)（建议做进曲线） | **新增** |
| **每级成长** | +8 生命 / +1 攻击 | **新增** |
| **魔晶掉落** | 小怪 1~3 / Boss 15~25 | **新增** |
| **装备升级消耗** | 20 × 1.5^(level-1) | **新增** |
| **锻炼单价** | 30 魔晶 / 次，上限 10 次 | **新增** |
| **法术解锁价** | 60 魔晶 | **新增** |
| Boss 生命 | 600（≈ 小怪 20 倍） | GDD §7.1 草案 |
| **Boss 韧性上限** | 100 | **新增**（`BossConfig.maxPoise`） |
| **Boss 削韧倍率** | ×2（削韧 = 伤害 × 2） | **新增**（`BossConfig.poisePerDamageMultiplier`） |
| **Boss 破韧免疫期** | 0.6 s | **新增**（`BossConfig.poiseBreakImmunityTime`） |

---

## 7. 存档设计

**已定方案**：JSON 文件（`Application.persistentDataPath`），`JsonUtility`，带 `version` 字段（见 `GDD.md` §11.2）。

**第一阶段存档内容**

| 字段 | 类型 | 说明 |
|---|---|---|
| `version` | int | 存档格式版本，用于将来迁移 |
| `magicCrystal` | int | 魔晶数量 |
| `equipmentLevels` | `List<EquipmentSave>` | 装备 id + 等级 |
| `learnedSpellIds` | `List<string>` | 已学法术 |
| `trainingCounts` | `List<TrainingSave>` | 各分项已锻炼次数 |
| `quests` | `List<QuestSave>` | 任务 id + 状态 + 进度 |
| `stats` | 若干个 int/float | 通关次数 / 死亡次数 / 最好用时 |

**技术建议**

| # | 建议 | 理由 |
|---|---|---|
| 1 | ⚠️ **只用 `List<T>`，不用 `Dictionary<TKey,TValue>`** | `JsonUtility` **不支持** `Dictionary`，会静默存成空 |
| 2 | ⚠️ **不用多态 / 接口字段** | `JsonUtility` 存不了多态，会被截断成基类字段 |
| 3 | 读写**全部 `try/catch`**：文件缺失 → 默认值；损坏 → 默认值 + 覆盖写回 | 首次运行与手改坏文件都不会崩 |
| 4 | 存盘时机：**只在关键节点**（离开酒馆 / 通关 / 死亡结算 / 设置变更） | 不每帧写 |
| 5 | 存档类 `ProgressSaveData` 与运行时 `PlayerProgress` **可分离**（存档对象是扁平 DTO） | 避免运行时结构变化直接破坏存档格式 |
| 6 | 迁移钩子：`if (data.version < CURRENT) Migrate(data);` | 现在就留位置，将来改字段不慌 |

---

## 8. 新增 ScriptableObject 清单

| 配置 | 用途 | 关键字段 | 备注 |
|---|---|---|---|
| `SpellConfig` | 法术定义 | id / 名称 / 图标 / 魔力消耗 / 冷却 / 前摇 / 预制体 / 伤害 | 取代"挂在武器上的特殊攻击" |
| `EquipmentConfig` | 装备定义 | id / 名称 / 图标 / `costCurve` / `statCurve[]` | 第一阶段不掉落 |
| `LootTable` | 掉落表 | `List<{ itemId, weight, min, max }>` | 多怪复用 |
| `ExpCurve` | 局内经验曲线 | `float[]` 或 `AnimationCurve` | 做进资产，别写公式 |
| `LevelGrowth` | 每级成长 | `List<StatModifier>` | 复用 §5.3.3 的同类结构 |
| `QuestConfig` | 任务定义 | id / 标题 / 目标类型 / 数量 / 奖励列表 | |
| `BossConfig` | Boss 数值与招式 | HP / 各招伤害 · 判定形状 · 前摇 · 冷却 / **韧性条** | ✅ **已建 `BossConfig : EnemyAIConfig`（2026-10-15）**，字段清单见 §5.4.4 |
| `EncounterConfig` | 区域敌人配置 | 区域 id → 敌人列表 | 让 `RoomController` 不写死 |

> 现有已就绪的 SO（`PlayerConfig` / `EnemyAIConfig` / `EnemyCasterConfig` / `WeaponConfig`）**继续用**，不重做。

---

## 9. 事件总线扩展

现有 `Core/GameEvents.cs` 只有 `PlayerDied` / `BossDied`。第一阶段建议扩为（**全部静态门面 + 订阅方成对退订**）：

| 事件 | 参数 | 谁发 | 谁听 |
|---|---|---|---|
| `EnemyKilled` | `(EnemyHealth, int exp)` | `EnemyHealth.Die` | 局内升级 / 任务系统 |
| `LootPicked` | `(string itemId, int count)` | `Pickup` | 魔晶计数 UI / 任务 |
| `PlayerLevelUp` | `(int newLevel)` | `PlayerLevel` | HUD / 属性重算 |
| `PlayerStatsChanged` | — | `PlayerStats` | HUD 血条/魔力条 |
| `QuestUpdated` | `(string questId, int cur, int need)` | `QuestService` | 任务 UI |
| `SceneLoadRequest` | `(SceneId)` | 出城触发器 / 结算 | `GameFlow` |

⚠️ **已经踩过的坑**：`EnemyHealth.Died` 是 **static 事件**，跨场景重开有重复订阅风险（**T7**）。
新增的静态事件**必须**同样遵守 `OnEnable` 订阅 / `OnDisable` 退订，否则会"一次击杀加两份经验"。

---

## 10. 新增脚本清单

| 脚本 | 目录 | 职责 | 依赖 |
|---|---|---|---|
| `GameFlow` | `Core/` | 场景枚举 + 加载收口 | `SceneManager` |
| `ProgressService` | `Core/` | `PlayerProgress` 唯一读写入口 + 存读盘 | 存档 |
| `PlayerProgress` | `Data/` | 纯数据（可序列化） | — |
| `PlayerStats` | `Gameplay/Player/` | 属性修正器管线 + 广播变化 | `ProgressService` |
| `PlayerLevel` | `Gameplay/Player/` | 局内经验/等级 | `GameEvents` |
| `IInteractable` / `InteractionDetector` | `Gameplay/` | NPC 交互框架 | `InputService.Interact` |
| `NpcInteractable` + 4 子类 | `Gameplay/Npc/` | 4 类 NPC 功能 | UI 面板 |
| `SpellCaster` / `SpellInventory` | `Gameplay/Player/` | 法术施放与已学列表 | `PlayerMana` |
| `LootSpawner` / `Pickup` | `Gameplay/Loot/` | 掉落与拾取 | `EnemyHealth.Died`、`ObjectPool` |
| `EquipmentService` / `TrainingService` | `Gameplay/Progression/` | 局外养成 | `PlayerProgress` |
| `QuestService` | `Gameplay/Quest/` | 任务推进与交付 | `GameEvents` |
| `SaveSystem` | `Core/` | JSON 存读盘 + 版本迁移 | `PlayerProgress` |
| `BossController` + 各招式子状态 | `Gameplay/Enemy/` | Boss 决策层 + 招式层 | `EnemyStateMachine<TState>`、`Hitbox` |
| `CameraCue` | `Camera/` | 初见演出的相机接管 | `CameraFollow` |
| `NpcPanelBase` + 各面板 | `UI/` | 面板基类与 4 个面板 | — |

**⚠️ 工程约定（动手前必看）**

- 单程序集 `Assembly-CSharp`，**无 asmdef** → 新脚本直接丢进 `Assets/_Game/Scripts/` 对应子目录即可。
- **C# 9**（Unity 6000.0.83f1）→ 别用 C# 10+ 语法（`record struct`、全局 using、文件级 namespace）。
- 新增 `.cs` **不会自动进** `Assembly-CSharp.csproj`（它是 gitignore 的生成物）→ 命令行编译验证时需临时插 `<Compile Include>`，验完还原；**或直接在 Unity 里靠 `read_console` 验证**。

---

## 11. 风险与前置

| # | 风险 | 影响 | 缓解 |
|---|---|---|---|
| 1 | **T1**（`enabled=false` 停重力） | **阻塞跳跃** | 动跳跃前先修，拆"移动抑制 / 重力常驻" |
| 2 | **`Jump` 输入与动画都缺** | 阻塞跳跃 | 补 Action + 重生成 `PlayerControls.cs`；动画手 K（`ANIM-GUIDE.md`） |
| 3 | **酒馆场景无美术** | 阻塞酒馆交付 | 复用 `LeartesStudios` 建筑件搭白盒；或先做"程序化占位房间" |
| 4 | **Boss 招式动画为零**（FBX 内 0 动画） | 阻塞 Boss 演出 | 手 K 或 Humanoid 重定向；⚠️ 注意剑要跟手 |
| 5 | **倒退动作 clip 缺失** | 影响远程怪观感 | 补 1 个 clip；⚠️ 别用 Walk 倒放（Wraith 是悬浮怪） |
| 6 | **范围膨胀**（一次加 5 个子系统） | 交付风险 | 严格按 §12 顺序做，**每步独立可验收** |
| 7 | 静态事件重复订阅（T7） | 经验/魔晶翻倍 | 新增静态事件一律 `OnEnable/OnDisable` 成对 |

---

## 12. 实施顺序（每步独立可验收）

| 阶段 | 内容 | 产出 |
|---|---|---|
| **P1.0** | 修 **T1** + 补 `Jump` 输入与动画 + 玩家状态机扩 `Walk/Jump/Fall/Land` | 玩家能在城堡里跳、走、跑 |
| **P1.1** | `GameFlow` + `ProgressService` + `PlayerProgress` + `SaveSystem` | 能在 `MainMenu` / `Tavern` / `Castle` 间切换且数据不丢 |
| **P1.2** | 酒馆白盒 + `IInteractable` 框架 + `Interact` 输入暴露 + 提示 UI | 能走到 NPC 前并打开一个空面板 |
| **P1.3** | `PlayerStats` 修正器管线 + 4 个 NPC 功能（装备/法术/锻炼/任务） | 魔晶能花出去，数值真的变 |
| **P1.4** | `LootSpawner` + `Pickup` + `PlayerLevel` + 局内升级 | 击杀 → 掉魔晶 → 升级闭环 |
| **P1.5** | 城堡 3 区域布置 + 两种小怪数值定稿 + **倒退动作**接入 | 战斗手感成型 |
| **P1.6** | `BossController` + 三招 + 突刺追击 + `CameraCue` 初见演出 | Boss 战可打 |
| **P1.7** | HUD（等级/经验/魔晶）+ 音效 + 构建 | **第一阶段可交付** |

---

## 13. 待定项（需要你拍板）

| # | 事项 | 建议 | 影响 |
|---|---|---|---|
| **A** | ⚠️ **局内升级是否永久保留** | 建议**不保留**（回酒馆重置） | 决定两条成长线会不会互相稀释、Boss 数值怎么定 |
| **B** | 玩家死亡后**已获得的魔晶是否保留** | 建议**保留一半**（或全保留，第一阶段简单优先） | 决定死亡惩罚力度 |
| **C** | 酒馆的美术方案 | 复用 `LeartesStudios` 建筑件搭白盒 | 决定酒馆工期 |
| **D** | 行走/奔跑的切换方式 | 建议 **Shift 切换**（`Sprint` 动作已存在） | 影响手感与动画需求 |
| **E** | 法术槽数量 | 建议第一阶段**只留 1 槽** | 影响 UI 复杂度 |
| **F** | 初见演出是否锁玩家输入 | 建议**锁 + 可跳过** | 影响演出体验 |
| **G** | 局内等级上限 | 建议 **10 级**（够撑一次下城） | 影响经验曲线 |

---

## 14. 与 GDD 的口径对齐（避免第三套说法）

| 项 | 以谁为准 | 说明 |
|---|---|---|
| 类型 / 视角 / 战斗判定 / 武器 / 魔力 | **`GDD.md`** | 本文件不改这些 |
| 玩家动作集（第一阶段） | **本文件 §3** | GDD §4 是总则，本文件是第一阶段增量 |
| 场景结构（含酒馆） | **本文件 §2** | ⚠️ 会把 `GDD.md` §9.1 从"单一主场景"修正为"主战斗场景 + 安全中枢" |
| 敌人通用架构 / 寻路 | **`GDD.md` §6 + `NAVMESH-GUIDE.md`** | 本文件只写第一阶段增量 |
| Boss 招式表 | **本文件 §5.4**（第一阶段定稿三招） | GDD §7.4 的"待讨论项"由此收敛 |
| 掉落 / 成长 / 法术 / 装备 / 任务 / 存档内容 | **本文件** | 新增系统，本文件为权威来源 |
