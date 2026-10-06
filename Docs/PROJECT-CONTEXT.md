# 项目上下文 / 交接文档（PROJECT CONTEXT）

> **用途**：这份文档是为了"**上下文长度不足、开启新会话时快速接管整个项目**"而写的。
> 任何新的 AI 会话或协作者，**读完本文即可理解项目全貌并开始干活**，不需要重新通读全部代码。
> 配套文档见文末 §11 文档索引。
>
> 最后更新：2026-10-07 | 项目阶段：**阶段 0 ✅ · M1 系统骨架全部 ✅（魔力/Hitbox/暂停/文案）· M2 武器（剑）全部 ✅（配置/装配/连段动画/火球特攻）· M3.1 NavMesh 烘焙 ✅ · M3.2 敌人状态机基类 ✅ · M3.3 视野感知 ✅ · M3.4 剑兵 Alert/Reposition ✅ · M3.5 法师（含 kiting）✅ · M3.6 敌人动画状态机 ✅（含敌人换装 Wraith）** · 资源改回**魔力**且**不再自动回复** · 冲刺穿墙已修（T27） · 攻击中"不移动但可限速转向"（手感方案 A）
>
> **下一步：M3.7 多关卡场景拆分 + 关卡流转**（✅ **M3 敌人部分已全部完成**：M3.1~M3.6 全绿）。
> 🔄 **2026-10-07 敌人换装**：近战 `Monster1` / 远程 `Monster2`，模型 = Wraith（自带有效 `WraithAvatar`）。
> 换装原因：旧 `Boxer` / `Gunner` 的 Animator **`avatar` 是空的**、全工程没有对应 Avatar 资产 ⇒ Humanoid 肌肉剪辑**播不出来** ⇒ 攻击动画不播、`TickAttack` 不触发、**打不出伤害**。

---

## 1. 一句话定位

Unity 6 的 **3D 近战动作游戏**（武术/暗黑奇幻题材）。框架自研，包含状态机、事件总线、对象池、A\* 等模块。当前正从"学习项目"改造为**课程设计项目**。

> ⚠️ **2026-10-05 关卡结构定稿：多关卡独立场景** —— `Level_01` / `Level_02` / `Level_03`
> 各自一个 `.unity` 场景，逐关清怪推进。详见 [`GDD.md`](GDD.md) §9。
> （中间曾评估"ARPG 单场景：一条通路直达 Boss、沿途小怪"，**未采纳** ——
> 评估过程存档在 [`ARPG-DIRECTION.md`](ARPG-DIRECTION.md)，⚠️ **其中 §4.6 相机实现仍然生效**。）
> 玩家模型已换成 **Roskva**（UE 素材包接入，Humanoid；剑模型与 4 段连段动画正在接入）；
> 相机为**鼠标控视角的后置跟随**（可仰视、动态地面安全角防穿地）。
> ⚠️ **武器范围收敛为「只做剑」** —— 第二把武器（长枪）与武器切换为额外项目（`ROADMAP.md` §13）。

---

## 2. 环境与运行

| 项 | 值 |
|---|---|
| 工程路径 | `D:\Unity\Unity Project\CurriculumDesign\Demo-main` |
| Unity 版本 | **6000.0.83f1**（Unity 6） |
| 渲染管线 | **URP 17.0.4** |
| 输入 | **新输入系统 1.19**，`Assets/InputSystem_Actions.inputactions` → 生成 `PlayerControls.cs` |
| 程序集 | **单一 `Assembly-CSharp`，无 asmdef** |
| 关键包 | TextMesh Pro、Timeline、AI Navigation、Test Framework、Visual Scripting |
| 特殊包 | **`com.coplaydev.unity-mcp`**（Unity MCP，装了这个外部 AI 才能直接操作编辑器） |
| 场景 | `Assets/Scenes/MainMenu.unity`（buildIndex 0）、`Assets/Scenes/Game.unity`（buildIndex 1） |
| 编译状态 | ✅ 控制台 0 报错 0 警告 |
| Git 仓库 | ✅ **已建并已接入远端**：本地 `Demo-main\.git` → `origin` = `https://github.com/xxvv-111/Trial.git`（**旧仓库 `github.com/xxvv-111/Demo.git` 不再使用**） |
| 当前工作分支 | **`main`**，跟踪 `origin/main`（原仓库分支名为 `CurriculumDesign`） |
| 分支状态 | ✅ **已同步**：`main` = `b80d608`（M2.2–M2.4 + 排除两个动画包），**本地与远端一致**（0 领先 / 0 落后）；仓库含 `.gitignore` + `.gitattributes`，**未使用 LFS**（最大文件 13.7 MB，低于 GitHub 100 MB 限制） |

**运行**：Unity 打开工程 → 打开 `MainMenu` 场景 → Play（或先 Play 再走开始按钮）。`Game` 场景也可直接 Play。

**注意**：`Assets/_Game/Notes/` 是空目录（仅 `.gitkeep`）；`Assets/_Game/Audio/` 空；`Assets/_Game/Prefabs/UI/` 空。

### 2.1 ⚠️ 版本控制排除清单（`.gitignore` 明确排掉的大体积 / 第三方资源）

> **本地保留，克隆仓库后需自行放回。**（2026-10-05 起）

| 排除项 | 体积 | 为什么可以排除 |
|---|---|---|
| `Assets/TKDstyle_AnimSet/` | 257 MB | 第三方武术动画包，另有署名/授权要求 |
| **`Assets/Magical-Knight_Set/`** | **790 MB** | 第三方 Humanoid 动作包（100+ 动作）。**M2.3 的 4 段连段动画是从它里面"抽出来复制"成独立 `.anim` 的**，复制后**不依赖原包** |
| **`Assets/Rapier_Anim_Set/`** | **793 MB** | 第三方 Humanoid 动作包，**当前未被使用** |
| Roskva 包内 `.psa` / `.psk` | 约 26 MB | Unreal 私有格式，**Unity 根本读不了** |

合计排除约 **1.86 GB**。⚠️ 三个动画包合计 1.84 GB，**远超 GitHub 单文件 100 MB / 仓库 1 GB 的软限制**，因此必须排除。

✅ **排除它们不影响克隆后运行**（已实测）：游戏真正使用的动画都**抽成了独立 `.anim` 文件**放在
`Assets/_Game/Art/Animations/`（模型同理，在 `Art/characters/`），并且**全工程没有任何
`.prefab` / `.controller` / `.anim` 引用这三个动画包的 GUID** —— 唯一的引用者是包自己的 `.meta`。
→ 所以克隆后**能正常打开工程并 Play**；只是要**再取用包内其它动作**时，得把包放回 `Assets/` 原路径。

**AI 接入（MCP）现状（2026-10-03 实测可用）**：ZCode 侧的用户配置 `~/.zcode/cli/config.json` 里声明了 HTTP 类型服务器 `unity` → `http://127.0.0.1:8080/mcp`（`timeoutMs: 60000`）。服务由 Unity 内的 `com.coplaydev.unity-mcp` 包（**v10.0.0**）提供，服务端自报 **`mcp-for-unity-server` v3.4.7**，暴露 **47 个工具**（`manage_gameobject`、`manage_scene`、`manage_script`、`read_console`、`execute_menu_item`、`manage_editor`、`set_active_instance` …）与 19 个资源（`mcpforunity://project/info`、`editor/state`、`instances` …）。
MCP 侧确认的工程根：`D:/Unity/Unity Project/CurriculumDesign/Demo-main`，实例名 **`Demo-main@890c462facb3146d`**，Unity **6000.0.83f1**，当前场景 `Assets/Scenes/Game.unity`，控制台 0 报错 0 警告。

⚠️ **两个已踩过的坑（下次直接用结论）**
1. **首次 `tools/list` 约 20 s，之后走缓存近乎瞬发。** 这慢于 ZCode 建立会话时的工具收集窗口，所以**新会话的第一条消息里常常看不到 `mcp__unity__*` 工具**（日志表现为 `mcp.startup.completed` 时 `unity` 仍是 `connecting`、`toolCount` 只算了其它服务器）。ZCode 进程内首次连接把缓存焐热后即恢复正常。
2. **不指定活动实例时，工具调用会失败**：返回 `{"success":false,"error":"Unity session not available; please retry","reason":"no_unity_session"}`（每次还白等一个 20 s 超时）。原因是服务端有多个 Unity 实例时不再自动路由。
   → 先调 `set_active_instance(instance="Demo-main@890c462facb3146d")` 即可（本机已设为全局活动实例，故后续调用正常）。可用资源 `mcpforunity://instances` 查看当前连了哪些编辑器。

---

## 3. 目录结构地图

```
D:\Unity\Unity Project\CurriculumDesign\Demo-main\
├── Assets\
│   ├── Scenes\
│   │   ├── MainMenu.unity          # 开始界面（标题 + 开始/退出）
│   │   └── Game.unity              # ⚠️ 当前**唯一**游玩场景（3 房间 + 玩家 + Canvas + GameManager）
│   │                               #  ⚠️ 待拆分为 Level_01/02/03.unity（见 ROADMAP M3.7）
│   ├── Editor\
│   │   ├── AStarSelfTest.cs        # A* 的编辑器自测（菜单 Tools/A* 自测、自测2：无路）
│   │   ├── FieldInspectorMenu.cs   # 反射打印脚本字段（调试工具）
│   │   ├── HitboxAnimationValidator.cs  # M1.2 新增：校验攻击动画是否挂了判定事件
│   │   ├── RoskvaAttachSword.cs    # ★ 新增：把剑挂到 Roskva 右手骨骼（菜单 Tools ▸ Roskva ▸ 1）
│   │   └── SwordComboImporter.cs   # ★ 新增：提取 4 段挥砍动画 + 挂 OnAttackHit（菜单 Tools ▸ Roskva ▸ 3）
│   ├── Settings\                   # URP 配置资产（PC/Mobile RPAsset、Volume Profile）
│   ├── TextMesh Pro\               # TMP 资源
│   ├── TKDstyle_AnimSet\           # 第三方武术动画资源包（⚠️ 署名要求，见 §9 T17；⚠️ **已排除出版本控制**）
│   ├── Magical-Knight_Set\         # 第三方 Humanoid 动作包，790 MB（100+ 动作，M2.3 连段动画的来源；⚠️ **已排除出版本控制**，见 §2 末）
│   ├── Rapier_Anim_Set\            # 第三方 Humanoid 动作包，793 MB（**当前未使用**；⚠️ **已排除出版本控制**，见 §2 末）
│   ├── InputSystem_Actions.inputactions / PlayerControls.cs（自动生成）
│   ├── _Game\                      # ★ 游戏主体
│   │   ├── Art\
│   │   │   ├── characters\Roskva\  # ★ 玩家角色（UE 素材包接入，**Humanoid**）
│   │   │   │   ├── _model\fbx\     # ★ 两个玩家模型（**均 Humanoid**，网格/材质同名）
│   │   │   │   │                   #  · OVR - Roskva.fbx       背剑版（9 网格，无独立剑网格）
│   │   │   │   │                   #  · OVR - Roskva_Sword.fbx 持剑版（+ Roskva_Sword_Hand，416 三角面）
│   │   │   │   │                   #    ⚠️ 6 材质与 Roskva 同名 → 复用同一批 Mat_Roskva_*.mat
│   │   │   │   │                   #    ⚠️ 剑网格**无 UV、仅 1 材质槽**（详见 §7 与 ART-ASSETS）
│   │   │   │   │                   #  ⚠️ OVR - Roskva_Animated.fbx 已删除；动画现来自 SwordInHand.fbx（含 Walk）
│   │   │   │   ├── _textures\      # 贴图（含 _urp 子目录；Hair_Gold 为金发变体）
│   │   │   │   ├── Meshes\         # ★ Equips_NoSword.asset（去掉剑的装备网格）
│   │   │   │   ├── Materials\      # Mat_Roskva_*（6 个人物材质）
│   │   │   │   │                   #  ★ Mat_Roskva_Sword_Blade（剑身，抛光钢，2026-10-05 新建）
│   │   │   │   └── anim\
│   │   │   ├── Animations\Player\  # PlayerAC.controller + Idle/Run/Dash/combo_01_1-4/Hit/Death
│   │   │   ├── Animations\Enemy\   # Monster1/AC_Monster1 + Monster2/AC_Monster2（各 5 状态）+ old/EnemyAC（占位，已弃用）
│   │   │   └── Materials\
│   │   ├── Audio\                  # ❌ 空（见 §9 T18）
│   │   ├── Notes\                  # ❌ 空
│   │   ├── Prefabs\
│   │   │   ├── Player\Player.prefab      # 含 HitboxController + Hitbox_Attack1~4（M1.2）
│   │   │   ├── Enemy\{Monster1, Monster2, Boxer, Gunner, Enemy_Slime, Bullet}.prefab
│   │   │   │                         # ★ 现役 = Monster1（近战，含 Hitbox_Attack）/ Monster2（远程，含 FirePoint）
│   │   │   │                         #   Boxer / Gunner 已从场景移除（avatar 为空，播不出动画），预制体暂留
│   │   │   ├── Effect\ / Fx\        # 打击特效预制体
│   │   │   └── UI\                 # ❌ 空
│   │   └── Scripts\                # ★ 全部游戏代码（详见 §5）
│   │       ├── Core\ Data\ Data\Weapons\ Fx\ AI\ Camera\ Tools\ UI\
│   │       └── Gameplay\           # 玩家 / Enemy / Room / **Combat**（★ M1.2 新增）
└── Docs\                           # ★ 本文档所在（非 Unity 资产，未进 Assets）
```

> ⚠️ **已清理，勿再引用**：`Assets/_Modules/`（11 个教学脚本，T16）、`Tools/` 下 6 个旧方案脚本（T15）、
> 旧 `Y Bot.fbx` 角色（已被 Roskva 取代）。

---

## 4. 命名空间分层

| 命名空间 | 职责 |
|---|---|
| `Game.Core` | 基础设施：事件总线、输入、对象池、工具扩展、接口 |
| `Game.Data` | ScriptableObject 数值配置 |
| `Game.Gameplay` | 玩家、敌人、房间、流程管理 |
| `Game.Gameplay.Enemy` | （仅 `EnemyMeleeAI` 使用，与 `Game.Gameplay` 不一致 ⚠️） |
| `Game.AI` | A* 算法 |
| `Game.Fx` / `Fx` | 打击特效（⚠️ `DamagePopup` 在裸 `Fx` 命名空间） |
| `Game.UI` | HUD 与菜单 |

---

## 5. 类清单与职责（核心参考）

### 5.1 `Game.Core`（基础设施）

| 类 | 职责 / 关键成员 |
|---|---|
| `EventCenter` | **静态泛型事件总线**。`Subscribe<T>/Unsubscribe<T>/Publish<T>`，内部 `Dictionary<Type, Delegate>`，**约束 `where T : struct`** |
| `GameEvents` | 静态事件门面：`PlayerDied`、`BossDied` + `RaisePlayerDied()` / `RaiseBossDied()` |
| `IDamageable` | 接口，仅 `void TakeDamage(int dmg)`。`PlayerFSM`、`EnemyHealth`、`EnemyMelee`、`Target` 实现 |
| `InputService` | **单例**，包装新输入系统。暴露 `Move`（已做归一化与死区）、`DashPressedThisFrame`、`AttackPressedThisFrame`。`Awake` 建 `PlayerControls` 并 `Enable()`，`OnDestroy` 里 `Disable()+Dispose()` |
| `ObjectPool<T>` | **泛型对象池**（`where T : Component`）。构造时按 `prewarm` 预热入 `Stack`；`Get()` / `Release()`。`TotalInstantiated` 计数 |
| `VectorExt` | 扩展方法：`FlattenY()`（压掉 y）、`WithY(float)` |

### 5.2 `Game.Data`（配置）

| 类 | 字段 |
|---|---|
| `PlayerConfig` | **只含角色自身属性**（M2.1 起攻击数值已迁出）：移动 `moveSpeed 6`/`dashSpeed 10`/`dashTimer 0.5`/`dashDelay 0.1`/`iFrameTime 0.5`；生存 `maxHp 100`/`hitInvulnTime 0.8`；**魔力** `maxMana 100`/`specialManaCost 30`。⚠️ 2026-10-06 二次改动：`manaRegenDelay` / `manaRegenRate` **已删除** —— 魔力不再自动回复（见 §10）。⚠️ 2026-10-06 改动：`maxEnergy`→`maxMana`、`specialEnergyCost`→`specialManaCost`，**删除** `dashEnergyCost`/`attackEnergyCost`；`comboWindow`/`attackRange`/`attackDamage` 仍在 `WeaponConfig`，此处仅作**未装配武器时的兜底** |
| `EnemyAIConfig` | `displayName`、`hp 30`、`aggroRange 8`、`attackRange 1`、`moveSpeed 3`、`windupTime 1`、`recoverTime 1.5`、`damage 10`、`prefab`、`hitEffect`；**受击反应（M1.2 新增）**：`canBeInterrupted true`（⚠️ **Boss 必须设 false**）/`hitStunTime 0.4`/`knockBackDistance 0.4`/`flashTime 0.15`；**视野感知（M3.3 新增）**：`viewDistance 0`（0 = 沿用 `aggroRange`）/`viewAngle 160`（360 = 退回纯距离感知）/`requireLineOfSight true`/`proximityRange 2`/`eyeHeight 1.2`/`aimHeight 1.0`/`loseTargetTime 0`（**0 = 永不放弃目标，等价于迁移前的行为**）；**近战节奏（M3.4 新增）**：`alertTime 0.35`（Alert 停顿时长，0 = 看见就追）/`repositionDistance 1.5`（攻击后退开距离，0 = 不后退） |
| `WeaponConfig` | **M2.1 新建**（原同名文件已在阶段 0 清理死代码时删除）。标识 `displayName`/`icon`；连段 `comboWindow`/`damage[4]`；判定盒 `hitboxLength[4]`/`hitboxWidth[4]`/`hitboxHeight`/`hitboxCenterY`；特殊攻击 `specialType`(枚举 `SpecialAttackType`)/`specialDamage`/**`specialManaCost`**/`specialCooldown`/`specialRange`/火球专用 5 项；外观 `modelPrefab`/`modelLocalPosition`/`modelLocalEuler`/`gripBoneName`。附取值辅助 `GetDamage`/`GetHitboxSize`/`GetHitboxLocalPosition`。⚠️ 2026-10-06 **删除** `energyCost[4]` 与 `GetEnergyCost()`（普攻免费） |

资产路径：
- `Assets/_Game/Scripts/Data/{PlayerConfig,EnemyAIConfig}.asset`
- `Assets/_Game/Scripts/Data/Weapons/{Weapon_Sword,Weapon_Spear}.asset`（**M2.1 新增**）

### 5.3 `Game.Gameplay`（玩家 + 战斗判定）

| 类 | 职责 / 要点 |
|---|---|
| `PlayerFSM` | **玩家状态机（核心）**。`Dictionary<PlayerState, Action>` 三张表（`_enter` / `_update` / `_exit`）；`Change(next)` 做 exit→set→enter；`Death` 状态是**终态**（拒绝一切切换）。实现 `IDamageable.TakeDamage`，内部判 `Invulnerable`（`_invulnTimer` 0.8s 或 `Dash.IsInvulnerable`）。⭐ 2026-10-06 起**攻击状态用 `_motor.SetFacingOnly(true)`**（不移动但可限速转向），并在 `_exit[Attack]` 复位；冲刺 / 施法 / 受击 / 死亡仍用 `_motor.enabled = false` |
| `PlayerState` | 枚举：`Idle, Run, Dash, Attack, Hit, Death` |
| `PlayerMotor` | 移动。读 `InputService.Move` → `CharacterController.Move()`；`Face(dir, degPerSec)` 用 `RotateTowards` 限速转向；驱动动画混合树参数 `speed`。⭐ **位置与朝向的唯一写入者**：`StepMove` / `StepMoveGrounded`（供 `PlayerDash`）、`SetFacingOnly(bool)`（供攻击）。⭐ 攻击转向由 `attackTurnSpeed`（180）+ **`gateTurnByHitFrame`（默认开 = 方案 B 转向窗口）** 控制，窗口判据走 `PlayerAttack.CanTurn`。⚠️ **`enabled=false` 会连重力/贴地一起停掉**（见 §9 T1）；**攻击状态已改用 `SetFacingOnly`**，不再整组件禁用 |
| `PlayerDash` | 冲刺。`BeginDash()` 设 `_dashTimer` + 无敌帧；`LateUpdate` 里按 `transform.forward * dashSpeed` 位移；`IsDashing` / `IsInvulnerable` 供 FSM 查询 |
| `PlayerAttack` | 普攻与连段。`StartCombo()` / `TryNextCombo()`（连段）；动画事件 **`OnAttackHit()`** 只负责**开启该段判定体**（M1.2 起，不再是瞬时采样）→ 命中结算由 `HitboxController` 统一收口。数值（伤害/连段窗口/段数/判定盒尺寸）读 `WeaponConfig`，未装配时回退 `PlayerConfig`；✅ **M2.2 已装配 `Weapon_Sword`**；`SyncHitboxDamage()` 在开判定体前写入该段伤害；`CloseAllHitboxes()` 供 FSM 在离开 Attack 时清残留。⚠️ **2026-10-06：移除对资源系统的依赖** —— `_energy` 字段与"资源不足则该段不接续"的判断已删除，连段窗口内可一直接。⭐ **2026-10-06 手感配套：新增转向窗口信号** —— `_hitThisSegment`（`OnAttackHit()` 里置 true，`StartCombo()` / `TryNextCombo()` 清零）与只读属性 **`CanTurn`**，供 `PlayerMotor` 判"命中帧前可转向、出手后锁定"（**方案 B**） |
| `PlayerHealth` | 血量。`MaxHp/CurHp`、`OnHpChanged` 事件、`ApplyDamage()`。⚠️ `Died` 事件与 `Die()` **被注释掉了（w6）**，实际未使用（见 §9 T8） |
| **`PlayerMana`** | **魔力**（2026-10-06 由 `PlayerEnergy` 改名；**文件与 `.meta` 一起 git mv，GUID 未变，场景/预制体引用都没断**）。`MaxMana`/`CurMana`、`OnManaChanged`、`OnManaSpendFailed`；**唯一消耗出口** `TrySpend(float)` + `TrySpendSpecial(override)`。⚠️ **成本表集中在本类**（统一出口原则）。⭐ **2026-10-06 起本类只剩一个消耗入口**：`TrySpendDash()` / `TrySpendAttack()` **已删除**（冲刺与普攻免费）。⭐⭐ **2026-10-06 二次改动**：**本类已无 `Update`**（魔力不再随时间回复），唯一的"加魔力"入口是 `Restore(amount)` / `RestoreFull()`（**当前无调用方**，留给击杀回魔 / 拾取回魔） |
| `AttackStateBehaviour` | `StateMachineBehaviour`，在攻击动画状态 enter/exit 时调 `PlayerAttack.SetAttacking(true/false)`，**替代每帧轮询** |
| `AttackFxBridge` | 把 **`HitboxController.OnHit`** 桥接到 `HitFxSystem.Play`（M1.2 起订阅源由 `PlayerAttack.OnHit` 改为判定体系统，使所有命中来源走同一条特效通道） |
| `BipedTwistBoneFixer` | **扭转骨补驱动**（2026-10-05 新增）。`LateUpdate` 里把 4 根 Biped 扭转辅助骨（`Bip001-L/R-ForeTwist` / `ForeTwist1`）设为**刚性跟随对应前臂**。存在的唯一原因：这 4 根骨**没有任何 human 映射**，而 Humanoid 的 Animator 只驱动映射过的骨 → 前臂转动时它们不动 → 手腕/前臂网格被撕扯（实测最大位移 45 mm，手腕半径仅约 30 mm）。`offset` 从 `SkinnedMeshRenderer` 的 **bindpose** 反算（不依赖"唤醒时还没播动画"的时序假设），**绑定姿态下严格 no-op**。⚠️ 别试图用"把扭转骨填进 Avatar 的 Lower Arm Twist 槽位"替代 —— 实测无效。已挂 `Player1.prefab` / `Player2.prefab`（`Player.prefab` 上也挂过一次，但该预制体随后已从磁盘删除，见 §12） |
| `Fireball` | **特殊攻击「火球」**（M2.4 新增）。`Launch(dir, speed, maxDistance, damage, explosionRadius, owner)` → 直线飞行 → 命中 / 到射程就在原地爆炸 → `Physics.OverlapSphere` 对半径内**每个** `IDamageable` 结算一次（**已用 `HashSet` 去重**，避免同一目标的多碰撞体被打多次）→ 自毁。⚠️ **命中伤害与溅射合并成一次 OverlapSphere**：爆炸中心即命中点，直接命中者必然在半径内，这样**天然不重复**。⚠️ 移动方式沿用 `Bullet.cs`（`transform.position +=`），速度 12 m/s、每帧约 0.2 m，远小于敌人碰撞体半径（约 0.5 m），**不会穿透**。`owner` 用于排除自己（`IsChildOf`） |
| `SpecialStateBehaviour` | **`Special_Attack` 状态的动画回调**（M2.4 新增）。进/出时调 `PlayerFSM.SetCasting(bool)`。⚠️ **刻意不复用 `AttackStateBehaviour`**：后者改的是 `PlayerAttack.isAttacking`，而该标记被 `PlayerFSM.IsAttackAnimOver()` 与连段窗口逻辑共用，让特攻也去改它会让语义混 |

**战斗判定（`Gameplay/Combat/`，M1.2 新增 —— GDD §8）**

| 类 | 职责 / 要点 |
|---|---|
| `Hitbox` | 攻击判定体。Trigger Collider + **必需自带 kinematic `Rigidbody`**（⚠️ 敌人身上没有 RB，否则 `OnTriggerEnter` **永不触发**）；字段：阵营/伤害/可命中层；**「已命中集合」每次 `Activate()` 时清空**（作用域=一次挥砍）；`SetDamage()` / `ApplyShape()` 供武器配置驱动；`OnDrawGizmos` 编辑期半透明常显、运行期随 `HitboxController.ShowGizmos` |
| `HitboxController` | 判定总管（每角色一个）。自动收集子物体判定体；**按名开关** `EnableHitbox(name, duration)`（支持定时自动关闭，重复开启会先清旧计时）；**`TryProcessHit` 是唯一命中出口**（过滤阵营 → `TakeDamage` → 广播 `OnHit`）；`DisableAllHitboxes()` 清残留；`GetByName()` / `ApplyWeaponShapes()`（⚠️ 按**名字**匹配 `Hitbox_Attack1~4`，不用数组下标）；运行时 **F1** 切换 Gizmos |
| `HitboxTeam` | 枚举：`Player` / `Enemy`（用于区分表现；过滤主要靠 `Hitbox.TargetLayers`） |

### 5.4 `Game.Gameplay`（敌人）

| 类 | 职责 / 要点 |
|---|---|
| `EnemyHealth` | 敌人血量。⚠️ **`Died` 是 static 事件**（`Action<EnemyHealth>`）；`OnEnable` 里**重置血量**；死亡时 `Died?.Invoke(this)` 然后 `SetActive(false)`。实现 `IDamageable` |
| `EnemyMeleeAI` | 近战敌人 AI（剑兵）。**M3.2 起改为表驱动状态机**：继承 `EnemyAIController`，自己持有 `EnemyStateMachine<EState>`，差异只在状态表。⭐ **M3.4 起共 7 个状态**：`EState { Idle, Alert, Chase, Attack, Reposition, Hit, Death }` —— 新增 **`Alert`**（站住 + 转身 + 停顿 `alertTime`）与 **`Reposition`**（攻击后沿"背离玩家"方向退开 `repositionDistance`，目标点经 `NavMesh.SamplePosition` 吸附，带"到位 / 超时 / 玩家又贴脸"三个出口）。`Idle → Alert → Chase` 的判据是 **`CanSeeTarget`（距离 + 扇形 + 视线）**。伤害由动画事件 **`TickAttack()`** 触发。⚠️ 迁移前的两个隐患已一并消掉：`SetTrigger("Attack")` 不再写在 `Update` 里；`transform.position +=` 的直线位移改由 `EnemyLocomotion` 统一处理。⚠️ GDD §6.2 的 `Windup` / `Recover` **没有单独成状态**（由攻击状态内的 `windupTime` / `recoverTime` 计时表达）。⭐ **M3.6 起伤害链完整**：`EnterAttack` 触发 `Attack`（AnyState → `attack1`），攻击 clip 的 **0.45 s** 挂 `TickAttack` 开判定体；受击走 `PlayHitAnim`（触发 `Hit` **并复位 `Attack` 触发器**）；致死走 `OnKilled` → `EnterDeath` → `PlayDeathAnim`（设 `IsDead`）。模型已换装 Wraith（`Monster1`） |
| `EnemyCaster` | **远程敌人（法师）**（🔄 2026-10-06 由 `EnemyRanged` 改名；⚠️ **`.cs` 与 `.meta` 一起 `git mv`，GUID 未变** ⇒ Gunner 上的组件引用与 `bulletPrefab` / `firePoint` 两个序列化引用都没断）。**M3.5 起改为表驱动状态机**：继承 `EnemyAIController`，自己持有 `EnemyStateMachine<EState>`，**6 个行为状态** `{ Idle, Alert, Chase, Attack, Hit, Death }`（⚠️ **没有 `Reposition`** —— "保持距离"折进 `Chase`）。**kiting 核心**：`Chase` 里先算"该去哪"（太近→后撤到 `comfortDistance`；太远→靠近；在舒适区→站住等冷却），经 `NavMesh.SamplePosition`（半径 0.3）吸附后交 `EnemyLocomotion.MoveToOrStep`。`Attack` = 前摇 → 出手 → 后摇 `recoverTime`(**M3.6 起 1.2 s**，使 `windup 0.5 + 1.2 = 1.7 s ≈ taunt 时长 1.667 s`)；⭐ **出手帧由动画事件 `TickAttack()` 驱动**（GDD §6.3 口径，**M3.6 起挂在 `Monster2/taunt.anim` 的 0.50 s**），`windupTime`(0.5s) 因此降为**兜底**（谁先到谁出手，`_casted` 闸门保证只出一发）；⭐ **被打断则本次施法作废**（`TickAttack` 里先确认仍处于 `Attack` 状态）；出手用 `Bullet`（**只打 `PlayerFSM`，不会误伤同伴**，这是不选 `Fireball` 的原因 —— 后者的 `OverlapSphere` 无队伍过滤）。⚠️ **覆写了 `AgentStoppingDistance` 为 0.3** —— 否则基类默认 `attackRange×0.9 = 8.1` 会让 Agent 在 8 m 外就停下，kiting 直接失效。⚠️ **只触发 `Attack` 一个动画**（`Alert`/`Chase` 复用 `Idle`/`Run`，见 GDD §6.4.1） |
| ⭐ `EnemyStateMachine.cs` | （**M3.2 新增**）`EnemyFsmBase`（非泛型外壳）+ `EnemyStateMachine<TState>`：与 `PlayerFSM` 同风格的**三字典表驱动**状态机（`Register` / `Start` / `Tick` / `Change` / `LockStateMachine` / `Is`）。⚠️ **纯 C# 类，不继承 MonoBehaviour、不引用 UnityEngine** —— 既绕开「Unity 不支持泛型组件序列化」的坑，又能在无 Unity 环境跑自测 |
| ⭐ `EnemyAIController.cs` | （**M3.2 新增**）敌人 AI 共用层，**非泛型** MonoBehaviour：组件与数值、目标查找（每 1 秒重试）、受击反应（闪白 → 打断 / Boss 只闪白）、距离查询、**动画触发带参数存在性检查**（缺参数只警告一次，不刷屏）。`Update` 里驱动 `Fsm.Tick()` |
| ⭐ `EnemyLocomotion.cs` | （**M3.2 新增**）「交给 Agent 走」层，普通 C# 类：`ChaseTarget` / `MoveToOrStep` / `MoveTo` / `Stop` / `Resume` / `FaceTarget` / `FacePoint` / `Retreat` / `SyncWithNavMesh`。⭐ **没挂 `NavMeshAgent` 或没烘焙时自动退回直线位移**，烘焙后自动切换，**调用方代码不变**。⚠️ `MoveToOrStep`（M3.4 新增）是"Agent 优先、否则逐帧直线位移"的统一入口 —— 状态机只要算出目标点就调它，不必自己判断有没有 Agent |
| ⭐ `EnemyPerception.cs` | （**M3.3 新增**）视野感知组件：距离 + 扇形角度 + `Physics.Raycast` 遮挡；结果**按帧缓存**；遮挡层**自动排除自己与目标所在的层**；带 Scene 视图可视化。⚠️ 由 `EnemyAIController` 自动补挂（没挂就 `AddComponent`） |
| `Bullet` | 弹道。`Launch(velocity, owner)`、`lifeTime 3s` 自销毁、`Update` 里按速度位移、`OnTriggerEnter` 命中 `PlayerFSM` 造成伤害（`damage 10`），撞非 trigger 物体销毁；用 `IsChildOf(_owner)` 避免自伤 |

### 5.5 `Game.Gameplay`（关卡与流程）

> ⚠️ **2026-10-05 结构变更**：关卡由"单场景三房间"改为 **多关卡独立场景**（`Level_01/02/03`）。
> 下表四个类**机制不变、代码可平移**（每关一个战斗区）；
> ⚠️ **待新增** `LevelFlow`（关卡索引 / 解锁 / 加载 / 结算）—— 目前**不存在**。

| 类 | 职责 / 要点 |
|---|---|
| `RoomController` | 房间。`_entryDoor`、`_prevRoom`（用于校验上一房已清）、`isFinalRoom`、`_enemies[]`、`_hint`。`Start` 里**禁用所有敌人并关门**；`OnPlayerEntered()` 关门 + 提示 + **激活全部敌人并统计 `_alive`**；订阅 `EnemyHealth.Died` 递减，归零 → `FinishClear()`；若 `isFinalRoom` 则 `GameEvents.RaiseBossDied()`。状态：`Started` / `Cleared` |
| `RoomTrigger` | 触发器。`EMode { OpenEntryDoor, StartFight }`，`OnTriggerEnter` 判 `Player` tag |
| `DoorController` | 门。`Open()` / `Close()` 只是 `_body.SetActive()` 开关 |
| `GameManager` | 流程。单例；`Start` 重置 `Time.timeScale = 1` **并恢复输入**、记录 `_startTime`、隐藏两个面板；订阅 `GameEvents.PlayerDied/BossDied`；`_ended` 防重入（**公开为 `HasEnded` 供 `PauseMenu` 判断**）；死亡 → **立刻锁游戏性输入** + 延时 2s 显示 `gameOverPanel`，通关 → 写 `resultText`（`VICTORY! TIME:...s`）+ 延时 0.5s 显示 `victoryPanel`；面板显示时 `Time.timeScale = 0`；`RestartRun()` / `BackToMenu()`（都会先还原 `timeScale`）。⚠️ **`timeScale = 0` 拦不住输入**，故结束时会同时调 `InputService.SetGameplayInputEnabled(false)`（见 §6.4） |
| `CameraFollow` | 第三人称相机（类名与文件名一致 ✅）。机位算法：焦点 = 角色 + `_focusHeight`（脚本默认 1.0，**场景实际 1.5**），机位 = 焦点**沿视线后退** `_distance(3)` → 视线必然穿过角色、**角色永远居中**；基准俯角 `_pitch`（默认 **30°**；⚠️ 2026-10-05 由 40° 调低），**鼠标可上下偏移（含仰视）**，上限 `_maxPitch`（默认 **60**，由 80 调整）；滚轮缩放 1.5~10、`_smoothTime 0`（硬跟随）；⚠️ **`_followTargetYaw` 必须为 false**（相机 yaw 归鼠标，若跟随角色朝向会与"移动相对相机 + 角色转向移动方向"构成**正反馈 → 按 WASD 视角持续旋转**）；开局 `SnapToTarget()` 瞬移到位；`HandleCursor()` 在游戏性输入关闭时**只解锁指针、不锁回**（否则结算/暂停面板点不动）。详见 `ARPG-DIRECTION.md` §4.6 |

> **⚠️ 相机俯角：可仰视 + 动态「地面安全角」（2026-10-05）**
> 此前 `_minPitch = 5`（正值）把俯角锁死在"永远俯视"，鼠标上推到底卡在 5°。
> 现在改为：`_minPitch = -60`（负值 = 允许仰视），**真正的下限由地面安全角自动收窄** ——
>
> ```
> 机位高度 = focusY + sin(pitch) × distance ≥ groundY + clearance
> → pitch ≥ asin((groundY + clearance − focusY) / distance)
> ```
>
> ⚠️ **安全角必须随距离变化**（距离越远允许的仰角越小）：
> 1.5 m → −27.8°；**3 m → −13.5°**；5 m → −8.1°；10 m → −4.0°。
> 写死常数会在拉远时穿地。
>
> ⚠️ **实现要点：钳制「俯角」而非「机位高度」** ——
> 钳制机位高度会压缩相机距离、且必须改用 `LookAt` 才能让角色居中；
> 钳制俯角则保持距离不变，机位仍从焦点沿视线推出 → **角色继续自动居中**（实测点积 = 1.000000）。
>
> ⚠️ 地面探测**必须忽略角色自身碰撞体**（否则射线打到自己胶囊体，实测天真写法命中 `Player @ y=1.6`）。
> ⚠️ 仍未做：防穿墙、极端仰角+最近距离时相机贴脸。

> `PauseMenu` 属 `Game.UI` 命名空间，见 §5.8。

### 5.6 `Game.AI`

| 类 | 职责 |
|---|---|
| `AStar` | 网格 A*。`Node { X, Y, Walkable, G, F, Parent }`；4 邻域；`FindPath(grid, start, goal)` 返回 `List<Node>` 或 `null`；曼哈顿启发式。⚠️ **仅在编辑器自测中被调用，未接入游戏逻辑** |

### 5.7 `Game.Fx` / `Fx`

| 类 | 职责 |
|---|---|
| `HitFxSystem` | 打击特效总控。`Awake` 建两个 `ObjectPool`（`HitSpark` 预热 10、`DamagePopup` 预热 8）；`Play(point, damage)` 弹出火花 + 飘字，并在结束时 `Release` |
| `HitSpark` | 火花。`Show(pos, onDone)` 面向相机缩放 + 淡出（0.18s 协程） |
| `DamagePopup` | 伤害飘字。用 **UGUI `Text`**（非 TMP），`Show(text, worldPos, onDone)`，`WorldToScreenPoint` 定位，上飘淡出（0.7s） |

### 5.8 `Game.UI`

| 类 | 职责 |
|---|---|
| `HPBar` | 订阅 `PlayerHealth.OnHpChanged`，用 `anchorMin/anchorMax` 拉伸 `_fill` |
| **`ManaBar`** | 魔力条 HUD（2026-10-06 由 `EnergyBar` 改名，GUID 未变）。挂在 `Canvas/ManaBarBack`（填充块 `ManaFill`），订阅 `PlayerMana.OnManaChanged` 更新填充、`OnManaSpendFailed` 触发闪烁。⚠️ 闪烁用 `WaitForSecondsRealtime`（暂停时不能卡在中间态） |
| `RoomHintText` | TMP 文本，`Show(msg)` 显示 3s 后清空（协程）。⚠️ 文案英文硬编码（见 §9 T20） |
| `MainMenu` | `StartGame()` 加载 `Game` 场景 / `QuitGame()` |
| `PauseMenu` | 暂停菜单（**M1.3 新增**）。`ESC` 开/关；暂停时**两道闸同时上**：`Time.timeScale = 0` + `InputService.SetGameplayInputEnabled(false)`；`Resume()` 还原暂停前的 `timeScale` 并恢复输入；幂等；`GameManager.HasEnded` 时忽略 ESC。⚠️ **组件挂在 Canvas 上、不在 PausePanel 内** —— 否则 `SetActive(false)` 会连带禁用脚本，菜单再也打不开。⚠️ `ESC` 由 `InputService.PausePressedThisFrame` 提供且**刻意不受输入闸门影响**（否则暂停后关不掉菜单）。UI 用**复制 `GameOverPanel`** 搭建（字体/按钮样式/过渡自动继承） |

### 5.9 `_Game/Scripts/Tools`（⚠️ 遗留，见 §9）

| 类 | 状态 |
|---|---|
| `PlayerInput` | ❌ 旧 `Input.GetAxisRaw` 方案，已被 `InputService` 取代，无引用 |
| `EnemyMelee` | ❌ 旧近战敌人（W7 方案），与 `EnemyMeleeAI` 重复；实现 `IDamageable`，发布 `EnemyDiedEvent` |
| `EnemyConfig` | ❌ 配套旧配置（`maxHp/moveSpeed/chaseRange`），有 `.asset` |
| `EnemyDiedEvent` | ❌ 旧事件结构体（被 `EnemyMelee` 与 `EnemyDeathLogger` 使用） |
| `EnemyDeathLogger` | ❌ 订阅 `EnemyDiedEvent` 打日志的调试器 |
| `Target` | ❌ 测试靶子（`hp = 10000` 打不死），实现 `IDamageable` |
| `FieldInspector` | ⚠️ 反射打印字段的工具（被 `Editor/FieldInspectorMenu.cs` 引用，**保留**） |
| `_DebugCheats_w7` | ⚠️ **调试作弊键，挂在 Player 上**：`J` 扣 10 血、`E` 扣 30 蓝、`K` 秒杀最近敌人。脚本注释自述"周末收口时删除" |

### 5.10 `_Modules`（教学练习，与游戏无关）

`AsyncDemo`、`CoroutineDemoA`、`DashLogger`、`EnemyRow`、`Hero`、`LearnLab`、`LifecyclePrinter`、`PlayerMove`、`PoolLab`、`PoolProbe`、`SerializationProbe` — 共 11 个，296 行。均为单文件教学 Demo，**不在任何场景中**。

### 5.11 `Editor`

| 类 | 职责 |
|---|---|
| `AStarSelfTest` | 菜单 `Tools/A* 自测` 与 `Tools/A* 自测2：无路`。两个用例：① 5×5 网格绕障找路并校验不穿墙；② 整列堵死应返回 `null` |
| `FieldInspectorMenu` | 菜单入口，对选中物体调 `FieldInspector.Dump` |
| `HitboxAnimationValidator` | **M1.2 新增**。菜单 `Tools/Hitbox/校验攻击动画事件`：扫描 `Art/Animations` 下所有剪辑（含 FBX 内嵌、过滤 `__preview__`），列出其动画事件并标记是否含判定事件（`OnAttackHit` / `TickAttack`）。**用途**：判定体靠动画事件开关，**漏挂事件的后果是该招式完全没有伤害且在编辑器里看不出来** |

---

## 6. 运行时链路（读代码时最容易迷路的地方）

### 6.1 场景启动

**MainMenu 场景**：`MainMenuManager`（挂 `MainMenu.cs`）→ 点击 `Button_Start` → `SceneManager.LoadScene("Game")`。

**Game 场景根对象**（13 个）：`Main Camera`(CameraFollow)、`Directional Light`、`Global Volume`、`Ground`、`InputService`、`Canvas`(7 个子物体)、`EventSystem`、`FxSystem`(HitFxSystem)、`Player`、`Room1`、`Room2`、`Room3`、`GameManager`。

**Player 身上的组件**（顺序即场景中的顺序）：
`Transform, Animator, PlayerMotor, PlayerDash, AttackFxBridge, CapsuleCollider, PlayerAttack, PlayerHealth, PlayerFSM, PlayerEnergy, Rigidbody, _DebugCheatsW7, CharacterController`
> ⚠️ 注意：**Rigidbody + CapsuleCollider + CharacterController 三套物理组件并存**。

**Canvas 子物体关键词**：`HPBarBack`/`HPFill`、`ManaBarBack`、`ResultText`、`VictoryOverPanel`/`VictoryMsgText`、`GameOverPanel`/`DeathMsgText`、`RestartButton`、`MainMenuButton`、若干 `Text (TMP)`。

### 6.2 战斗事件流（M1.2 后为**判定体驱动**）

**玩家攻击**
```
玩家按攻击 → InputService.AttackPressedThisFrame
   → PlayerFSM.UpdateNeutral 检测到 → TryAttack()（2026-10-06 起普攻免费，无资源门槛）→ Change(Attack)
   → _enter[Attack]: PlayerAttack.StartCombo() + Animator.SetTrigger("Attack")
   → 动画播到关键帧 → Animation Event: PlayerAttack.OnAttackHit()
       · SyncHitboxDamage() 把**该段伤害**写入判定体
       · HitboxController.EnableHitbox("Hitbox_Attack{n}", 0.25s)
   → Hitbox（Trigger + kinematic Rigidbody）在时间窗内真实存在
   → OnTriggerEnter → HitboxController.TryProcessHit()  ← **唯一命中出口**
       · 层过滤（碰撞矩阵已保证，代码再挡一层）→ 找 IDamageable → TakeDamage
       · 广播 HitboxController.OnHit
   → AttackFxBridge → HitFxSystem.Play() → 火花 + 飘字（对象池）
   → AttackStateBehaviour(OnStateExit) → SetAttacking(false) → FSM 判定攻击结束回 Idle/Run
```

**敌人攻击**（与玩家**同规则**，GDD 设计支柱 4）
```
动画关键帧 → Animation Event: EnemyMeleeAI.TickAttack()
   → HitboxController.EnableHitbox("Hitbox_Attack", 0.25s)   （敌人判定体 layer=EnemyHitbox，只碰 Player）
   → 命中 → 同上唯一出口 → PlayerFSM.TakeDamage(damage)（内部判 Invulnerable）
   → Change(Hit) 或 Change(Death)（血量≤0）
```

**敌人受击反应（T21，M1.2 接通）**
```
EnemyHealth.TakeDamage() → 广播**实例事件** Damaged(自身, 伤害)
   → EnemyMeleeAI.OnDamaged()
       · 闪白（所有敌人，用 MaterialPropertyBlock，不产生材质实例）
       · 若 canBeInterrupted：关判定体（打断攻击）+ 击退 + 进 Hit 硬直
       · 若 !canBeInterrupted（Boss）：**只闪白**
   → 血量≤0 → Died（static 事件，见 §9 T7）→ RoomController 计数
```

**暂停 / 结算时的输入闸门（M1.3）**
```
Time.timeScale = 0   ← 冻结逻辑与动画
   ⚠️ 但**拦不住输入**：Update() 仍每帧跑、Input System 不受 timeScale 影响
InputService.SetGameplayInputEnabled(false)   ← 这才拦住鼠标转视角
   · 只 Disable Player action map（UI map 保留，否则菜单点不动）
   · CameraFollow.HandleCursor() 顺势解锁指针（否则面板点不动）
   · 例外：ESC 走 PausePressedThisFrame，**刻意不受闸门影响**（否则暂停后关不掉菜单）
```

### 6.3 房间与胜负流

```
玩家踏入 FightTrigger1 → RoomTrigger.OnTriggerEnter(tag=="Player") → RoomController.OnPlayerEntered()
   → 关门 + RoomHintText.Show("Clean The Room") + 激活全部敌人（_alive 计数）
敌人死亡 → EnemyHealth.Die() → Died(this) 静态事件 → RoomController.OnEnemyDied → _alive--
   → _alive<=0 → FinishClear(): 提示 "Door Open"；(isFinalRoom 时) GameEvents.RaiseBossDied()
GameEvents.BossDied → GameManager.OnVictory → 写 ResultText + 显示 VictoryOverPanel
GameEvents.PlayerDied → GameManager.OnPlayerDied → 2s 后显示 GameOverPanel
面板按钮 → RestartRun()（重载当前场景）/ BackToMenu()（加载 MainMenu）
```

### 6.4 动画事件清单（实测结果，工具：`Tools/Hitbox/校验攻击动画事件`）

| 动画文件 | 事件名 | 时间 | 接收者 |
|---|---|---|---|
| `Player/combo_01_1.anim` | `OnAttackHit` | 0.33 s | `PlayerAttack` |
| `Player/combo_01_2.anim` | `OnAttackHit` | 0.23 s | 同上 |
| `Player/combo_01_3.anim` | `OnAttackHit` | 0.33 s | 同上 |
| `Player/combo_01_4.anim` | `OnAttackHit` | 0.53 s | 同上 |
| `Player/Elbow Uppercut Combo.anim` | `OnAttackHit` ×2 | 0.87 / 1.33 s | 同上 ⚠️ |
| `Player/Upward Thrust.anim` | `OnAttackHit` | 1.00 s | 同上 ⚠️ |
| `Enemy/combo_01_1.anim` | `TickAttack` | 0.30 s | `EnemyMeleeAI` |

> ⚠️ **M2 重要提醒**：`Elbow Uppercut Combo` 与 `Upward Thrust` **也挂了 `OnAttackHit`**，
> 但它们**不属于当前 4 段普攻**。若日后切到这些动画（例如 M2 的特殊攻击），
> 会走到 `PlayerAttack.OnAttackHit` 并按当时的 `_comboIndex` 开启**普攻判定体** ——
> **M2 做特殊攻击时必须让它们用自己的判定体**，否则会误开普攻判定。
>
> 其余动画（`Idle` / `Standard Run` / `Stomach Hit` / `Sword And Shield Death` / `Standing Dive Forward` 等）**无事件**，属正常。

### 6.5 动画状态机现状

- **`PlayerAC.controller`**（参数：`speed`, `Dash`, `Attack`, `Hit`, `IsDead`）
  状态：`Locomotion`（Blend Tree，按 `speed`）、`Attack1`、`Attack2`、`Attack3`、`Attack4`、`Dash`、`Hit`、`Death`（共 8 个）
- **`AC_Monster1.controller`**（参数：`Speed`(Float) / `Attack`(Trigger) / `Hit`(Trigger) / `IsDead`(Bool)）
  状态：`idle` / `flyinplace` / `attack1` / `hit` / `death`（共 **5** 个）
- **`AC_Monster2.controller`**（同上 4 个同名参数）
  状态：`idle` / `flyinplace` / `taunt` / `hit` / `deathspecial`（共 **5** 个）
- ⚠️ **`EnemyAC.controller` 已弃用**（在 `Art/Animations/Enemy/old/` 下，只有 `idle` + `combo_01_1` 两状态、参数只有 1 个 `Attack`），
  保留只为对照；两套怪物控制器**参数完全同名**，所以 AI 代码对两者一视同仁。

---

## 7. 美术资产现状（对改造很关键）

| 资产 | 路径 | 关键事实 |
|---|---|---|
| **`OVR - Roskva.fbx`** | `Art/characters/Roskva/_model/fbx/` | **背剑版玩家模型**（UE 素材包接入）。`animationType = Human` ✅ / `isHuman` ✅ / 9 个网格（Body / Equips / Eyeshadow / Face / Fur / Hair / Hair2 / Lower Body / Upper Body）。⚠️ **不含独立剑网格** |
| **`OVR - Roskva_Sword.fbx`** | 同上 | **持剑版玩家模型**（2026-10-05 接入，用于战斗关卡）。✅ **已改 Humanoid**（`animationType: 3` / `avatarSetup: 1`），**55 项 human 映射与背剑版逐项一致**。**10 个网格** = 上述 9 个 + **`Roskva_Sword_Hand`**（416 三角面 / 273 顶点）。**7 个材质**；⚠️ 其中剑网格有 **2 个材质槽**：槽0 `NPC_Roskva_Parts`（**剑首 + 剑柄 + 护手**，走 Equips 贴图，四段 UV 区分外观）、槽1 `NPC_Roskva_Sword_Blade`（剑身，抛光钢）。✅ **剑已有 UV**（2026-10-05 自动生成，**四段**：剑首/剑柄/护手/剑身） |
| `OVR - Roskva_SwordInHand.fbx` | 同上 | **2026-10-05 起成为「玩家原生动画」的来源**（替代已删除的 `OVR - Roskva_Animated.fbx`）。✅ Humanoid（`animationType: 3` / `avatarSetup: 1`），内含 **5 段动画**：`Idle01` 37.33s / `Talk01_old` 29.33s / `Talk02` 33.33s / `Talk03` 37.33s / **`Walk` 2.47s**，全部 `isHumanMotion = True`。⚠️ 其 Avatar 为 **54 项映射、缺 `Neck`**（见 T26）。⚠️ `Idle01` 已提取为独立 `.anim` 并接进 `Locomotion` 混合树；`Talk*`/`Walk` 也已挂为 `PlayerAC` 状态（尚未接入玩法逻辑） |
| `Mat_Roskva_Sword_Blade.mat` | `Art/characters/Roskva/Materials/` | **2026-10-05 新建**。剑材质：URP Lit，抛光钢（基色 0.82/0.85/0.90、金属度 1、光滑度 0.8），**无贴图**（剑网格无 UV，纯色材质正合适） |
| `Equips_NoSword.asset` | `Art/characters/Roskva/Meshes/` | **去掉剑的装备网格**（顶点 4516 / 27 骨骼 / 材质 `Mat_Roskva_Parts`）。⚠️ 该网格由已失效的 `RoskvaSwordRig` 工具生成，当前剑已改由 `OVR - Roskva_Sword.fbx` 提供，此资产的用途待重新评估 |
| Roskva 贴图与材质 | `Art/characters/Roskva/{_textures,Materials}/` | 含 `_urp` 子目录；`Mat_Roskva_Hair*` 已接入**金发变体**（`T_Roskva_Hair_Gold`，接在 `_BaseMap` 基础色槽，sRGB 正确） |
| 握持挂点 | Roskva 模型内 | `B_Weapon_L` / `B_Weapon_R`（武器模型挂点，`WeaponConfig.gripBoneName` 默认取 `B_Weapon_R`） |
| TKDstyle_AnimSet | `Assets/TKDstyle_AnimSet/` | **第三方**武术动画包，项目动画的来源之一（⚠️ 署名要求，见 §9 T17；⚠️ **已排除出版本控制**，见 §2.1） |
| Magical-Knight_Set | `Assets/Magical-Knight_Set/` | **第三方** Humanoid 动作包（100+ 动作，790 MB）—— **M2.3 连段动画的来源**（`1_atk_sword03` 等已抽出为独立 `.anim`）（⚠️ **已排除出版本控制**，见 §2.1） |
| Rapier_Anim_Set | `Assets/Rapier_Anim_Set/` | **第三方** Humanoid 动作包（793 MB）—— **当前未使用**（⚠️ **已排除出版本控制**，见 §2.1） |
| PlayerAC / AC_Monster1 / AC_Monster2 | `_Game/Art/Animations/` | 三个 Animator Controller（玩家 1 + 敌人 2；敌人两套参数同名） |
| 敌人预制体 | `_Game/Prefabs/Enemy/` | **`Monster1`（近战现役，已含 `Hitbox_Attack`）**、**`Monster2`（远程现役，已含 `FirePoint`）**、`Boxer` / `Gunner`（旧模型，avatar 为空已弃用）、`Enemy_Slime`、`Bullet` |

> ✅ **最重要的一条**：玩家角色与动画 FBX **都是 Humanoid**。
> 这意味着**新角色外观（模型）可以复用现有全部动画**（Unity Humanoid 重定向），不必为每个新角色重做动作。
> 这是整个美术方案的技术前提，详见 `ART-PIPELINE.md`。
>
> ⚠️ **Humanoid 映射的验证方法**：不要只看 `avatar.isValid` ——
> **`Neck` 属可选槽位，缺失时 Avatar 仍然 `isValid = true`**（这正是 T25 能悄悄漏掉的原因）。
> 必须用 `animator.GetBoneTransform(HumanBodyBones.X)` 逐个抽查关键骨骼。
> 另一个坑：`humanDescription.human[]` 为**空数组**时 Unity 会全自动映射；
> 一旦改成**显式列表**，Unity **不会自动补齐未列出的槽位**。

---

## 8. 约定与风格（改代码时请遵守）

1. **中文注释**，行内注释用 `//` 简短说明意图。
2. 配置数值一律走 **ScriptableObject**，不硬编码。
3. 事件优先用 `GameEvents`（静态门面）或 `EventCenter<T>`（类型化总线）。
4. 复用 `ObjectPool<T>` 而不是频繁 `Instantiate/Destroy`（弹道/特效）。
5. `InputService` 是输入的**唯一入口**，不要在业务代码里直接读 `Input.*`。
6. **文件名必须与类名一致**（Unity 对 MonoBehaviour 的硬要求）—— 现有两处违反，见 §9 T9/T10。
7. 一次性性能敏感代码（每帧）避免 LINQ 分配。

---

## 9. 已知问题清单（改造时优先处理）

> 编号 T1–T10 在 `GDD.md` §13.2 有对应的改造方案，两处编号一致。

| # | 问题 | 位置 | 影响 |
|---|---|---|---|
| **T1** | `_motor.enabled = false` 用于"停止移动"，**重力也被停掉** | `PlayerFSM` / `PlayerMotor` | ⚠️ **已降级为低优先级**：跳跃已取消，不再阻塞任何功能（代码仍不优雅） |
| **T2** | 玩家同时挂 `Rigidbody` + `CapsuleCollider` + `CharacterController` | `Player.prefab` / 场景 Player | 三套物理重复，易互相干扰 |
| ~~T3~~ | ~~攻击判定是动画事件里的**瞬时 `OverlapSphere` 采样**~~ | ✅ **已修复 2026-10-04（M1.2）**：改为帧驱动的判定体系统（`Hitbox` + `HitboxController`），判定体在关键帧期间**真实存在**；伤害统一走 `HitboxController.TryProcessHit` 唯一出口 | 已消除 |
| ~~T4~~ | ~~体力**从未被消耗**（唯一调用点在作弊脚本）~~ | ✅ **已修复 2026-10-04（M1.1）**：成本表集中在 `PlayerEnergy`，接入 3 个消耗点（冲刺 / 普攻首段 / 连段后续），体力不足则**硬性拒绝动作**；加了再生与 HUD 闪烁 | 已消除 |
| ~~T5~~ | ~~`PlayerConfig` 同时存角色数值**与**攻击数值~~ | ✅ **已修复 2026-10-04（M2.1）**：攻击数值（伤害 / 连段窗口 / 段数 / 判定盒尺寸 / 每段体力消耗 / 特殊攻击）全部迁到 `WeaponConfig`；`PlayerConfig` 只保留**角色自身**属性（移动、生命、体力上限与再生），旧字段降级为"未装配武器时的兜底" | 已消除 |
| ~~T6~~ | ~~敌人无共用状态机基类（近战 switch 硬编码 / 远程完全没有）~~ | ✅ **已修复 2026-10-06（M3.2）**：新增 `EnemyStateMachine`（**纯 C#** 三字典表驱动）+ `EnemyAIController`（**非泛型**共用层）+ `EnemyLocomotion`（移动分层），`EnemyMeleeAI` 已迁移。⭐ 验收达成：**加第三种敌人只剩"建表 + 数值"** —— 自测里弓兵的 `走位→瞄准→射击` 循环用**同一套骨架**跑通，未新增一行状态机代码 | 已消除（⚠️ `EnemyRanged` 本体待 M3.5 重建后接入） |
| **T7** | `EnemyHealth.Died` 是 **static 事件** + 房间常驻订阅 | `EnemyHealth` / `RoomController` | 跨场景重开有泄漏/重复订阅风险 |
| **T8** | `PlayerHealth.Died` 事件与 `Die()` **被注释掉（w6）**，从未触发 | `PlayerHealth` | 死亡链路不统一，易出隐性 bug |
| ~~T9~~ | ~~文件名 ≠ 类名~~ | ✅ **已修复 2026-10-04**：`PlayEnergy.cs`→`PlayerEnergy.cs`、`CameraMove.cs`→`CameraFollow.cs`、`ManaBar.cs`→`EnergyBar.cs`（类名同步，`git mv` 保 GUID 引用不断） | 已消除 |
| ~~T10~~ | ~~调试作弊键留在正式场景~~ | ✅ **已修复 2026-10-04**：组件已从 Player 移除，`_DebugCheats_w7.cs` 已删除 | 已消除 |
| ~~T11~~ | ~~通关文案拼写错误 `VICTPRY!`~~ | ✅ **已修复 2026-10-04**：改为 `VICTORY`。⚠️ **另发现第二处不同的拼写错误**：`VictoryOverPanel/VictoryMsgText` 原为 `VECTORY !`，已在 M1.3 一并改为 `VICTORY !` | 已消除 |
| ~~T12~~ | ~~面板弹出时游戏不暂停~~ | ✅ **已修复 2026-10-04**：结算时置 `Time.timeScale = 0`，延时改用 `WaitForSecondsRealtime` | 已消除 |
| ~~T13~~ | ~~`WeaponConfig` 命名空间误写 `Game.Date`~~ | ✅ **已修复 2026-10-04**：文件已删除（M2.1 重建） | 已消除 |
| T14 | `AStar` **未接入游戏逻辑**，仅编辑器自测 | `Game.AI` / `Editor` | ✅ **已结案（2026-10-04 翻转）**：寻路改用 **Unity 自带 NavMesh**（`com.unity.ai.navigation` 2.0.14 已装）。理由：`AStar.FindPath` **不重置邻居节点的 `G/F/Parent`**，复用同一网格重算会因残留 `Parent` 成环导致 `Reconstruct` **死循环卡死编辑器**——而"每 0.3 s 重算"必须复用网格，**不加 Reset 就不能接入**；且自建网格烘焙/障碍标记/路径平滑/动态避障四件事 NavMesh 已内置。→ `AStar` + `AStarSelfTest` **保留不动**，降级为**报告"路径搜索算法实现"章节的素材**（含两个测试用例）。详见 `NAVMESH-GUIDE.md` |
| ~~T15~~ | ~~`Tools/` 下 6 个脚本为旧方案死代码~~ | ✅ **已修复 2026-10-04**：已全部删除（7 个 .cs）；**删除前先移除了 `Enemy_Slime.prefab` 上的 `EnemyMelee` 组件**，场景 Missing Script = 0 | 已消除 |
| ~~T16~~ | ~~`_Modules/` 11 个教学脚本~~ | ✅ **已修复 2026-10-04**：整个目录已删除 | 已消除 |
| **T22** | **`PlayerDash` 开局白送 0.5s 无敌**：无敌计时被初始化成配置值（0.5）且 `Update` 每帧递减 → 开局 0.5 s 内 `IsInvulnerable` 为真 | `PlayerDash` | ✅ **已修复 2026-10-04**：改用运行时计时 `_iFrameTimer`，初值 0 |
| **T23** | **`EnemyMeleeAI` 每帧 `SetTrigger("Attack")`**（写在 `Update` 里）→ 攻击动画不断被重置 | `EnemyMeleeAI` | ✅ **已修复 2026-10-04**：移到 `SetState`，进入 Attack 时触发一次。⚠️ 2026-10-06（M3.2）起该方法已不存在，等价逻辑在表驱动状态机的 **`EnterAttack`（enter 表项）** 里 |
| **T24** | 多处 **NRE 风险**：`HitFxSystem`（预制体未连线）、`AttackFxBridge`（fx 未赋值）、`DamagePopup`（无 Text / 无 Camera.main）、`HPBar`/`EnergyBar`（`_fill` 为 null）、`RoomHintText`（`_text`） | 多处 | ✅ **已修复 2026-10-04**：均已补 null 保护（对应 T19） |
| T17 | `TKDstyle_AnimSet` 为第三方资源包 | `Assets/` | 提交/演示需注意署名与授权 |
| ~~T25~~ | ~~Avatar 缺少 `Neck` 映射~~ | ✅ **已修复 2026-10-04**：`human[]` 项数 **54 → 55（完整）**，`Neck -> Bip001-Neck`；端到端抽检 11 个关键骨骼全部解析成功。成因备查：`human[]` 原为空数组（Unity 全自动映射，含 Neck），改为**显式列表**时漏掉 Neck —— **显式列表不会自动补齐未列出的槽位** | 已消除（⚠️ 见 T26） |
| **T26** | **动画来源 FBX 的 Avatar 缺 `Neck`**（`human[]` 54 项），而玩家 Avatar 是 55 项 | **`OVR - Roskva_SwordInHand.fbx`**（2026-10-05 起，替代原 `Roskva_Animated.fbx`） | ⚠️ 两者不一致 → 用其 `Idle01` / `Talk*` / `Walk` 时**颈部动作不会重定向**（对走路循环影响很小，但 `Talk*` 是**对话动画，颈部不参与会比较僵**）。修法同 T25：Rig → Configure 把 `Neck` 映射到 `Bip001-Neck`。**建议在正式使用 `Talk*` / `Walk` 前补掉** |
| ~~T27~~ | ~~`PlayerDash` 用 `transform.position +=` 位移，**绕过 `CharacterController`** → 冲刺穿墙~~ | ✅ **已修复 2026-10-06**：冲刺位移改走 `PlayerMotor.StepMoveGrounded()` → `CharacterController.Move()`（**位置只有一个写入者**：`PlayerMotor`），贴墙起手会在墙面外一个胶囊半径处停住。⚠️ 附带实测发现：**编辑模式下 `CharacterController.Move` 完全不生效**（冲 4 米实测位移 −0.83 米），所以这类位移只能用 **Play 模式**验证 | 已消除（⚠️ 真实手感需 Play 确认） |
| T18 | ~~无暂停菜单~~（✅ 已做，M1.3）/ **无音频**（`Audio/` 空）、**无存档**、**Boss 实体未做** | 多处 | V1 缺口：音频与存档待 M5；Boss 待 M4 |
| T19 | `HitFxSystem` 用 `sparkPrefab`/`popupPrefab`，若未赋值 `Awake` 会 NRE | `HitFxSystem` | 场景里已连线，风险低 |
| T20 | `RoomHintText` 文本为英文硬编码（"Clean The Room" / "Door Open"） | `RoomController` / `RoomHintText` | 若要中文化需改这里 |
| ~~T21~~ | ~~敌人受击反应链路断裂~~ | ✅ **已接通 2026-10-04（M1.2）**：`EnemyHealth` 新增**实例事件** `Damaged`（非 static，避免 T7 隐患）；`EnemyMeleeAI` 订阅后实现硬直 + 闪白 + 击退；**闪白改用 `MaterialPropertyBlock`**（原先 `.material.color` 会实例化材质）。按 `EnemyAIConfig.canBeInterrupted` 区分：小怪会打断、**Boss 只闪白**（设 false） | 已消除 |

---

## 10. 当前进度

### 已完成（可运行）

**原有基础**
- ✅ 开始界面（标题 + 开始 + 退出）
- ✅ 玩家：移动、冲刺（含无敌帧）、4 段连段攻击、受击/死亡、生命条
- ✅ 3 房间清怪流程（锁门/开门/提示）
- ✅ 胜利 / 失败结算面板 + 重开 / 回主菜单
- ✅ 近战敌人 AI（基础五状态）+ 远程敌人（无状态机）
- ✅ 打击特效（火花 + 伤害飘字，对象池驱动）
- ✅ A\* 实现 + 编辑器自测
- ✅ 编译零报错零警告

**阶段 0 · 开工前置（2026-10-04）**
- ✅ 5 个层已建、输入补 `Special` 并删冲突的 `Jump`、`PlayerConfig` 补齐体力与受击无敌字段、调试作弊键移除
- ✅ 全项目脚本清理：脚本 50 → 37（删 19 个死代码）；修 4 个真 bug（T22–T24）

**M1 · 系统骨架（2026-10-04）—— 全部完成**
- ✅ **M1.1 体力统一出口**（`845fece`）：成本表集中在 `PlayerEnergy`，接入 3 个消耗点（冲刺/普攻首段/连段后续），体力不足**硬性拒绝动作**，含再生与 HUD 闪烁
- ✅ **M1.2 Hitbox 系统**（`4593136`/`8596c11`/`59ee4e0`/`e5f874d`）：归层 + 碰撞矩阵、`Hitbox` + `HitboxController`、玩家 4 段判定体、敌人判定体、**T3 + T21 结案**、动画事件校验工具
- ✅ **M1.3 暂停菜单**（`b7add1c`）：`ESC` 开/关、冻结时间 + **锁游戏性输入**（`timeScale` 拦不住鼠标）、继续/重开/回主菜单
- ✅ **M1.4 修文案**：`VICTPRY`（阶段 0）+ **`VECTORY`**（M1.3，第二处不同的拼写错误）

**M2 · 武器（剑）—— 全部完成 ✅** ⚠️ 范围收敛为「只做剑」
- ✅ **M2.1 WeaponConfig 重做**（`41472f2`）：`WeaponConfig` + 剑/长枪两份资产；伤害/连段窗口/段数/**判定盒尺寸**/体力成本全部接入
- ✅ **M2.2 装配剑**（`Weapon_Sword` 已挂 `Player1`/`Player2` 的 `PlayerAttack._weapon` + `PlayerEnergy._weapon`）
- ✅ **M2.3 剑的模型 + 4 段连段动画**（**用户已接入**：`1_atk_sword03`/`2_atk_sword04`/`3_atk_sword05`/`4_atk_sword01`，素材来自工程内 `Magical-Knight_Set`）
- ✅ **M2.4 特殊攻击 —— 火球 · 远距离 · 命中爆炸**（🔄 形态改定后已实现：`Fireball.cs` + `Fireball.prefab` + `SpecialStateBehaviour.cs`；`PlayerState` 加 `Special`；`PlayerAC` 加参数与转场）。**Play 实测**：扣 30 体力 ✓、冷却 3 s 生效 ✓、火球生成 ✓、爆炸扣 30 血 ✓、不误伤自己 ✓
  → ⭐ **M2 全部收口**。⚠️ **未验证**（受工具限制）：实际按键手感、火球**飞行途中**命中、`Special_Attack` 播完自动回 Idle

**M3 · 敌人 + 寻路（进行中）**
- ✅ **M3.1 NavMesh 烘焙 + 障碍物**（2026-10-06）：`NavMeshRoot`（`NavMeshSurface`）+ `Obstacles` 下 6 个柱子 + 3 扇门挂 `NavMeshObstacle(Carving)` + `Boxer`/`Gunner` 挂 `NavMeshAgent`；烘焙产物 `Assets/Scenes/NavMesh-Game.asset`（**580 ms / 651 三角形 / 73 KB**）
- ✅ **M3.4 剑兵（Alert / Reposition）**（2026-10-06）：`EnemyMeleeAI` 状态数 5 → **7**。
  `Alert` = 站住 + `FaceTarget` + 停顿 `alertTime`（0.35 s）；`Reposition` = 攻击后沿"背离玩家"方向退开 `repositionDistance`（1.5 m），
  目标点经 `NavMesh.SamplePosition`（半径 0.3）吸附，出口有三个：**到位（<0.25 m）/ 超时（`distance÷speed×2`）/ 玩家又贴脸**。
  `EnemyLocomotion` 新增 `MoveToOrStep`（Agent 优先、否则逐帧直线位移）；`EnemyAIConfig` 新增 `alertTime` / `repositionDistance`。
  ⚠️ 两个"刻意为之"：① **不在 enter 回调里调 `Change`**（会递归），`repositionDistance = 0` 的分支放在 `UpdateAttack` 里判；
  ② `EnterReposition` 后**不调 `FaceTarget`** —— Agent 走位时朝向归它管，强行接管会抖动（表现为"转身走开"而非"面朝玩家倒退"）。
  **自测 30 项全通过**（原 26 项 + 剑兵循环新增 4 项）。⚠️ **没有动画**（`EnemyAC` 只有 2 个状态，刻意不调 `SetTrigger` 以免刷警告）
- ✅ **M3.2 敌人状态机基类**（2026-10-06）：`EnemyStateMachine.cs`（纯 C# 三字典表驱动）+ `EnemyAIController.cs`（非泛型共用层）+ `EnemyLocomotion.cs`（移动分层，**无 Agent 时自动退回直线位移**）；`EnemyMeleeAI` 已迁移。**自测 26 项全通过**
- ✅ **M3.3 视野感知**（2026-10-06）：`EnemyPerception.cs`，距离 + 扇形 + 射线遮挡，按帧缓存；参数由 `EnemyAIConfig` 统一下发；剑兵 `Idle → Chase` 已接入
  → ⚠️ **一处待实机确认**：`viewAngle` 默认 160°，若敌人"不追人了"就调成 360（一个字段退回纯距离感知）
  → ⚠️ **M3.1 的门的动态 Carving 仍需在 Play 模式确认**（编辑模式下 Carving 更新时机不可靠）

**其他并行进展**
- ✅ **攻击手感优化（2026-10-06）**：攻击中**不移动**、但**可限速转向**（`PlayerMotor.attackTurnSpeed = 180 °/s`）——
  改动集中在 `PlayerMotor.SetFacingOnly(bool)` + `PlayerFSM._enter/_exit[Attack]`；顺带修掉"攻击期间贴地下压被中断"。
  ⭐ **采用方案 B（转向窗口）**：命中帧前可转、出手后锁定（判据 `PlayerAttack.CanTurn`），
  ⚠️ 窗口很短（Attack1 只 0.233 s），180 °/s 下最多转 42° → **建议调到 300~360**；
  把 `PlayerMotor.gateTurnByHitFrame` 取消勾选即退回方案 A。
  ⚠️ 手感数值与"鼠标转视角会带着角色转"这两点**需 Play 校准**
- ✅ 相机改造完成：鼠标控视角 + 后置跟随（基准俯角 **30°**、**可仰视**、动态地面安全角防穿地）；**游戏结束后不能转视角**（输入闸门）
- ✅ 玩家模型换为 **Roskva**（Humanoid，**T25 Neck 已修**）；`Equips_NoSword` 无武器网格已接入
- ✅ `OVR - Roskva_Animated` 改 **Humanoid** → `Walk` / `Idle01` 等 5 段动画**可重定向**（M3 巡逻前提）

### 未完成（V1 缺口，见 `GDD.md` §3.1）

> ✅ **M2 已全部收口**（M2.1–M2.4 明细见上方「已完成」），以下是当前真正剩下的缺口。

**额外项目（加分项，非交付必需；完整说明见 `ROADMAP.md` §13）**
- ⏸️ **M2.5 长枪·投掷与召回**（数据 asset 已就绪，代码与动画未做）
- ⏸️ 开局二选一 / 武器切换（`PlayerAttack.SetWeapon()` 已就绪）
- ⏸️ 剑的额外招式（`Elbow Uppercut Combo` / `Upward Thrust` —— 动画已存在但**未接入**）

**关卡（结构变更新增）**
- ❌ **多关卡场景**：`Level_01/02/03` **尚未拆分**（目前只有 `Game.unity`，3 个房间全在里面）
- ❌ **`LevelFlow`**（关卡索引 / 解锁 / 加载 / 结算）未做
- ❌ 选关界面未做；HUD 关卡进度未做

**M3 剩余**
- ✅ **M3.5 法师（远程）**（2026-10-06 完成）：`EnemyRanged` → **`EnemyCaster`**（`git mv` 保 GUID），改为 `EnemyAIController` 子类，**6 个行为状态** + kiting 目标点 + `EnemyPerception` 视线遮挡检查；🆕 `Data/EnemyCasterConfig.asset`（法师独立数值），Gunner 的 `_config` 已指过去。**自测 39 项全通过**（30 → 39，法师用例重写为 6 状态、12 项；🆕 判据「一次 Attack **只出手一次**」+「冷却恢复后能再次施法」）
  （⚠️ **绝不能直接 `SetDestination(player.position)`**，那样法师会冲向玩家 —— 见 `NAVMESH-GUIDE.md` §9）
- ❌ **M3.6 敌人动画状态机**：⚠️ 实测两个敌人预制体用的是**同一个** controller —— `Assets/_Game/Art/Animations/Enemy/**old**/EnemyAC.controller`（**在 old 目录下**！），只有 `idle` + `combo_01_1` 两状态、参数只有 1 个 `Attack` Trigger（🔴 **本轮实测更正**：剑兵 5 段、**法师也是 5 段** —— 原写「法师需 6 段」已随「弓兵 → 法师」改定作废）。⭐ 敌人模型 `combo_01_1 1.fbx` 实测是 **Humanoid**，可从 `OVR - Roskva_SwordInHand.fbx`（`Walk`）+ `Magical-Knight_Set` 直接重定向，**不必新造**
- ❌ **M3.7 多关卡拆分**：见下方「关卡」
- ⚠️ **M3.1 遗留的手验**：门的动态 Carving（关门堵 / 开门通）+ 敌人移动手感，**必须在 Play 模式确认**

**M4–M6**
- ❌ 真正的 Boss + Boss 血条（决策为**留到最后设计**）
- ❌ 音频（`Audio/` 为空）、存档
- ❌ HUD 完善、Windows 构建
- ❌ 报告 / PPT / 演示视频脚本

### 遗留问题（未修，见 §9）

**共 27 项已记录，其中 17 项已消除**（T3–T6 / T9–T13 / T15 / T16 / T21–T25 / T27）。
剩余未修项：

| 编号 | 内容 | 影响 |
|---|---|---|
| **T1** | `_motor.enabled = false` 停移动时把贴地也停掉 | 已降级为低优先级（跳跃取消） |
| **T2** | Player 上 `Rigidbody` + `CharacterController` + `CapsuleCollider` **三者并存** | 物理重复，易互相干扰 |
| **T6** | 敌人无共用状态机基类（近战 switch 硬编码 / 远程完全没有） | 加两种敌人 = 两份重复代码 → **M3.2 处理** |
| **T7** | `EnemyHealth.Died` 是 **static 事件** | 跨场景重开有重复订阅风险（`Damaged` 已用实例事件避开） |
| **T8** | `PlayerHealth.Died` 从未触发，死亡链路不统一 | 易出隐性 bug |
| **T14** | `AStar` 未接入（已结案：**改用 Unity NavMesh**，见 `NAVMESH-GUIDE.md`） | 无影响（自研 A\* 弃用） |
| **T17** | `TKDstyle_AnimSet` 为第三方资源包 | 提交/演示需注意署名与授权 |
| **T19** | `HitFxSystem` 预制体引用未赋值时 `Awake` 会 NRE | 场景里已连线，风险低 |
| **T20** | `RoomHintText` 文本为英文硬编码 | 若要中文化需改这里 |
| **T26** | 动画来源 FBX 的 Avatar **缺 `Neck`**（现为 `OVR - Roskva_SwordInHand.fbx`） | 用其 `Idle01`/`Talk*`/`Walk` 时颈部不重定向（**建议正式用 `Talk*` 前补**） |
| **T18** | 音频 / 存档 / Boss 实体 | V1 缺口，分属 M5 / M4 |

### 10.1 已确认的设计决策（速查 · 2026-10-03）

> 完整描述见 `GDD.md`，此处仅供快速恢复上下文。

| # | 决策 | 关键影响 |
|---|---|---|
| 1 | 参考《哈迪斯》，**只取"单层试炼"骨架** | 范围不膨胀 |
| 2 | **不做跳跃** | 省掉 Jump/Fall/Land 动画；**T1 物理重构降级为低优先级**；~~体力表去掉跳跃项~~（2026-10-06 起不再有体力消耗表） |
| 3 | **魔力不足 → 硬性禁止**该动作（消耗 > 当前魔力就不执行） | ⚠️ **2026-10-06 收窄**：现在只有特殊攻击会消耗魔力，冲刺与普攻没有资源门槛 |
| 4 | **武器 2 把：剑 + 长枪**，都是**四段**普通攻击（⚠️ **V1 只做剑**，见第 13 条） | 现有 `Attack1–4` 动画**可复用**；✅ 剑已换专用连段动画（`1_atk_sword03` 等） |
| 5 | 🔄 **剑·特殊攻击：火球 · 远距离**（2026-10-05 改定）—— 原「点按 → 插地 → 圆形范围伤害」**作废**；理由：游戏背景允许使用魔法 | 点按分支；发射火球，可复用 `Bullet.cs` 弹道 |
| 6 | ⏸️ **长枪·特殊攻击（额外项目）**：**点按立即投出**（固定距离、直线）→ 落地插地 → **空手不能普攻**（攻击键改为召回）→ **点按攻击键或特殊攻击键召回**（返程路径伤害） | 新增 `SpearThrow` / `Unarmed` / `SpearRecall` 子状态；**空手时按状态重映射按键语义**（攻击键→召回） |
| 7 | 敌人需要**视野判定 + 寻路**；**关卡内**布置障碍物 | 新增视野组件；**T14 升级为必做**（A* / NavMesh 方案待定） |
| 8 | **Boss 是人形** | 可复用 Humanoid 重定向动画 |
| 9 | **Boss 招式表暂缓** | 先搭决策框架 + 状态机骨架 |
| 10 | 音频**两种方式都用**（程序化合成 + 现成素材） | `AudioManager` 用"ID → Clip"查表，便于替换 |
| 11 | **存档用 JSON**（`persistentDataPath` + `JsonUtility`，带 `version`） | 需处理"文件缺失 / 损坏"；会产生**可见的存档文件** |
| 12 | **寻路改用 Unity 自带 NavMesh** ⚠️（2026-10-04 翻转；原为"接入自研 A\*"） | **T14 结案**；不再需要自建网格与路径平滑；`AStar` 降级为报告素材。新增工作：房间内摆障碍物 + 烘焙 + 门的 `NavMeshObstacle`(carving)。操作手册见 `NAVMESH-GUIDE.md` |
| 13 | ⏸️ **额外项目：武器开局二选一**（剑 / 长枪），**单局内不切换** —— ⚠️ **V1 只做剑，进关卡默认装配剑** | 省掉切换动作与切换 UI；⚠️ 只有一把武器时"选择"无意义 |
| 14 | ⏸️ **额外项目：长枪细节** —— **固定投掷距离**、**直线**飞行、到达终点**插在地上**、召回**与其他攻击同一套 hitbox 判定**、玩家死亡则**长枪留在原地**并直接进结算。**蓄力 / 瞄准 / 指示器 / 瞄准减速已整体取消** | 不再需要"长按三态"输入与指示器 UI；长枪需世界物体表现（飞行 / 插地 / 飞回） |
| 15 | **受击打断分两档：普通小怪会被打断**（硬直 0.4 s + 闪白，攻击中断）；**Boss 不会**（仅闪白，无硬直条/破防） | ⚠️ 现有代码里小怪的受击反应**链路是断的**（`EnemyHealth` 从不通知 AI），需要**接通**而不是删除（见 **T21**）；Boss 前摇表现必须做足预警 |
| 16 | 待定：投掷距离与投掷/召回速度的具体数值、命中回魔力、敌人失去目标后的搜索行为、敌人互相分离、Boss 招式表、关卡是否美化 | 见 `GDD.md` §15.2 |

---

## 11. 文档索引（阅读顺序）

> ⚠️ 与 `Docs/README.md` 保持一致（`Docs/` 下**共 9 份** Markdown：`README` + 本文 + 下列 7 份）。

| 顺序 | 文档 | 用途 |
|---|---|---|
| 0 | `Docs/README.md` | 索引与阅读指引、**一句话现状** |
| 1 | **`Docs/PROJECT-CONTEXT.md`**（本文） | **新会话快速接管：项目全貌 + 代码地图 + 坑** |
| 2 | `Docs/GDD.md` | 游戏策划案（**要做什么**）。**关卡结构见 §9**（多关卡独立场景） |
| 3 | **`Docs/ARPG-DIRECTION.md`** | **方案选型存档**：ARPG 单场景（❌ **未采纳**）+ ⚠️ **仍然生效的 §4.6 相机实现** |
| 4 | **`Docs/ROADMAP.md`** | **完成路线图**：阶段 0 + M1–M6 分步计划、每步验收标准、依赖关系、阻塞项 |
| 5 | `Docs/ART-PIPELINE.md` | 美术/音频资源的**获取方案**与可行性（怎么拿） |
| 6 | `Docs/ART-ASSETS.md` | 美术资产清单：已有/缺失对照、阻断项、**署名表**（缺什么） |
| 7 | **`Docs/NAVMESH-GUIDE.md`** | **操作手册**：Unity 自带寻路接入（烘焙 / Agent 改造 / 门阻挡 / kiting / 常见坑），**做 M3 时必读** |
| 8 | **`Docs/ANIM-GUIDE.md`** | **手 K 动画指南**：4 条硬约束、复用 vs 手 K 的分界、K 帧流程、导出导入设置 |

> 交接提示：新会话建议先读本文 §1–§7（定位/环境/目录/类职责/运行链路/美术现状），
> 需要改动时再读 §9（已知问题）与 §10（当前进度）；**动手前先实查代码** ——
> 各阶段的"现状"描述容易停在写作时的状态（已多次实测过期）。

---

## 12. 变更日志

| 日期 | 变更 |
|---|---|
| 2026-10-07 | **🐞 修复两处手感/渲染问题（用户反馈）：① Monster1 被打一下就变黑；② Monster2 远离玩家时先转身再走。** **① 变黑的根因 = 受击闪白用了 `MaterialPropertyBlock`，而敌人的材质 shader 不吃这一套。** 实测链条：敌人材质是 `Wraith_4.mat`，shader = **`Polygonmaker/SkinCutout DoubleSided`**（ASE 生成的 URP shader）—— 它**根本没有 `_BaseColor`**（只有 `_Color` / `_ColorR/G/B` / `_EmissionColor` / `_SkinHairColor` / `_SSS`），所以 `FlashRed()` 写的 `_BaseColor` 是**空操作**；更糟的是**只要给这些渲染器挂上非空 MPB，整个模型就明显变暗**：实测（固定相机 + 手动步进 + 中心 260×260 平均色）——原样亮度 **0.1817**、MPB 写法（`_BaseColor` = 红）亮度 **0.1605**（**-11.7%，且 R/G/B 全面下降 = 变暗而不是变红**）；而 `_BaseColor` = 红 与 = 白 出来的图**逐字节完全相同** ⇒ 参与渲染的不是颜色值、而是"挂 MPB"这个动作本身。再测"把 shader 声明的全部 26 个属性都写进 MPB"**仍然变暗**（38288 vs 原样 41138）⇒ 不是"缺属性"导致的，而是 URP / SRP Batcher 在这套 ASE shader 上的常量缓冲绑定行为。清空 MPB（`SetPropertyBlock(null)`）则**逐字节复原**。 **② 修法：改用「临时把 `sharedMaterials` 换成受击色克隆材质」**（`EnemyAIController.InitFlash/MakeFlashMaterial/FlashHit/RestoreColor`）：Awake 时按源材质克隆一次（同一材质只克隆一份）并缓存"原样数组 / 闪白数组"，受击换过去、`flashTime` 后换回来。实测：闪白时**偏红且亮度基本不变**（R 0.1811→0.1816、G 0.1760→0.1663），还原后**与原样 md5 完全相同**。顺手修掉一个老问题：原来只闪 `GetComponentInChildren<Renderer>()` 的**第一个**渲染器（Wraith 模型有 33 个渲染器、12 个激活）⇒ 现在**覆盖全部**。另加 `OnDisable` 里兜底 `RestoreColor()`（闪白中途被打死/隐藏，红色材质会留在渲染器上顶着出场）+ `OnDestroy` 销毁克隆材质。新增两个可调字段：`_hitFlashColor`（默认 (1, 0.28, 0.22)）/ `_hitFlashEmission`（默认 2，材质没有 `_EmissionColor` 时忽略）。 **③ 后撤朝向：`EnemyLocomotion` 新增 `BackstepTo(point, faceTarget)`** —— 与 `MoveToOrStep` 的唯一区别是**转向权**：后者把转向交还 `NavMeshAgent`（Agent 朝行进方向转身 ⇒ "先扭过身、再背对玩家走开"），`BackstepTo` 全程 `updateRotation = false` 由自己每帧 `RotateTowards` 攥着朝向。`EnemyCaster.UpdateChase` 的"太近 → 后撤"分支改走它（`Stop`/`Resume`/`MoveToOrStep`/`FaceTarget`/`Retreat` 都会解除该模式）。实测（Play 手动步进 20 fps）：距离 2.50 → 2.58 → 3.40 → 4.19 m **持续拉开**，而**朝向偏角全程 0.0°**、`agent.updateRotation` 全程 `false`，到 4.19 m（≥ `castRangeMin` 4）才转入 `Attack` 施法。 **④ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Play 模式两种表现均实测通过，控制台 **0 条**（进/出 Play 都干净）。⚠️ 本轮另发现两个**环境性**坑（非代码问题）：`EditorApplication.Step()` 可以手动步进游戏循环（Unity 窗口未聚焦时 Player 循环会停、`frameCount` 卡住，此时 `ScreenCapture` 出不了图，但 `Camera.Render()` + RenderTexture 可用）；`SetActive(true)` 激活的敌人在**域重载后**可能没跑到 `Awake`（`Fsm`/`Loco` 为 null）⇒ 用全新 Play 会话复测。⚠️ **回滚点**：`.workbuddy/backup/m36-monsters-2026-10-07/`（只覆盖 M3.6 那批文件）；本轮 3 个 `.cs` 的原版见同目录。 |
| 2026-10-07 | **🎬 M3.6 敌人动画状态机完成 + 敌人换装 Wraith（`Monster1` / `Monster2`）**。**① 需求**：用户已备好两个怪物模型并把动画放进各自的 Animator，要求"完善好"。**② 换装决定（用户确认）**：旧的 `Boxer` / `Gunner` **要被取代**。根本原因——两者 Animator 的 `avatar` **是空的**，且全工程找不到它们模型的 Avatar 资产（`combo_01_1 1.fbx` 标注 Humanoid 却生成不出 Avatar），⇒ Humanoid 肌肉剪辑**没有 Avatar 就播不出来** ⇒ 攻击动画不播 ⇒ `EnemyMeleeAI` 的伤害只由 `TickAttack` 开判定体 ⇒ **打不出伤害**。Wraith 自带可用的 `WraithAvatar`，一次解决。**③ 控制器重建（2 个文件）**：用户搭的图里**所有 transition 的条件都是空的**、且 AnyState→death/hit 无条件（进 Play 就会互相抢），因此清空重搭：新增 `Speed`(Float) 参数驱动 `idle ↔ flyinplace`（阈值 0.1，值 = Agent 实时水平速度）；`attack` / `hit` 按 exitTime（0.9 / 0.85）回收；AnyState → `death` / `hit` / `attack`（**列表顺序即优先级**，死亡禁止自过渡、攻击禁止自过渡、受击允许自过渡）。**④ 动画事件（关键）**：两个攻击 clip **原本一个事件都没有** ⇒ 近战永远开不了判定体。用**手掌骨骼轨迹采样**（`AnimationClip.SampleAnimation` 逐帧量 `WraithRPalm` 相对根的正前方分量）定出真实出手帧：`attack1` 手部最前伸 **0.512 m @ 0.50 s**、前伸增速峰值 **0.433 s** ⇒ 事件挂 **0.45 s**；`taunt` 手势顶点 **0.50 s** ⇒ 事件挂 **0.50 s**。**⑤ 代码接线（5 个文件）**：`EnemyAIController` 新增 `SetAnimFloat` / `ResetAnimTrigger` / `PlayHitAnim`（受击时**必须先复位 `Attack` 触发器**，否则"受击动画一播完就诈尸补挥一刀"）/ `PlayDeathAnim` / **`OnKilled()` 钩子**（基类非泛型拿不到子类的状态枚举）；`EnemyLocomotion` 暴露 **`CurrentSpeed`**（Agent 走 `agent.velocity`，直线兜底路径带 0.15 s 宽限）；`EnemyMeleeAI` / `EnemyCaster` 补 `OnKilled` + 受击播动画 + `EnterDeath` 设 `IsDead`。**⑥ 死亡表现修复（用户要求一起修）**：`EnemyHealth.Die()` 此前**直接 `SetActive(false)`**，死亡动画一帧都看不到；改为"先播完、再隐藏"——新增 `_deathHideDelay`（默认 **2.4 s**），用 `Time.unscaledTime` 计时（顿帧会把 `timeScale` 压到 0，`WaitForSeconds` 会被拉长）。**⑦ 配置校正**：`EnemyAIConfig.recoverTime` 1.5→**1.0**（= `attack1` 时长）、`hitStunTime` 0.4→**0.75**；`EnemyCasterConfig.recoverTime` 0.6→**1.2**（`windup 0.5 + 1.2 = 1.7 ≈ taunt 1.667`）、`hitStunTime` 同上。⚠️ 顺带发现 `EnemyAIConfig.asset` **落盘时缺了 M3.4/M3.5 新增的字段**（只有 `recoverTime` 之前的老字段），Unity 靠 C# 默认值兜着；本次 `SaveAssets` 已一并补写落盘。**⑧ 关卡重装配（用户确认"铺回 4 只、对齐原布局"）**：两个怪物先存成正式预制体 `_Game/Prefabs/Enemy/Monster1.prefab` / `Monster2.prefab`（**Variant** + 嵌套厂商预制体；实测厂商 `Wraith_4.prefab` / `Wraith_CustomColors_4.prefab` **未被写入任何包装组件**），再按原 `Boxer1` / `Boxer2` / `Boxer3` / `Gunner` 的位置与父物体摆回 `Room1`/`Room2`/`Room3`，并重连三个 `RoomController._enemies`。⚠️ **踩坑（已记入技能）**：`PrefabUtility.InstantiatePrefab` 后 `SetParent(room, true)`，若**不调 `PrefabUtility.RecordPrefabInstancePropertyModifications`**，保存场景时 localPosition 覆盖会被**静默回退成预制体自带的 local 值** ⇒ 敌人被房间的 `(-29,-28,29.5)` 偏移带到 `y=-28`（导航网外 28 米），表现为满屏 `Failed to create agent because it is not close enough to the NavMesh` + 全部回落直线位移。**⑨ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Play 模式逐条实测 —— 受击 → 动画 `hit` + 行为 `Hit`、走位 → 动画 `flyinplace` 且 `Speed` 参数 = 3、致死 → 动画 `death` + `IsDead=True`、3.5 s 后延迟隐藏、近战实际打出伤害（玩家 100→80）；**控制台 0 条**（`TickAttack has no receiver!` 与 `Failed to create agent` 均消失）。隔离实验另证四条 transition 全部生效（`Speed` 双向 / `Attack` trigger / `IsDead`）。⚠️ **遗留（关卡摆位，非代码问题）**：`Monster1_R1` 被 `Obstacle_01` 挡住视线（射线 3.75 m 处命中，`HasLineOfSight=False`）⇒ 该只不会发现玩家。⚠️ **回滚点**：`.workbuddy/backup/m36-monsters-2026-10-07/`（场景 / 两个控制器 / 两个 clip / 5 个 .cs / 2 个配置）。 |
| 2026-10-06 | **🐞 修复：Gunner 报 `AnimationEvent 'TickAttack' on animation 'combo_01_1' has no receiver!`**。**① 现象**：法师一进入攻击，控制台就刷这条警告（动画事件没人接收）。**② 根因**：两个敌人共用同一个占位 controller `Art/Animations/Enemy/**old**/EnemyAC.controller`（只有 `idle` + `combo_01_1` 两状态），而 `combo_01_1.anim` 在 **0.3 s** 处挂了 `TickAttack` 事件 —— 剑兵的 `EnemyMeleeAI` 有同名方法能接住，**M3.5 改写出的 `EnemyCaster` 没有**。⭐ 这其实**是 M3.5 引入的回归**：旧 `EnemyRanged` 从不触发 `Attack` 动画（只有距离判定 + 冷却），所以事件从来没被触发过；新版 `EnterAttack()` 会 `SetAnimTrigger("Attack")`，于是 `combo_01_1` 开始播、事件开始来。**③ 修法**：`EnemyCaster` 新增 **`TickAttack()`** 作为**出手帧的动画事件入口**（符合 GDD §6.3「出手由动画事件驱动」的既定口径），内部：先判**仍在 `Attack` 状态**（⚠️ **不这样判就会出现"被打断了却还是放出一发"**，GDD §6.3 明确禁止）→ 再走幂等的 `TryCastOnce()`；原前摇计时改为**兜底路径**，两者共用 `_casted` 闸门 ⇒ **一次施法只出一发**。**④ 行为变化（如实记录）**：占位近战 clip 的事件在 **0.3 s**，所以法师**实际出手时机由 0.5 s 变为 0.3 s**（`windupTime` 退居兜底）；⭐ **M3.6 换成法师专属施法 clip（`atk_energy*`）时，只需把事件挪到真正的出手帧，代码不用再动**。**⑤ 验证**：`dotnet build` 0 警告 0 错误；状态机自测 **39 → 42 项全通过**（新增「前摇受击 → 立即离开 Attack，迟到的动画事件被守卫拒绝」「被打断后重新施法仍能出手一次」两项）；实查 Gunner 预制体：`Animator` 与 `EnemyCaster` **在同一个物体上**（动画事件才能找到该方法），全工程扫描确认**只剩这一个**无人接收的事件（其余 `OnAttackHit` 均由 `PlayerAttack` 接收） |
| 2026-10-06 | **🔮 M3.5 法师完成：`EnemyRanged` → `EnemyCaster`（远程敌人改为表驱动状态机）**。**① 需求**：`GDD.md` §6.3 改定法师后，把 `EnemyRanged` 从零改写为状态机；用户确认 **保留 kiting / 表现复用现成 / 数值沿用原草案（HP 30 / 速度 2.8 / 冷却 2.2 / 伤害 8）**。**② 代码改动（4 个文件 + 1 个新资产）**：**`EnemyRanged.cs` → `EnemyCaster.cs`**（⚠️ **`.cs` 与 `.meta` 一起 `git mv`**，实测 **GUID 未变** `d8dfcac78dd30204ca61e3fd0b4f61a3` ⇒ Gunner 预制体上的组件引用**没断**，且 `bulletPrefab` / `firePoint` 两个**序列化引用也保住了** —— 为此**刻意沿用了旧字段名**，因为 Unity 按字段名恢复数据，改名就会丢引用）；类改写为 `EnemyAIController` 子类，**6 个行为状态** `{ Idle, Alert, Chase, Attack, Hit, Death }`（**没有 `Reposition`**，"保持距离"折进 `Chase`）。**`EnemyAIController`** 新增 **`protected virtual void OnAIUpdate()`** 钩子（基类 `Update` 是私有的，法师的施法冷却需要在所有状态下都走）。**`EnemyAIConfig`** 新增 4 个远程字段：`comfortDistance 6` / `castRangeMin 4` / `castCooldown 2.2` / `projectileSpeed 14`。🆕 **`Data/EnemyCasterConfig.asset`**（法师独立数值）+ `.meta`（新 GUID `4bdb472e…`），并把 **Gunner 预制体的 `_config` 指过去**（⚠️ 旧 `EnemyRanged` **不是** `EnemyAIController` 子类、**没有 `_config` 字段**，不显式连上就会变成"敌人不动"）；顺手把剑兵的 `EnemyAIConfig.asset` 的 `displayName` 由"拳击手"改为"剑兵"。**③ 三个关键的实现决定**：**(a) 施法弹复用 `Bullet` 而**不是** `Fireball`** —— 实测 `Fireball.Explode()` 用 `Physics.OverlapSphere` 对**所有** `IDamageable` 结算且**没有队伍过滤**，法师用它会把旁边的同伴一起炸；`Bullet` 只命中 `PlayerFSM`，**天然不误伤**。**(b) 覆写 `AgentStoppingDistance = 0.3`** —— 基类默认 `attackRange × 0.9`，法师 `attackRange 9` ⇒ stoppingDistance 8.1，Agent 会在 8 m 外停下，**kiting 直接失效**。**(c) 出手前再查一次视线**（前摇 0.5 s 里玩家可能躲到柱子后 → 取消这次施法），且**前摇被打断时施法自动取消**（出手逻辑在 `UpdateAttack` 里，离开状态就不会执行）。**④ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；**磁盘层复核**（Unity 本轮处于关闭状态，所以改用文件核对）：`.meta` GUID 未变、Gunner 的 `_config` 已指向新资产、`bulletPrefab`/`firePoint` 仍在、旧的 `fireCooldown`/`detectRange`/`bulletSpeed` 已清除；自测源码已改为法师 6 状态用例。**⑤ ⚠️ 未验证（本轮环境出了状况）**：**没有任何 Play 模式验证**（Unity 关闭），且**自测没能跑起来** —— 本机 `dotnet` 的 MSBuild/Roslyn 构建服务在本轮中途卡死（连 `Assembly-CSharp.csproj` 也编不动了；已试 `build-server shutdown` / 单节点 / 离线 NuGet 均无效，`csc` 直调被安全策略拦截）。⇒ **重启机器或重启 Unity 后，请务必重跑一次自测**（`.workbuddy/fsm-selftest/` 里 `dotnet run`）。⚠️ **回滚点**：`.workbuddy/backup/m3.5-2026-10-06/`。 |
| 2026-10-06 | **🔮 设计变更：远程敌人「弓兵 → 法师」，两个敌人统一为 5 个动画状态**（用户决定）。**① 决定内容**：远程敌人由弓兵改为**法师（Caster）**；两个敌人（剑兵 / 法师）的**动画状态机都只有 5 个状态**：`Idle / Run / Attack / Hit / Death`。**② ⭐ 这个改动直接消灭了项目原本最大的动画阻塞点** —— `ANIM-GUIDE.md` / `ART-PIPELINE.md` 都写明**唯一"必须手 K（或去 Mixamo 找）"的就是弓兵的 `Aim` / `Shoot`**（库里没有拉弓动作）。改成法师后施法动作**现成可用**：`Magical-Knight_Set/Animation/Humanoid/atk_energy01~11`（**11 个**，另有 `inplace/` 220 个**去根运动**版本）⇒ **敌人侧已无"必须手 K"的动画**。**③ ⭐ 一个关键概念澄清（已写进 `GDD.md` §6.4.1）**：**"行为状态数" ≠ "动画状态数"** —— 剑兵行为有 **7** 个状态、法师 **6** 个，但**动画只要 5 个**：`Alert` / `Reposition` 这类"没有专属动作"的中间状态**复用 `Idle` / `Run`**。**§6.4.1 给出了完整映射表**（行为状态 → 动画）。**④ 法师的行为设计（🔧 部分待确认）**：保持距离（kiting）仍是核心，但**走位折进 `Chase`**（不再单独成 `Reposition` 状态，因为 kiting 的目标点本来就在 `Chase` 里算）；`Aim` + `Shoot` **折叠进单个 `Attack`**（前摇与出手帧都在这一段动画内，出手仍用动画事件驱动）；行为状态 6 个：`Idle / Alert / Chase（含 kiting）/ Attack / Hit / Death`。⚠️ **待用户确认**：是否保留 kiting、法术表现用 `Bullet`（直线弹道）还是 `Fireball`（带爆炸）、沿用原弓兵数值（HP 30 / 速度 2.8 / 冷却 2.2 / 伤害 8）是否合适。**⑤ ⚠️ 一处实测纠错**：原先多处文档写"弓兵需 **6** 段动画"，本轮核对后更正为**两个敌人都只需 5 个**；另外核实**两个敌人预制体（`Boxer` / `Gunner`）用的是同一个 controller**，路径是 **`_Game/Art/Animations/Enemy/old/EnemyAC.controller`**（在 `old/` 目录下，只有 `idle` + `combo_01_1` 两状态、参数只有 1 个 `Attack` Trigger）。**⑥ 文档同步**：`GDD.md`（§3.1 敌人 2 种 / §6.1 / **§6.2 动画状态机** / **§6.3 整节重写为法师** / **§6.4.1 新增映射表 + 素材候选表** / §10 / §11 / §12 / §13）、`ROADMAP.md`（进度树 / M3.5 / M3.6 / §8 美术需求 / §12）、`ANIM-GUIDE.md`（**"必须手 K"的结论整段改写**）、`ART-PIPELINE.md`（§5.1 角色动画清单 + §8 采购表）、`ART-ASSETS.md`（§3.2 法师需求）、`NAVMESH-GUIDE.md`（§9 说明）、`README.md` —— **共 8 份、替换与重写 70 余处**；⚠️ **历史行（变更日志 / 带 ~~删除线~~ 的旧记录）保留原措辞**，因为那描述的是当时的决定。`check_docs.py` 9 份全通过。**⑦ 未改动代码**：`EnemyRanged.cs` 仍是旧实现（只有冷却计时 + 转向 + 开火），**M3.5 才改写**。 |
| 2026-10-06 | **⚔️ M3.4 剑兵完成：补 `Alert` / `Reposition`（状态数 5 → 7）**。**① 需求**：按 `ROADMAP.md` §12 推进主线，M3.4 只剩"补两个状态"。**② 改动（4 个文件）**：`EnemyAIConfig` 新增 **`alertTime 0.35`**（Alert 停顿时长）/ **`repositionDistance 1.5`**（攻击后退开距离），`EnemyAIController` 暴露对应 `AlertTime` / `RepositionDistance`；`EnemyLocomotion` 新增 **`MoveToOrStep(Vector3)`**（"Agent 优先、否则逐帧直线位移"的统一入口，`ChaseTarget` 改为复用它）；`EnemyMeleeAI` 状态表加 `Alert` / `Reposition` 两项 + 转移条件：`Idle →(CanSeeTarget) Alert →(停顿 alertTime) Chase →(进攻击范围) Attack →(后摇结束) Reposition →(到位/超时/玩家又贴脸) Chase`。**③ 三个刻意的实现决定**：**(a)** **不在 `enter` 回调里调 `Change`** —— 那会在 `Change` 内部再触发 `Change`，递归语义容易踩坑；`repositionDistance = 0`（退回旧行为）的分支因此放在 `UpdateAttack` 里判。**(b)** `Reposition` **调 `MoveToOrStep` 而刻意不调 `FaceTarget`** —— Agent 走位时朝向归 Agent 管，`FaceTarget` 会接管转向权、而 `MoveTo` 每帧又交还给 Agent，两者打架会抖动；所以表现是"**转身走开**"而不是"面朝玩家倒退"，这是取舍，见 GDD §6.2 只要求"拉开距离"。**(c)** 后退目标点**必须经 `NavMesh.SamplePosition` 吸附**（半径 **0.3**，要大于导航网 y 偏移 0.066，用 0.05 会全判成"不在网格上"）—— 直接给网格外的点会让 Agent 走到"最近合法点"甚至原地打转（`NAVMESH-GUIDE.md` §9 的要求）。**④ 防卡死**：`Reposition` 有**超时兜底**（`max(0.3s, distance ÷ speed × 2)`）—— 没有它的话，目标点被柱子挡住（永远到不了位）会让敌人**永久卡在 Reposition**。**⑤ 刻意不做**：`Alert` / `Reposition` **不调 `SetAnimTrigger`** —— `EnemyAC` 只有 2 个状态，调了只会给控制台添"参数不存在"的警告（M3.6 再补）。**⑥ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Unity 控制台 **0 错误**；**状态机自测 30 项全通过**（`.workbuddy/fsm-selftest/` 可 `dotnet run` 复跑：原 26 项 + 剑兵循环新增 4 项 —— 看见玩家→Alert、Alert 停顿 22 帧后→Chase、后摇→Reposition、到位→Chase、超时兜底 63 帧→Chase、状态序列校验）；实查 `EnemyAIConfig.asset` 新字段已按默认值落位（`alertTime 0.35` / `repositionDistance 1.5`）、既有数值未变（`aggroRange 8` / `attackRange 1` / `moveSpeed 3` / `windupTime 1` / `recoverTime 1.5` / `viewAngle 160`），`EState` 枚举实测为 `Idle,Alert,Chase,Attack,Reposition,Hit,Death`。**⑦ ⚠️ 未验证（必须 Play 模式）**：敌人实际表现 —— ① 后退时"转身走开"的观感是否可接受；② 会不会出现"攻击→后退→追上来"的**来回拉锯**（`attackRange 1 m` 与 `repositionDistance 1.5 m` 的量级很接近，需要看实际节奏，必要时调 `repositionDistance`）；③ 多个敌人同时后退是否挤在一起。⚠️ **回滚点**：`.workbuddy/backup/m3.4-2026-10-06/`。 |
| 2026-10-06 | **🔄 攻击转向由「方案 A（整段可转）」改为「方案 B（转向窗口）」**（用户选定）。**① 需求**：出手前能微调朝向，**出手后朝向锁定**（判定与视觉不错位）。**② 改动（3 个文件、很小）**：`PlayerAttack` 新增 `_hitThisSegment`（`OnAttackHit()` 里置 true；`StartCombo()` / `TryNextCombo()` 清零 ⇒ **每段各自一个窗口**）与只读属性 **`CanTurn`**；`PlayerMotor` 新增 `[SerializeField] gateTurnByHitFrame = true` 开关与 `CanTurnNow()` 判据（在 `_facingOnly` 分支里：`if (hasInput && CanTurnNow()) Face(dir, attackTurnSpeed)`），并在 `Awake` 里 `GetComponent<PlayerAttack>()`；`PlayerFSM` 只补了注释（逻辑不变）。⭐ **开关化设计**：`gateTurnByHitFrame` 取消勾选即**不改代码**退回方案 A。**③ 两个刻意的设计决定**：**(a)** `CanTurn` **不叠加 `isAttacking`** —— 那个由 `AttackStateBehaviour` 在动画状态 enter/exit 驱动，而 Attack 的过渡本身有 0.05~0.25 s 混合期，会让"窗口起点"不可控（Attack1 的窗口总共才 0.233 s）；**(b)** `CanTurnNow()` 采用**防御性放行** —— `PlayerAttack` 为空、或某段动画忘挂 `OnAttackHit` 时**一律允许转向**，宁可松也不要出现"突然转不动"这种难归因的 bug。**④ ⚠️ 参数提醒**：窗口很短（实测动画事件：Attack1 0.233 s / Attack2 0.333 s / Attack3 0.567 s / Attack4 0.433 s），`attackTurnSpeed` 仍是 **180 °/s** ⇒ Attack1 最多只能转 **42°**。**建议改到 300~360**（300 ⇒ 70/100/170/130°；360 ⇒ 84/120/204/156°）。该字段是 `PlayerMotor` 上的序列化值，**Inspector 直接改，无需重新编译**。**⑤ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Unity 控制台 **0 错误 0 警告**；实查玩家实例 `gateTurnByHitFrame = True`（新字段按 C# 默认值正确落位）、`attackTurnSpeed = 180`、`PlayerAttack.CanTurn` 属性存在（空闲态 = True）、`_hitThisSegment = False`、`isAttacking = False`。**⑥ ⚠️ 未验证（必须 Play 模式）**：真实手感、以及"鼠标转视角会带着角色一起转"是否需要加"冻结相机参照"开关。⚠️ **回滚点**：`.workbuddy/backup/attack-facing-2026-10-06/`（含 `PlayerAttack.cs.before-B` / `PlayerMotor.cs.before-B`）。 |
| 2026-10-06 | **🎮 攻击手感优化：攻击中"不移动、但可有限转向"（方案 A）**。**① 需求**：攻击时按移动键不应移动，但**应能一定程度上改变面朝方向**（原来完全不能转，太僵硬）。**② 根因（实测）**：`PlayerFSM` 进 Attack 时设 `_motor.enabled = false`，而**朝向与位移都写在 `PlayerMotor.Update()` 里**（`Face()` 与 `_cc.Move`）→ 组件一禁用两者一起冻死；且全工程**只有 `PlayerMotor` 写 rotation**，没有别的东西能补上。量化：4 段攻击动画实测 **1.000 / 1.200 / 1.133 / 1.633 s（合计 4.966 s）**，即连段全程近 **5 秒**朝向一动不动。**③ 改动**：`PlayerMotor` 新增 `[SerializeField] attackTurnSpeed = 180`（一般移动 `turnSpeed = 720`）与 **`SetFacingOnly(bool)`** —— 该模式下**跳过水平位移、保留贴地下压、仍按输入方向限速转身**；`Face()` 签名改为 `Face(dir, degPerSec)`。`PlayerFSM` 的 `_enter[Attack]` 由 `enabled=false` 改为 `enabled=true; SetFacingOnly(true)`，并在 **`_exit[Attack]` 复位 `SetFacingOnly(false)`**（与 `CloseAllHitboxes()` 放一起，**漏了就会"只能转身、跑不动"**）。**④ 两个刻意为之的细节**：**(a)** `_facingOnly` 分支**不写 `_anim.SetFloat("speed")`** —— 保持攻击前的值，好让 `Attack→Locomotion` 的 0.25 s 混合期正确混向 Run（写 0 会混向 Idle，表现为"跑动中砍完一刀先站一下"）；**(b)** 顺带修掉一个旧隐患：**攻击期间贴地下压不再被中断**（原 `enabled=false` 连贴地一起停，属 T1 同源问题）。**⑤ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Unity 控制台 **0 错误 0 警告**；实查玩家实例 `PlayerMotor.attackTurnSpeed = 180`（新字段按 C# 默认值正确落位）、`turnSpeed = 720`、`groundStickSpeed = 2`、`config = PlayerConfig`。**⑥ ⚠️ 未验证（必须 Play 模式）**：实际手感 —— 本轮**没进 Play**（`Game.unity` 仍是未保存状态）。⚠️ **预判风险**：`Face()` 的方向是**相对相机每帧重算**的，攻击中鼠标转视角会带着角色一起转（可能"原地打转"），需要时再加"攻击期间冻结相机参照"的开关。⚠️ **同时发现**：用户已把 **Dash 的 5 条入线全部删除** ⇒ **Dash 状态不可达**（冲刺只剩位移+无敌、无动画），而 `PlayerFSM._update[Attack]` 里仍在按冲刺键就 `TryDash()`；`SetTrigger("Dash")` 也已成为**无人消费的死调用**。**建议处理但未动**（等用户决定）。**⑦ 回滚点**：`.workbuddy/backup/attack-facing-2026-10-06/`（`PlayerMotor.cs` / `PlayerFSM.cs`）。 |
| 2026-10-06 | **🔄 二次规则变更：魔力「不再自动回复」+ 修掉真 bug（冲刺穿墙）**。**① 需求**：魔力不应该自动回复；角色冲刺可以穿墙（bug）。**② 魔力改动**：`PlayerMana` **整段删除 `Update()`**（原"停止消耗 0.6 s 后按 25/s 回复"）与运行时计时 `_regenDelayTimer`；`PlayerConfig` 删除 `manaRegenDelay` / `manaRegenRate` 两个字段（`PlayerConfig.asset` 里对应两行也删掉）；新增**唯一的"加魔力"入口** `Restore(amount)` / `RestoreFull()`（**当前无调用方**，预留击杀回魔 / 拾取回魔 / 过关补给）。消耗侧不变：`TrySpend` / `TrySpendSpecial`（读武器 `specialManaCost 30`），开局满魔 100 ⇒ **一局最多放 3 次特殊攻击** —— 特殊攻击由此从"节奏手段"变成"稀缺资源"（已写入 `GDD.md` §4.8）。**③ 冲刺穿墙根因**：`PlayerDash.LateUpdate()` 写的是 `transform.position += transform.forward * dashSpeed * dt` —— **完全绕过 `CharacterController`**，等于"直接改坐标"，所以任何墙都拦不住（**不是穿隧/穿透，是根本不检测**）。**修法**：`PlayerDash` 改为调用同物体上的 `PlayerMotor.StepMoveGrounded(delta)`，由 `PlayerMotor` 统一走 `CharacterController.Move()`（它本来就是唯一持有 CC 的组件）。⭐ 由此确立约定：**玩家位置只有一个写入者（`PlayerMotor`）**，其它组件禁止直接写 `transform.position`。**④ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Unity 控制台 **0 错误 0 警告**；grep 确认玩家脚本里**已无 `transform.position =`**；**反射实测** `PlayerMana` 类型上**不存在 `Update` 方法**（自身方法表只有 `SetWeapon / Start / TrySpend / TrySpendSpecial / Restore / RestoreFull / Broadcast`）、`PlayerConfig.asset` 的 `manaRegenDelay` / `manaRegenRate` **FindProperty 返回 null**（字段确已删除）；结构复核确认 `Player2` 上 `PlayerMotor` + `PlayerDash` + `CharacterController(enabled=True)` 齐备，且 `PlayerDash.config = PlayerConfig`、`PlayerMana._config = PlayerConfig`、`PlayerMana._weapon = Weapon_Sword`（引用都没丢）。**⑤ ⚠️ 本轮新踩到的坑**：**编辑模式下 `CharacterController.Move` 不生效** —— 在临时附加场景里用与玩家同参数的胶囊冲 4 米撞 1 米厚墙，实测位移 **−0.83 米**（既没前进、也没被墙挡住）。所以"冲刺会不会穿墙"这类问题**只能进 Play 模式验**。**⑥ ⚠️ 未验证**：Play 模式下贴墙冲刺的实际表现 —— 本次**没有进 Play 模式**，因为实查发现 `Game.unity` 处于**未保存改动**状态，未擅自接管编辑器。⚠️ **回滚点**：`.workbuddy/backup/fix-mana-regen-dash-2026-10-06/`（5 个原文件）。 |
| 2026-10-06 | **🔄 规则变更：资源由「体力」改回「魔力」，并收掉冲刺/普攻的消耗 —— 顺便修掉一个真 bug（玩家运动不贴地）**。**① 用户需求**：游戏背景引入了魔法 → 资源条改回 **mana（魔力）**；**删掉消耗体力的部分，只保留特殊攻击消耗魔力**。**② 代码改动（改名 + 删逻辑）**：`PlayerEnergy.cs` → **`PlayerMana.cs`**（类 `PlayerEnergy`→`PlayerMana`）、`UI/EnergyBar.cs` → **`UI/ManaBar.cs`**（类 `EnergyBar`→`ManaBar`）—— ⚠️ **`.cs` 与 `.meta` 一起 `git mv`，GUID 未变**，所以场景里 `Canvas/ManaBarBack` 上的组件引用与 `_player`/`_fill` 都没断（实查确认：`_player = Player2 (PlayerMana)`、`_fill = ManaFill`）；事件 `OnEnergyChanged`→`OnManaChanged`、`OnSpendFailed`→`OnManaSpendFailed`；`MaxEnergy/CurEnergy`→`MaxMana/CurMana`。**删除**：`TrySpendDash()`、`TrySpendAttack()`、`WeaponConfig.energyCost[4]`、`WeaponConfig.GetEnergyCost()`、`PlayerConfig.dashEnergyCost/attackEnergyCost`；`PlayerFSM.TryDash()` / `TryAttack()` 去掉资源门槛（现在无条件放行）、`PlayerAttack.TryNextCombo()` 删掉资源判断；`PlayerConfig` 的 `maxEnergy`→`maxMana`、`specialEnergyCost`→`specialManaCost`、`energyRegenDelay/Rate`→`manaRegenDelay/Rate`（**数值不变**：100 / 30 / 0.6 / 25）。`Weapon_Sword.asset` / `Weapon_Spear.asset` 的 `energyCost` 行删除、`specialEnergyCost: 30` → `specialManaCost: 30`。`Editor/CreatePlayerPrefabs.cs` 的必备组件名数组同步改名（否则该工具会报"缺 PlayerEnergy"）。**③ ⭐ 顺带修掉的两个真问题**：**(a) 玩家运动不贴地（根因实测确认）** —— `CharacterController` 的 `center=(0, 0.8, 0)` 配 `height=2`，胶囊底部落在 **world Y = -0.2**，而可见脚底在 **-0.0008**（脚趾骨骼 `L-Toe0` -0.018 / `R-Toe0` -0.021）→ **胶囊比脚低 0.1992 米、嵌进地面**，`CharacterController.Move()` 把重叠顶出去就把角色抬高约 0.2 米。修法：`center.y 0.8→1.0`（= `height/2`，胶囊底正好落在脚底）、`skinWidth 0.08→0.03`（Unity 建议 ≈ 半径 10%，原来是半径 27%）；`Player1.prefab` / `Player2.prefab` 都已改（实查确认胶囊底 local Y 由 **-0.2 → 0**）。**(b) T2 其实已经不在**：实查玩家身上**只有 `CharacterController` 一个碰撞体**（Rigidbody 与 CapsuleCollider 都没了），`applyRootMotion = False`。**④ 验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**；Unity 侧实查 —— `ManaBarBack` 组件类型为 `Game.UI.ManaBar` 且引用完好、玩家实例与两个预制体组件列表均为 `PlayerMana`（**无 `PlayerEnergy` 残留**）、Missing Script **0**、`PlayerConfig.asset` 的 `maxMana 100`/`specialManaCost 30`/`manaRegenDelay 0.6`/`manaRegenRate 25` 与 `Weapon_Sword.specialManaCost 30` 均正确。⚠️ **未验证**：Play 模式下"魔力不足时特殊攻击被拒绝 + HUD 闪烁"的实际手感。**⑤ 设计影响（如实记录）**：收掉冲刺/普攻消耗后，`GDD.md` 原第 2 条设计支柱「体力即节奏（输出与位移收费）」**已不成立**，已改写为「魔力即资源节奏」：节奏压力从"打与撤的取舍"转移到"特殊攻击的使用时机与冷却"，割草更顺但策略张力变浅 —— 这是"表现（发火球）与资源语义必须一致"的有意取舍。 |
| 2026-10-06 | **🗺️ M3.1 NavMesh 烘焙 + 障碍物（首次改场景与预制体）**。**改动清单**：① `ProjectSettings/NavMeshAreas.asset` —— Agent Type `Humanoid` 的 `agentRadius` **0.5 → 0.4**（与敌人 `CapsuleCollider.radius = 0.4` 对齐；⚠️ 这个值必须与烘焙和 Agent 两侧一致，否则窄道处敌人会卡）；② `ProjectSettings/TagManager.asset` —— 新建层 **`Door`（slot 13）**，3 扇门体从 `Environment` 移过去并挂 `NavMeshObstacle`（`Shape=Box` + `Carving` ✓ + 取消 `Carve Only Stationary`）；③ `Assets/Scenes/Game.unity` —— 新增 `NavMeshRoot`（挂 `NavMeshSurface`）与 `Obstacles` 下 6 个柱子（`Obstacle_01`–`06`，1.5×2.5×1.5 米，`Environment` 层，复用墙的 URP `Lit` 材质），每房间 2 个、其中一个**故意卡在"入口门 → 该房间敌人"的连线上**；④ `Assets/_Game/Prefabs/Enemy/{Boxer,Gunner}.prefab` —— 各加 `NavMeshAgent`（`type=Humanoid` / `radius 0.4` / `speed 3` / `stopping 0.9` / `Avoidance Priority` **50 / 60**）；⑤ 🆕 `Assets/Scenes/NavMesh-Game.asset`（烘焙产物）。**`NavMeshSurface` 配置**：`Agent Type = Humanoid` / `Collect Objects = All` / **`Include Layers = 只勾 Environment`** / `Use Geometry = Physics Colliders`。⭐ **实测数值**：烘焙 **580 ms** / **651 三角形** / **1368 顶点** / **≈248553 m²** / 资产 **73 KB**；覆盖 `(-249.5, 0.1, -249.5)` → `(249.5, 5.1, 249.5)`（`Ground` 是 500×500 的 Plane，所以整块地面都烘进去了 —— 代价可接受，不必改 `Volume`）。⭐ **三个实测撞出来的坑（已写进 `NAVMESH-GUIDE.md` §15.2）**：**① `NavMeshObstacle` 的 Carving 会被写进烘焙数据** —— 同一场景唯一差别是"烘焙时门 obstacle 开不开"：启用时烘出门线 `######.....######`（**门洞被永久烘死**，开门也走不通），禁用时烘出 `#################`（全通）→ **烘焙前必须临时禁用门的 Carving**；**② `NavMeshSurface` 上没有 Agent Radius / Height 字段**（反射实测 18 个序列化字段里没有，只有 `m_AgentTypeID` 这个"选哪套体型参数"的下拉），体型参数属于 Agent Type，手册 §5 那张表已拆成两张；**③ Carving 不是当帧生效的** —— 同一帧内"关组件立刻采样"会读到旧结果，本轮因此一度误判成"门没被排除"，**改状态与读结果必须分到两次执行**。**验证手段与结果**：`layerMask` 生效性用"只留 Door 层 → 导航网面积 **0 m²**"反证（若 mask 被忽略应是整块地面）；资产干净性用"Carving 全关 + 重新挂载重载资产 → 三条门线全 `#`"独立复核；寻路用 `NavMesh.CalculatePath`：玩家在同一房间内时 **4 个敌人全部 `PathComplete`**，绕障比 **1.020**（`Boxer1 → 门内侧` 的路径在 `Obstacle_01` 的 z 区间内偏到 `x = -0.8`，确实绕开了柱子）；场景 4 个敌人实例均已带上 `NavMeshAgent`。⚠️ **两个判定陷阱**（也曾误导过我）：`SamplePosition` 半径太小会误判（导航网在 y≈0.066 平面，用 0.05 半径会全判成"不可走"，0.2~0.3 才稳）；`PathPartial` 不一定是 bug（玩家在房外、门关着时房间本就该封死，此时正是正确行为）。⚠️ **仍未验证（必须在 Play 模式）**：门的**动态** Carving（关门堵 / 开门通）与敌人实际移动手感；编辑模式下 Carving 的更新时机不可靠。**操作记录与教学已写入 `NAVMESH-GUIDE.md` §15**（七步总览 + 手动路径 + 判据 + 可复现的验证代码片段）。 |
| 2026-10-06 | **⚙️ M3.2 敌人状态机基类 + M3.3 视野感知（首次改游戏代码）**。**新增 4 个文件**（都在 `Assets/_Game/Scripts/Gameplay/Enemy/`）：`EnemyStateMachine.cs`（`EnemyFsmBase` 非泛型外壳 + `EnemyStateMachine<TState>` 三字典表驱动，`Register` / `Start` / `Tick` / `Change` / `LockStateMachine` / `Is`）、`EnemyAIController.cs`（**非泛型** MonoBehaviour 共用层：组件、数值、目标查找、受击反应、动画触发带参数存在性检查）、`EnemyLocomotion.cs`（移动分层：`ChaseTarget` / `Stop` / `Resume` / `FaceTarget` / `Retreat` / `SyncWithNavMesh`）、`EnemyPerception.cs`（距离 + 扇形 + 射线遮挡，按帧缓存）。**改动 2 个文件**：`EnemyMeleeAI.cs` 迁到基类（同样 5 个状态、同样时序、同样受击反应，逐条对齐）；`EnemyAIConfig.cs` 新增 7 个视野字段。⭐ **两条关键设计决定**：① **状态机刻意做成纯 C# 类、不继承 MonoBehaviour** —— 查证后确认 Unity 官方口径是"泛型 MonoBehaviour 不被支持"，把 `_config` 这类 `[SerializeField]` 放进泛型基类有**丢引用导致敌人完全不动、且 Inspector 看不到字段**的风险，无法在编辑器里实测就不能押这个赌；同时纯 C# 的副产品是**能在没有 Unity 的环境里跑自测**。② **位置只有一个写入者** —— 有 Agent 时全部交给 Agent，`EnemyLocomotion` 绝不写 `transform.position`。**验证**：`dotnet build Assembly-CSharp.csproj` **0 警告 0 错误**（校验时临时把 4 个新脚本加进 csproj 的 `Compile` 列表，**校验后已还原**，备份在 `.workbuddy/backup/Assembly-CSharp.csproj.bak-2026-10-06`；该文件本就 gitignore）；**状态机自测 26 项全通过**（`.workbuddy/fsm-selftest/`，`dotnet run` 可复跑：迁移时序 9 项 + 边界防护 5 项 + 剑兵完整战斗循环 9 项 + 第三种敌人复用骨架 3 项）。⚠️ **一处行为变化需实机确认**：剑兵 `Idle → Chase` 由「纯距离」改为「距离 + 扇形 + 视线」，`viewAngle` 默认 **160°**（GDD 口径），若敌人"不追人了"调到 **360** 即退回旧行为（一个字段）。⚠️ **未做**：M3.1 NavMesh 烘焙（⚠️ 通道状态：本轮核查初期 `127.0.0.1:8080` **连接被拒（离线）**，同会话后半段**已恢复在线** —— Unity 在跑、控制台 0 报错；该步会改 `Game.unity` 与预制体，**需用户确认后再动**）、M3.4 `Alert`/`Reposition`、M3.5 弓兵（`EnemyRanged` **本轮未改动**）、M3.6 敌人动画。⚠️ **Unity 侧已复核**：控制台 0 编译错误；`Game.unity` **Missing Script = 0**（3 个 `Boxer` + 1 个 `Gunner` + 4 个 `EnemyHealth`）；**`_config` 引用未因字段迁到基类而丢失**（`SerializedObject` 实读为 `EnemyAIConfig`）；`EnemyAIConfig.asset` 7 个新字段已按 C# 默认值落位（`viewAngle 160` / `requireLineOfSight true` / `proximityRange 2` / `loseTargetTime 0` 等）。⚠️ **一个无关紧要的滞后**：Unity 重生成的 `Assembly-CSharp.csproj` 尚未纳入这 4 个新脚本（只影响 IDE 智能提示，不影响 Unity 自身编译 —— Unity 是按 `Assets/` 扫描编译的，编辑器再获焦点时会自动补上）。 |
| 2026-10-06 | **🧹 剔除两个大体积第三方动画包出版本控制 + 文档整理 + 首次功能提交**。**需求**：`Magical-Knight_Set` / `Rapier_Anim_Set` **不进 git**。**实测现状**：两包此前**从未被跟踪**（`git ls-files` 命中 0 —— 所以不存在"从索引里删"的动作），但**也未被 ignore** → 任意一次 `git add -A` 都会把它们加进来；体积实测 **790 MB / 793 MB**（合计 1.58 GB），**远超 GitHub 单文件 100 MB / 仓库 1 GB 的软限制**，一旦提交会被拒。**处置**：`.gitignore` 追加 **4 条规则**（目录 + `.meta` 各 2 条），**沿用既有的 `TKDstyle_AnimSet` 写法**（那条也是第三方包 + 体积 + 署名三个理由）。⭐ **排除的安全性已验证**：全工程搜这两个包的 GUID（`f91e037f…` / `fcb6abcc…`），除**包自己的 `.meta`** 外**零引用**（`.prefab`/`.controller`/`.anim`/`.unity`/`.asset`/`.mat` 全查过）→ **克隆仓库后能正常打开并 Play**；因为现役动画早已**抽成独立 `.anim` 副本**放在 `Art/Animations/Player/`（M2.3 时从 `Magical-Knight_Set` 复制出来的），并不依赖原包。**文档同步（整理）**：① 本文**新增 §2.1「版本控制排除清单」**（4 项 × 体积 × 为什么能排除 + 合计约 1.86 GB + "克隆后可正常运行"的实测依据），§3 目录地图补两行，§5 资产表补两行；② `ART-ASSETS.md` §7 摘要行、**§10 第三方署名表**（补两行 + 明确"不进 git 但**署名不能省**"），⚠️ 并**修正一条过期表述** —— `Y Bot.fbx` 原写"已不再使用但资产仍在"，实际**已于 10-05 从工程删除**（本轮删除清单里可见）；③ `ART-PIPELINE.md` 资源表补两行；④ `GDD.md` §13「可复用动画库」标注该包不进 git；⑤ `ROADMAP.md` §9「第三方资源清单」由"TKD 一个包"扩为"三个包 + 署名不能省"。**顺带**：把上一轮生成的临时备份 `Docs/PROJECT-CONTEXT.md.dupbak` **移出仓库**（→ `.workbuddy/backup/PROJECT-CONTEXT.md.dupbak-2026-10-06`），避免它被当成文档提交。**提交内容**：本次为自初始提交 `da8cc75` 以来的**第一批功能提交**，含 M2.2/M2.3/M2.4 全部产物（`Player1`/`Player2` 预制体、`Fireball` 相关 4 个文件、`BipedTwistBoneFixer`、`CreatePlayerPrefabs` 工具、新动画 `.anim`、剑 FBX 与材质）+ 旧资源删除（`Player.prefab`/`Y Bot.fbx`/`Roskva.prefab`/`OVR - Roskva_Animated.fbx`、旧动画移入 `old/`）+ 5 份文档更新。**已推送 `main` → `origin`（提交 `b80d608`，快进 `5c4dc1d..b80d608`）。** ⚠️ 唯一"未进 git"的差异就是被排除的三个动画包（本地仍在）。<br>⚠️ **推送时踩到一个环境坑（记下来）**：`git push` 报 `schannel: CRYPT_E_NO_REVOCATION_CHECK (0x80092012)` —— schannel 无法访问证书吊销列表（本机走代理，吊销服务器不可达）。`git -c http.schannelCheckRevoke=false` 在本机 Git 2.53 **无效**；`http.sslBackend=openssl` 则报 `unable to get local issuer certificate`；`ssh`（22 端口）被网络拒绝。**可行解法**：`GIT_SSL_NO_VERIFY=1 git push origin main`（**一次性环境变量，未写入任何 git 配置**；⚠️ 代价是本次不校验证书，仅建议在信任网络下用）。 |
| 2026-10-05 | **📄 M2.4 文档同步收尾 + 修掉一处「重复粘贴」**（本轮**只动文档，无代码改动**）。① 跑 `check_docs.py` 发现 `PROJECT-CONTEXT.md` §12 的 **M2.3 那行末尾多出一段完全重复的正文**（与被重复部分相似度 **98.7%**，仅差开头标题；行内多一根竖线导致表格列数校验报警）—— ⚠️ **这是我自己上一轮改写该行时旧内容没替换干净留下的**，已截断修正（行长 4245 → 2156），修正前备份 `.workbuddy/backup/...`、同目录 `PROJECT-CONTEXT.md.dupbak`；并追加了一版**全局「行内自我重复」扫描**（取每行尾 200 字符回查是否在行内更早出现）确认**无其他残留**。② **补齐本轮遗漏的过时锚点**（M2.4 那轮只同步了 `ROADMAP`/本文 §10/§12/`GDD`/`ART-ASSETS`，**漏了本文头部与 `README.md`，`ROADMAP` §12 也没跟上**）：**本文头部** 阶段由「M2.1 ✅」→「**M2 武器（剑）全部 ✅**」、下一步由「M2.2 装配剑」→「**M3 敌人 + 寻路**」；**§10** 把 M2 从「（进行中）」提升为「**全部完成 ✅**」并**把 M2.2–M2.4 三条从「未完成」区移到「已完成」区**（原「未完成 → M2 剩余（只做剑）」小节已删，改为一句指向），⚠️ 同时标注 M2.4 的三项**未验证**项；**`README.md`** 阅读顺序表第 3 行、进度行、下一步、最短路径第 4 条共 4 处；**`ROADMAP.md` §12「下一步建议」**由「① M2.3 连段动画（进行中）② M2.2 装配剑 ③ M3」重写为「**M2 已全部收口** → ① M3（附：动手前先手动实试火球并据 `atk_energy01` 实际出手帧调 `specialCastDelay`）② M3.7 拆关 ③ 额外项目」。**校验：9 份文档的表格列数、代码块闭合、内部链接全部通过（0 问题）。** |
| 2026-10-05 | **✅ M2.4 特殊攻击「火球」完成 —— M2 全部收口**。**形态改定**（用户设定）：背景允许魔法 → 特殊攻击 = **发射火球、远距离、命中后小范围爆炸**，**取代**原「剑·插地（`SwordSlam`）」。**新增**：`Gameplay/Player/Fireball.cs`（飞行→命中/到射程就在原地爆炸→`Physics.OverlapSphere` 对半径内每个 `IDamageable` 结算一次，**`HashSet` 去重避免多碰撞体被打多次**；⚠️ **命中伤害与溅射合并成一次 OverlapSphere** —— 爆炸中心即命中点，直接命中者必在半径内，天然不重复；移动沿用 `Bullet.cs` 的 `transform.position +=`，12 m/s 每帧约 0.2 m，远小于敌人碰撞体半径，**不穿透**）；`Gameplay/Player/SpecialStateBehaviour.cs`（⚠️ **刻意不复用 `AttackStateBehaviour`**，因后者改的 `PlayerAttack.isAttacking` 被连段窗口逻辑共用，混用会让语义错）；`Prefabs/Fx/Fireball.prefab`（layer `Projectile(11)` + Trigger 球 r=0.35 + **kinematic Rigidbody** + 球体视觉）；`Art/Materials/Mat_Fireball.mat`（URP/Lit + Emission）。**修改**：`PlayerState` 加 `Special`；`PlayerFSM` 加三表项 + `TrySpecial()`（冷却→武器→体力，顺序不可换）+ `SpawnFireball()` + `SetCasting()`，⚠️ 回 Idle 判据用 `_specialAnimStarted && !_casting`（**否则 `SetTrigger` 当帧就误判"动画已结束"**）并加 3 s 兜底；`WeaponConfig` 加 `Fireball = 3` 枚举 + `specialProjectileSpeed`/`specialExplosionRadius`/`specialCastDelay`/`specialSpawnHeight`/`specialProjectilePrefab`；`Weapon_Sword.asset` 的 `specialType` **SwordSlam→Fireball**、`specialRange` **3.5→15 m**（普攻数据未动）；`PlayerAC.controller` 加参数 `Special`(Trigger) + **5 条进入转场**（Locomotion + Attack1~4 → Special_Attack）+ **1 条转出**（→ Locomotion, exitTime 0.85）+ 给 `Special_Attack` 挂 behaviour（⚠️ 原状态是**孤岛**：进出 transition 与 behaviours **全为 0**）。**Play 实测验收**：① `TrySpecial()` 成功、**体力 100→70**（扣 30）、冷却 3 s、二次调用被挡且不再扣血；② `SpawnFireball()` 生成成功（layer 11、玩家上方 1.2 m + 前方 0.4 m、组件齐全）；③ 爆炸 **`Boxer1` hp 30→0** 并触发死亡；④ **在自己脚下引爆玩家 100→100 未受伤**（owner 排除生效）。⚠️ **未验证**：实际按键手感、火球**飞行途中**命中、动画播完自动回 Idle —— 因 MCP 无法模拟输入且**测试时 Unity 窗口未聚焦导致游戏循环暂停**（`frameCount` 卡在 2），逐帧行为只能靠手动激活/反射调用验证。⚠️ 实现中又踩了一次 **`execute_code` 不支持局部函数**（C# 6）的坑，已记入技能。 |
| 2026-10-05 | **✅ M2.3 完成（用户接入）+ 🔄 特殊攻击形态改定为「火球 · 远距离」+ 🧹 工作区清理**。① **M2.3 已完成，且走的路线比原计划更优**：4 段连段动画**由用户从工程内已有的 `Assets/Magical-Knight_Set/Animation/Humanoid/` 提取**（那是一个 **100+ 动作的完整 Humanoid 动作包**：8 向 strafe、4 向 roll、`hit_*`/`dead_01~05`/`rise_*`、`atk_sword01~08`、`atk_energy01~11`、combo、跳、翻滚等），接到 `PlayerAC`：`Attack1` → `1_atk_sword03`（1.000 s）、`Attack2` → `2_atk_sword04`（1.200 s）、`Attack3` → `3_atk_sword05`（1.133 s）、`Attack4` → `4_atk_sword01`（1.633 s），**四段各自挂 `OnAttackHit`**（0.233/0.333/0.567/0.433 s）。同时 `Locomotion` 换成 `Idle01` + `strafe_run_strafe_front`，`Dash`→`roll_front`、`Hit`→`hit_light_F_body`、`Death`→`dead_01` → **旧 TKD 剪辑（`combo_01_1~4` 等）已彻底不再使用**，全部移入 `Art/Animations/Player/old/`。<br>⚠️ 我此前按原计划实际**跑通了整条 Blender/BVH 生成链路**并产出过 `SwordCombo_Roskva.fbx` + 4 段 `.anim`，但**质量不如上述手工动画**（我的版本 4 段全部等长 0.967 s，缺节奏差异）→ **该路线放弃，产物与工具已清理**（`Editor/SwordComboImporter.cs`、`SwordCombo_Roskva.fbx` 工程内与源目录均已删除）。若将来仍需，可用 `D:/ArtAssert/tools/sword_combo/make_sword_combo.py --process` 从现有 `sword_combo_01~04.bvh` 重生成（链路已验证）。<br>② **特殊攻击形态改定（用户补充设定）**：**游戏背景允许使用魔法 → 剑的特殊攻击是「发射火球、远距离攻击」**，**取代**原「剑 · 插地（`SwordSlam`，以自身为中心的圆形范围伤害）」。动画 `atk_energy01`（1.067 s）已挂到 `PlayerAC` 新增的 `Special_Attack` 状态。**代码侧缺口（实测）**：该状态目前是**孤岛** —— `behaviours = 0`、转出 transition `0`、**能进入它的 transition 也是 0**；`PlayerState` 枚举无 `Special`、`PlayerFSM` **零特攻逻辑**；`atk_energy01` **无判定事件**；`Weapon_Sword` 的 `specialType=1(SwordSlam)` / `specialRange=3.5` **需改**为火球类与远距离（如 12~15 m）。⭐ **可大量复用**：`InputService.SpecialPressedThisFrame` ✅、`PlayerEnergy.TrySpendSpecial()` ✅、`Gameplay/Enemy/Bullet.cs` + `Bullet.prefab`（弹道/命中/lifeTime）✅。<br>③ **工作区清理**：`.workbuddy` **76 M → 7.0 M**（删 `diag/` 38 M 剑 UV 渲染图、`backup/` 里剑 FBX 的 `bak2~6` 30 M、以及 47 个 `args_*.json` + 47 个诊断 `*.cs` + 日志/一次性探针；**保留**记忆、`bak7` 回滚点、`PlayerAC.controller.bak`、`prefabs-2026-10-05/`、文档引用的 `blender_sword_uv_split.py` / `blender_rename_sword_mat.py`、以及 46 个剑 UV 流程脚本）。⚠️ 顺带确认 **`.workbuddy/` 在 git 仓库之外**（仓库根 = `Demo-main/`），不受版本控制。工程内删除：`SwordCombo_Roskva.fbx`（未引用）+ 三个失效 Editor 脚本（`CreateNewPlayerPrefab.cs` / `RoskvaSwordRig.cs` / `SwordComboImporter.cs`，其引用的 5 个 FBX 有 4 个早已缺失）。**编译 0 error 0 warning**。 |
| 2026-10-05 | **✅ M2.2 装配剑完成（数据侧）**。把 `Weapon_Sword.asset` 挂到 **`Player1.prefab` / `Player2.prefab` 的 `PlayerAttack._weapon` 与 `PlayerEnergy._weapon`**（各 2 处）；⚠️ **刻意挂在预制体而非 Level 场景实例上** —— V1 语义是"进关卡默认装配剑"（角色级属性），挂预制体还能自动覆盖场景实例（**实测场景实例 0 条 override 即继承**）且不弄脏场景。**做过备份**：`.workbuddy/backup/prefabs-2026-10-05/`。 <br>⚠️ **重要发现：ROADMAP 原有的验收判据在现状下无法区分装与不装** —— `Weapon_Sword` 与 `PlayerConfig` 兜底值**几乎逐项相同**：伤害 `[12,15,10,20]` = `attackDamage`、体力 `[8,10,12,15]` = `attackEnergyCost`、特殊体力 30 = 30、连段窗口 1 = 1；判定盒尺寸虽然"只在武器里"，但**预制体上的既有尺寸（1.50/1.60/2.50 … 1.86/1.60/3.10）恰好已等于剑的数值**。→ 所以"判定盒变 2.5~3.1、伤害变 12/15/10/20"这套判据**看不出任何变化**（`damage`/`energyCost` 是 M2.1 从 `PlayerConfig` 复制成同值的）。 <br>✅ **改用「换武器探针」做真实验收**：在 **Play 模式**用 `Weapon_Spear`（判定盒 3.2~3.9 / 1.0~1.2、连段窗口 0.9）当探针调 `PlayerAttack.SetWeapon()`：<br>· ① 进 Play（`Awake` → `ResolveWeaponData()`）→ `Weapon = Weapon_Sword`，判定盒 (1.50,1.60,**2.50**)…(1.86,1.60,**3.10**)，`combopWindow=1`<br>· ② `SetWeapon(长枪)` → 判定盒变为 (1.00,1.60,**3.20**)…(1.20,1.60,**3.90**)，`combopWindow=0.9` ← **尺寸与窗口都随之变化，配置驱动确认生效**<br>· ③ `SetWeapon(剑)` → 全部换回<br>（`SetWeapon` 仅运行时生效，退出 Play 即还原 —— **实测场景未被写回**，场景里武器 GUID 出现 0 次。）<br>⚠️ **新记录一个陷阱**：`_weapon` 在 `PlayerAttack` 与 `PlayerEnergy` 上**各有一份**，`SetWeapon()` 只改自己那份 —— 实测 ② 步只调 `PlayerAttack.SetWeapon()` 时，`PlayerEnergy._weapon` **仍停留在 Weapon_Sword**。→ **将来做武器切换（§13）必须同时调两者**，否则会出现"伤害按新武器算、体力按旧武器扣"。建议后续收敛为统一入口（如 `WeaponHolder`）。<br>⭐ **M2.2 的真实价值在 M2.4**：`specialType=1(SwordSlam) / specialDamage=30 / specialEnergyCost=30 / specialRange=3.5` **只存在于武器配置**，不装配武器则特殊攻击无数据来源。 |
| 2026-10-05 | **📄 实查更正「动画来源」——`Walk` 已就绪、T26 换了文件**（本轮**只动文档**）。① `OVR - Roskva_Animated.fbx` 确已删除，但其 5 段动画**已由 `OVR - Roskva_SwordInHand.fbx` 替代**（Humanoid，`animationType:3`/`avatarSetup:1`）：`Idle01` 37.33s / `Talk01_old` 29.33s / `Talk02` 33.33s / `Talk03` 37.33s / **`Walk` 2.47s**，**全部 `isHumanMotion=True`**。→ ✅ **M3「小怪巡逻」所需 `Walk` 不再缺失**（早先"需重新导出"的结论已过期）。② **T26 跟着换了文件**：`OVR - Roskva_SwordInHand.fbx` 的 Avatar 是 **54 项映射、缺 `Neck`**，而 `Roskva.fbx` / `Roskva_Sword.fbx` 都是 **55 项、有 `Neck`**。⚠️ 因其承载 `Talk01_old`/`Talk02`/`Talk03` 三段**对话动画**，颈部不重定向会比较僵 → **建议正式使用 `Talk*` 前先补**（Rig → Configure → `Neck` → `Bip001-Neck`）。③ 现状补充：`PlayerAC` 已新增 `Idle01`/`Talk01_old`/`Talk02`/`Talk03`/`Walk` 五个状态（`Idle01` 已接进 `Locomotion` 混合树），但**`Attack1~4` 仍指向旧的 `combo_01_1~4`**、`Dash`/`Hit`/`Death` 也仍是旧剪辑；旧剪辑已移入 `Animations/Player/old/`（⚠️ 移动不改 GUID，引用仍有效）。④ 用户已保存 `Game.unity`：**场景里 `Player.prefab` 的引用归零、`Player2.prefab` 416 处**（此前"磁盘场景 356 处悬空引用"的风险**已解除**）。 |
| 2026-10-05 | **✅ 修复待机时手腕变形（根因：Biped 扭转辅助骨未被 Humanoid 映射）+ 新增 `BipedTwistBoneFixer`**。**症状**：两个 Roskva 模型待机时左右手腕都有变形。**根因**：`Upper Body` 网格（3506 顶点 / 62 骨）蒙皮依赖 4 根 **3ds Max Biped 扭转辅助骨** —— `Bip001-L/R-ForeTwist`（父级 `Bip001-*-UpperArm`，是 `Forearm` 的**兄弟而非子级**；总权重 **57.5** / 329 顶点）与 `Bip001-L/R-ForeTwist1`（父级 `ForeTwist`；总权重 **20.3** / 228 顶点）。这两级在 Max 里由 Orientation Constraint 跟随前臂拧动，但**两个 Avatar 的 human 映射各 55 条、含 Twist 的 0 条**（meta 双向复核）→ **Humanoid 的 Animator 只驱动映射过的骨** → 前臂转 14.87° 时扭转骨原地不动 → 网格被撕扯。**实测（`Player1` + `Idle01` @0.5s）**：`UpperArm` 23.26°、`Forearm` 14.87°、`Hand` 6.91°，而 `ForeTwist`/`ForeTwist1` **均为 0.00°**；`Forearm↔ForeTwist` 夹角由绑定态 10.17° 被拉到 23.97°。**顶点位移**（手腕区，扭转权重≥0.2 的 151 点/侧）：0.5s→10.0 mm、12s→2.6 mm、**20s→45.0 mm**、30s→35.8 mm，而**手腕半径仅约 30 mm**；对照组（扭转权重≈0 的 926 点）几乎不动 → 证明问题精确局限在这批顶点。**修法**：新增 `Gameplay/Player/BipedTwistBoneFixer.cs` —— `LateUpdate` 里令 `扭转骨.rotation = 前臂.rotation * offset`，`offset = rot(bindpose 前臂)⁻¹ * rot(bindpose 扭转骨)`。⚠️ **`offset` 必须从 bindpose 反算**，不能在 `Awake` 里读当时的 `rotation`（依赖"唤醒时 Animator 还没播过动画"的时序假设）；**位置无需处理**（扭转骨与前臂同挂 `UpperArm`、`localPosition` 恒定，刚性跟随）。**验收**：① 绑定姿态下施加修正**恰好 0.0000°**（严格 no-op）；② 动画中相对夹角**恒等于绑定值**（右 10.17° / 左 10.17°，未修时右漂到 23.97°→47.19°、左漂到 65.90°→68.18°）；③ **Play 模式第 276 帧实测 4 根骨与应有姿态偏差全部 0.00000°**。**已挂** `Player1.prefab` / `Player2.prefab`（`Player.prefab` 上也挂过一次，但该预制体随后已从磁盘删除）。⚠️ **两条排除结论（别走死路）**：**(a)** 把扭转骨填进 Avatar 的 `Left/Right Lower Arm Twist` 槽位**实测无效**（填后 `ForeTwist` 仍 0.00°）—— 动画是在没有扭转骨的骨架上做的，muscle 里没有扭转数据；**(b)** `Bip001-RUpArmTwist`、`Bip001_B_L/R-Thigh-Twist`、持剑版多出的 `Bip001-R-Hand.001/002`、以及全部 `*_end` 叶子骨**蒙皮权重全为 0.0**，纯辅助骨无害。 ⚠️ 顺带实测坑：`execute_code` 的编译器只到 **C# 6**（不支持局部函数 / `out var`）；且 `_end` 后缀会让"名字含 ForeTwist"的匹配把持剑版错匹配成 8 对（已加后缀排除，现为 4 对）。**未改动**任何 FBX / meta / 场景。 |
| 2026-10-05 | ⚠️ **现状变更（非本次改动所为，仅记录）**：`Assets/_Game/Prefabs/Player/Player.prefab` 与 `.meta` **已从磁盘删除**（该预制体在 git `HEAD` 中仍存在，可恢复）。⚠️ **连带风险**：磁盘上的 `Assets/Scenes/Game.unity` 仍引用它的 GUID `6eec6966…` **356 处** —— 若直接加载/提交该场景会显示缺失预制体。**待办**：确认删除是否有意；若是，则需把场景里的 Player 实例换成 `Player1`/`Player2`（用户当前内存中的场景已换成 `Player2` 实例，只是尚未保存）并提交。 |
| 2026-10-05 | **✅ 剑首（柄端突起）改为独立分段，复用护手的粗糙金属贴图（用户反馈"剑柄末尾有一个突起的物体，也应该是和护手类似的粗糙金属材质"）**。**原问题**：首版只分三段，`t < 0.195` 整段都算"剑柄+剑首"共用一个材质槽与一块皮革贴图 → 剑首被打成皮革色。**实测分段（面中心沿主轴归一化位置）**：<br>· **剑首** 0.0000..0.0520，半径 0.0001..**0.0222**（峰在 t=0.018），96 面<br>· **剑柄**（握把本体）0.1030..0.1541，半径 0.0085..0.0165，16 面（⚠️ 中间**没有顶点环**，是 8 个裸长面）<br>· **护手** 0.1872..0.2271，半径 0.0135..0.1138，120 面<br>· **剑身** 0.2380..1.0000，184 面<br>**修正**：脚本扩为 **4 段分区**，新增 `--seg-pommel`（默认 0.058）；**剑首与护手共用槽 0 与同一块贴图区域**（两者都是装具件，外观一致 → **不需要第 3 个材质槽，`meta` 也不用改**）；因剑首**半径与轴向 t 同样不单调**（0.0003 → 0.0222 → 0.0077 → 0.0135），**横向参数用 `radius`**（与护手同规则），让「半径最大 = 贴图高光侧」。**采样校验**：剑首 `#64605E`、护手 `#62605E`（**两者一致 ✅**）、剑柄 `#533630`，**四段黑面数全部为 0**。**校验**：顶点 273 / 索引 1248 不变、材质槽 2 个、UV 320 点 `ByPolygonVertex`、材质分配 `ByPolygon`。备份 `.workbuddy/backup/OVR - Roskva_Sword.fbx.bak7`。 ⚠️ **踩坑记录**：`--seg-grip` 必须 **< 0.1872**，否则 t=0.1872 那一圈（半径 0.019–0.024，实为**护手底座**而非剑柄）会被误判成剑柄（默认已由 0.195 收到 **0.185**）。 |
| 2026-10-05 | **✅ 修正护手的 UV 映射方向（用户反馈"高光部分应该是护手的边缘"）**。**根因**：护手是**向两侧伸展的弧形横档**（不是圆盘），实测其**到主轴的垂直距离（半径）与沿轴位置 `t` 不单调** —— 沿轴依次 **0.0296 → 0.0869 → 0.1131 → 0.0380 → 0.0558 → 0.1039 → 0.0204**（先升后降再升再降）。而首版拿「轴向 `t`」当径向参数 → 一部分**外缘顶点落到贴图暗区**、内圈落到亮区 → **高光/纹样错位**。**修正**：新增 `--param-<段> axis 或 radius` 开关，**护手改用真实半径**做横向参数，并让**半径最大 = 矩形 `pu=1` 端 = 贴图高光侧**（护手贴图横向亮度递增：U 0.225 处 0.060 → U 0.251 处 **0.297**）；剑柄仍是细长件，继续用轴向。**验证指标（边缘亮度/中心亮度）**：<br>· 旧（轴向 t）中心 0.0866 / 边缘 0.1158 = **1.34×**，且 **U 实际只跨 0.0027**（几乎没用上贴图渐变 —— 这就是"错位"的原因）<br>· **新（半径）中心 0.0670 / 边缘 0.1914 = 2.86×**，U 跨 **0.015** ✅<br>**校验**：顶点 273 / 索引 1248 不变、材质槽 2 个、UV 327 点、材质分配 `ByPolygon`、`meta` 无需改动。备份 `.workbuddy/backup/*.bak6`。 ⚠️ 仍属自动近似展开，纹样走向与原设计不必一致；`--param-guard/--rev-guard/--pad` 均可调。 ⚠️ 顺带确认：护手**并非**原判断的"薄盘"，而是**带双翼的弧形横档**（侧视可见左右两侧弧形 + 中间连到剑身）。 |
| 2026-10-05 | **✅ 按用户指认的贴图区域修正剑的 UV（模板匹配定位）**。**用户提供两张截图**，指认：① 深灰金属片 = 护手 + 剑柄末端金属件；② 深棕皮革（带红带）= 剑柄。**手法**：写 `match_templates.py` 用 **FFT 归一化互相关（NCC）** 把这俩截图在 `Equips_Diffuse.png` 上做模板匹配（多尺度 0.2~1.6），**匹配分数 0.905 / 0.693（很高，确认就是同两块）**：<br>· **护手** → U 0.2109..0.2646 / V 0.1582..0.3125（像素 X 216..271, Y 704..862；深灰金属，平均 `#6F6862`，填充 67%）<br>· **剑柄** → U 0.5234..0.6104 / V 0.4766..0.6426（像素 X 536..625, Y 366..536；深棕皮革+红带，平均 `#624336`，填充 87%）<br>⚠️ **NCC 实现踩了个坑**：第一版分母多乘了 √n（`sqrt(sum(I²)−sum(I)²/n) · tsd·n`），导致所有分数都是**负数**且分布异常；正确分母是 `sqrt(var_I) · sqrt(sum((T−mean_T)²))`，且**局部统计量必须用积分图**而不是 FFT 重复推。<br>**紧边界**：实测两块矩形**边缘都带黑**（护手左右仅 19%/26% 填充、剑柄下边 2%）→ 按「行列内容率 ≥90%」取紧边界再留 **12% 内缩**：护手 U 0.2246..0.2510 / V 0.1582..0.3125、剑柄 U 0.5322..0.6104 / V 0.4844..0.6377。**结果：剑柄 0/116、护手 0/116 面落在黑边上**。**渲染核对**：护手深灰金属、剑柄深棕皮革带红带、剑身银灰钢 —— **均与用户截图一致**；⚠️ **偏暗是正常的**（贴图本身色调就暗）。**校验**：顶点 273 / 索引 1248 不变、材质槽 2 个、UV 327 点 `ByPolygonVertex`、材质分配 `ByPolygon`、`meta` 无需改动。备份 `.workbuddy/backup/*.bak5`。 ⚠️ 仍属**自动近似柱面展开**，纹样走向与原设计不必一致；参数 `--uv-grip/--uv-guard/--pad/--swap-grip` 均可调。 |
| 2026-10-05 | **✅ 修正剑的 UV 映射（用户反馈"映射不对"）+ 新增内缩机制**。**问题**：首版自动 UV 把剑柄映射到 U 0.219..0.281 —— 实测该处是贴图的**暗棕区**，且**区域边界压到黑色背景**，导致 25/116 面渲染成黑块；护手同理（38/116 面黑）。**排查手法（可复用）**：① `blender_diag_uv2.py` 把三段**分别染红/绿/蓝渲染**，确认几何分区本身正确；② `scan_tex_regions.py` / `find_tex_blocks.py` 扫描贴图找连通块 —— ⚠️ **发现整张贴图的装备件之间几乎没有纯黑间隔**（前景占 84.8%），只有一块 107×247 的金属件独立（U 0.896..1.0 / V 0.752..0.993），**但填充率仅 62%**，矩形映射必带黑面（实测 24/116）→ 未采用；③ `scan_subrect.py` 做**逐行可用率**分析，扫出真正连续的暖金长条：**U 0.135..0.215 / V 0.289..0.402**（前景率 83~100%，左图色 `#AE8D67`~`#BC966B`）。**修正**：剑柄 → U 0.137..0.213 / V 0.291..0.400；护手 → 同 U、V 0.401..0.462（与剑柄同一片金色区，本身也都是装具，合理）。**新增 `--pad`（默认 0.22）内缩机制** —— ⚠️ 剑柄是**圆锥形**，柱面展开必然有面贴到区域边沿，**若边界压着黑背景那些面就变黑**；把所有 UV 往矩形内部缩后：**剑柄 0/116、护手 0/116 面黑**，平均色 `#B69365` / `#BD9770`（暖金）。**校验**：顶点 273 / 索引 1248 与原来一致；材质槽 2 个；UV 327 点 `ByPolygonVertex`；材质分配 `ByPolygon`；`meta` 无需改动。备份 `.workbuddy/backup/*.bak4`。 ⚠️ **仍属自动近似柱面展开，纹样走向与原设计不必一致**；`--seg-grip/--seg-guard/--uv-grip/--uv-guard/--pad` 全部可传参，用户实机核对后可再调。 |
| 2026-10-05 | **✅ 给剑补 UV + 拆双材质槽（剑柄/护手走 Equips 贴图，剑身用钢）**。**用户需求**：「剑柄和护手和剑身不应该用一样的材质」，并指出 `Equips_Diffuse.png` 里有剑柄/护手的贴图（原本与 Equips 一体）。**根因（三层，逐层查证）**：① 源文件 `Roskva_Sword_Hand.fbx` **完全没有 UV 层**；② `OVR - Roskva_SwordInHand_Full.fbx` 里剑**有 UV 层但值全是 (0,0)** —— 因为用户的 `D:/ArtAssert/tools/model_check/merge_sword_inhand.py` 第 128 行用 `smesh.from_pydata(verts, [], faces)` 建网格，**该 API 不带 UV**；③ 建模时就没做过 UV 展开。→ **材质配得再对也没用**（无 UV ⇒ 全顶点采样同一点 ⇒ 一片纯色）。**已验证可行路径**：① 用「背剑版 Equips 4749 顶点 − 持剑版 4578 顶点」定位到被删的 **171 个剑顶点**（按位置哈希匹配），取出其 UV → 剑的 UV 分散在 **5 块区域**，主要为 U 0.219..0.281/V 0.125..0.344（左下金色编织纹，48%）与 U 0.906..1.000/V 0.750..0.875（右上金属件，37%）。② 用 PCA 测出剑的几何分区（主轴占方差 99.0%，全长 1.3199）：**剑柄+剑首** 归一化位置 0..0.205（半径 0.022）、**护手** 0.187..0.238（半径最粗 0.114）、**剑身** 0.238..1.0（0.023→0.002 渐尖）。**执行**：新脚本 `.workbuddy/blender_sword_uv_split.py`（两段式，默认只分析）—— 按沿主轴位置把面分三段 → 为剑柄/护手做**柱面展开 UV** 映射到上述贴图区域 → 拆成 **2 个材质槽**（槽0 `NPC_Roskva_Parts` / 槽1 `NPC_Roskva_Sword_Blade`），材质分配方式 `ByPolygon`。**校验**：顶点 **273 / 索引 1248 与原来完全一致（几何无损）**；剑 Model 材质槽 **2 个**；UV 数组 **327 点**、映射方式 `ByPolygonVertex`；骨骼 365 不变。**替换工程内 FBX（保留 `.meta` → GUID 不变）**，⚠️ **`.meta` 无需改动** —— 已有的 7 条重映射里 `NPC_Roskva_Parts` 与 `NPC_Roskva_Sword_Blade` 都在，Unity 会自动接对两个材质。⚠️ **UV 是自动近似的柱面展开**，纹样走向可能与原设计不同，**需实机核对、可再调**（脚本的分区比例与 UV 矩形都是可传参的）。**备份**：`.workbuddy/backup/OVR - Roskva_Sword.fbx.bak3` / `...meta.bak3`。 ⚠️ **顺带纠正两处自己的取数错误**：`extract_sword_uv.py` 曾把 `SWORD_START` 写成独立剑顶点数（273）而非 `len(vf)-len(vh)`（27170）；UV 取值必须用**数组位置**索引 `UVIndex`，不能用顶点索引（否则 UV 范围会算成占满全图）。 |
| 2026-10-05 | **✅ 路 A 落地：给剑换独立材质（`OVR - Roskva_Sword.fbx` 重导 + meta 补第 7 条重映射）**。**背景**：上一轮查明剑网格 `Roskva_Sword_Hand` 的材质 `NPC_Roskva_Parts` 与 `Equips`(6060面)/`Fur`(3171面) **共用** → 无法单独给剑换材质。用户选**路 A**（Blender 侧给剑独立材质名）。**执行**：① 可复用脚本 `<工程>/.workbuddy/blender_rename_sword_mat.py`（两段式，默认只分析；沿用用户 `remove_back_sword.py` 的风格）。⚠️ **关键手法**：**复制** `NPC_Roskva_Parts` 再给副本改名 `NPC_Roskva_Sword_Blade` —— **绝不能直接改原名**（否则 `Equips`/`Fur` 会一起变成金属）。Blender 5.2 命令行（`D:/Blender/blender.exe -b -P ...`）跑通，导出到**源目录新文件** `D:/ArtAssert/baidu/Roskva/_model/fbx/OVR - Roskva_Sword_BladeMat.fbx`（不覆盖用户原件）。② **导出前后逐项校验**：网格 **10/10 同名同数**、**10 个网格的顶点数/索引数逐一完全相同**（剑仍 819/1248）、骨骼 **365/365 一致**、Bip001 **143/143 一致**、材质 **6→7**（只多出剑的独立材质）、meta 的 **55 项 human 映射在新文件中全部命中（缺失 0）**。差异仅：少一个 `Armature.001` 零子物体空节点（无害）、体积 4.39→6.38 MB（**几何数组的类型与字节数完全相同**，差异只在元数据，不影响 Unity 导入）。③ **替换工程内文件**：备份原 `.fbx`/`.meta` → `.workbuddy/backup/*.bak2`，只覆盖 `.fbx`（**保留 `.meta` → GUID 不变 → Humanoid 配置不丢**）。④ **meta 追加第 7 条重映射**：`NPC_Roskva_Sword_Blade` → `Mat_Roskva_Sword_Blade.mat`(guid `771036122bd64c5182f013a5a049053c`)。**回读校验：animationType 3 / avatarSetup 1 / human 55 项 / 重映射 7 条（6 旧 + 剑专属）**。⚠️ **仍未解决**：剑网格**无 UV** → 贴图材质对它依旧无效，只能纯色；要"剑身钢 + 剑柄/护手走装备贴图"须 Blender 侧**补 UV + 拆 2 个材质槽**。⚠️ **顺带定位**：`D:/ArtAssert/baidu/Roskva/` 是素材源目录（含 `OVR - Roskva.blend`、`tools/remove_back_sword.py`、`tools/retarget_to_humanoid.py`、`anim/sword/sword_combo_01~04.bvh`）；`Roskva_Sword_Hand.fbx`/`Roskva_Sword_Sheathed.fbx` **在源目录存在但未进工程** —— 这解释了此前"三个 Editor 工具脚本引用的文件不存在"。**Blender 5.2 位于 `D:/Blender/`（可命令行调用）。** |
| 2026-10-05 | **✅ 适配 `OVR - Roskva_Sword.fbx`（持剑版玩家模型）**（本轮**只动 2 个资产文件，无代码改动**）。**背景**：用户准备做"背剑模型（村庄）+ 持剑模型（战斗关卡）"的双预制体方案，需要把新模型改成 Humanoid 并接好材质。**实查结构**：该 FBX 含 **10 个网格**（9 个与 `OVR - Roskva.fbx` 同名 + `Roskva_Sword_Hand`）与 **6 个同名材质**，**骨架是 Roskva 的超集**（Roskva 的 179 根骨骼全部包含，多出的 186 根全是 Blender 自动生成的 `*_end` 叶子骨）。① **改 Humanoid**：`animationType 2→3`、`avatarSetup 0→1`，并把 `OVR - Roskva.fbx.meta` 里那 **55 项 human 骨骼映射原样复制**过去（⚠️ 复制前已程序化校验：**55 根骨骼在剑版中一根不缺，缺失 0**，故为确定性映射而非依赖自动推断）。② **材质重映射**：把 Roskva 的 **6 条 `externalObjects`** 全部复制 → 人物材质直接复用现有 `Mat_Roskva_*.mat`，与 Roskva **完全一致**。③ **新建 `Mat_Roskva_Sword_Blade.mat`**（URP Lit，抛光钢：基色 (0.82,0.85,0.90)、金属度 1、光滑度 0.8，**无贴图**、关键字清空）。⚠️ **两项关键实测结论**：**(a) 剑网格 `Roskva_Sword_Hand` 完全没有 UV 层** —— 另外 9 个网格都有 `LayerElementUV`（Equips 的 UV 数组有 9156 个），唯独剑只有 Normal + Material。**所以"剑柄/护手的材质不能正常使用"**：没有 UV → 819 个顶点全部采样到贴图的同一像素（UV 0,0）→ 整把剑渲染成一片纯色。**(b) 剑只有 1 个材质槽**（`LayerInformationType = AllSame`，Materials 数组长度 1，材质名 `NPC_Roskva_Parts`，与 `Equips`/`Fur` **共用**）→ 无法只给剑身单独一个材质，也无法通过改名重映射区分（按材质名匹配会连带把整套装备和毛发一起改掉）。**绕过办法**：剑的材质名在 FBX 里改独立（如 `NPC_Roskva_Sword_Blade`，随后在 meta 加一条重映射），或在该渲染器上直接覆盖材质。⚠️ **同批发现**：`Assets/Editor/` 下三个未跟踪工具脚本（`CreateNewPlayerPrefab.cs` / `RoskvaSwordRig.cs` / `SwordComboImporter.cs`）引用的 **5 个 FBX 全部已不存在**（`OVR - Roskva_NoSward.fbx` / `Roskva_Sword_Hand.fbx` / `Roskva_Sword_Sheathed.fbx` / `SwordCombo_Roskva.fbx` / `Roskva_Sword_R.fbx`）→ **运行会直接弹"找不到模型"**。**另**：`OVR - Roskva_Animated.fbx`（M3 巡逻所需的 `Walk` 动画源）**已被删除**。 ⚠️ 备份：`D:/Unity/Unity Project/CurriculumDesign/.workbuddy/backup/OVR - Roskva_Sword.fbx.meta.bak` |
| 2026-10-05 | **🔄 计划变更：关卡结构改为「多关卡独立场景」+ 武器范围收敛为「只做剑」**（本轮**纯文档，无代码改动**）。**用户决策**：① 放弃"完整场景（ARPG 单场景）"路线，回到**关卡制** —— 且明确为**多个独立关卡场景**（`Level_01` / `Level_02` / `Level_03` 各自一个 `.unity`），含关卡流转与选关；② 多武器改为**额外项目**，V1 **只做剑**。**文档改动**：① `GDD.md` —— 头部表格与变更记录、§1.1/§1.2、§2 核心玩法循环（流程图重绘为多关卡递进）、§3.1 范围（武器收敛 + 多关卡编排 + 选关）、**新增 §3.3 额外项目 / 加分项**、§4.2 移动、§4.6 特殊攻击（标注 V1 只做剑）、§5.1–§5.4（武器范围 + 剑的专用连段动画）、**§9 关卡与场景（重写为 3 关独立场景 + 关卡机制 + `LevelFlow` 需求）**、§10.1/§10.2、§12 资源表、§13.1/§13.3/§13.4、§14 里程碑（新增 M3.5 关卡）、§15。② `ARPG-DIRECTION.md` —— **整份重新定位**为"**方案选型存档（❌ 未采纳）**"：标题/头部/§0 改为撤回结论，§4/§5/§6/§7/§8 标注失效，⚠️ **保留 §4.6 相机实现（唯一仍然生效的产出）**，并修掉 §6.1 后遗留的**断裂表格残片**。③ `ROADMAP.md` —— 头部、§0 进度树（M2 收敛为剑 / M3.5 改多关卡 / 新增额外项目线）、§4 M2 章节（M2.2 → "装配剑"、新增 M2.3"剑的模型 + 连段动画接入"、M2.4 剑·插地、长枪移出）、**新增 M3.7"多关卡场景拆分 + 关卡流转"**、§7/§9/§10/§11/§12、**新增 §13 额外项目章节**。④ `PROJECT-CONTEXT.md`（本文）—— 头部状态与下一步、§1 结构说明、§3 目录地图（补 2 个新 Editor 脚本 + 剑 fbx）、§5.5 关卡与流程（标注 `LevelFlow` 待新增）、`CameraFollow` 描述（俯角 40→30、上限 80→60）、§10 进度、§10.1 决策速查第 13/14 条、§11 索引。⑤ `README.md` 头部/进度/阅读顺序。⑥ `ART-ASSETS.md`、`ART-PIPELINE.md`、`NAVMESH-GUIDE.md`（加"房间 → 关卡"对照注）。 ⚠️ **同批发现的现状**：工作区新出现 `Editor/RoskvaAttachSword.cs`（挂剑）、`Editor/SwordComboImporter.cs`（提取挥砍 + 挂判定事件）与 `Roskva_Sword_R.fbx`（剑模型，**已生成**）—— 剑的接入工作**正在进行**，但 `SwordCombo_Roskva.fbx`（挥砍动画源）**尚不存在**。 **提交 `668f5c7`（相机调参入库）。** |
| 2026-10-05 | **✅ 相机视角可以上抬了（允许仰视 + 动态地面安全角）**（提交 `2ca175f`）。**问题**：鼠标上推到底，视角停在俯角 5° 不能继续上抬。**根因**：`_minPitch = 5`（正值）把俯角锁死在"永远俯视"。它取正值是为防穿地但**过于保守** —— 相机离地 = `focusHeight + sin(pitch) × distance`，焦点 1.0 + 距离 3 时真正贴地临界是 **−19.5°**，即 **5° ~ −19.5° 这 24.5° 本来是安全的，却被整个砍掉**。深层原因：本版相机**故意删掉了防穿地修正**（每帧 SphereCast 会导致机位跳变→抖动），没有碰撞兜底就只能用 `_minPitch` 硬拦，一拦就把"上抬"整个砍掉了。**方案（用户选定）**：新增动态「地面安全角」`asin((groundY + clearance − focusY) / distance)`，并有三个关键设计点 —— ① ⚠️ **钳制「俯角」而非「机位高度」**：钳制高度会压缩相机距离、且必须改用 `LookAt` 才能让角色居中；钳制俯角则保持距离不变、机位仍从焦点沿视线推出 → **角色继续自动居中**（实测点积 = 1.000000）；② ⚠️ **安全角必须随距离变化**（距离越远允许仰角越小）：1.5/3/5/10 m → −27.8°/−13.5°/−8.1°/−4.0°，写死常数会在拉远时穿地；③ ⚠️ **地面探测必须忽略角色自身碰撞体**（实测天真写法命中 `Player @ y=1.6`，会把地面当成 1.6）。**改动**：`_minPitch` 5→**−60**、新增 `_groundY`/`_groundClearance`/`_autoDetectGroundY`、`ResolveGroundY()`/`GroundSafeMinPitch()`/`EffectivePitch()`/`ClampAboveGround()`。**验证**：各距离相机高度恒为 0.300 m **均不穿地** ✓；最低俯角时相机 0.3 m < 焦点 1.0 m → **确实在仰视** ✓；角色仍在画面正中心 ✓。俯角范围 5°~80°（75°）→ **−13.5°~80°（93.5°）**，新增 18.5° 仰视空间。⚠️ 仍未做（用户决定细节优化后议）：防穿墙、极端仰角+最近距离时相机贴脸。 ⚠️ 同步修正文档中 `_followTargetYaw` 误写为 `true`（实测 false）与"固定俯角 40°"的不准确措辞 |
| 2026-10-05 | **📄 文档整理（本轮无代码改动）**：全文实查后发现并修正多处**过时表述**，使文档与代码一致。① **头部状态**：由"阶段 0 + M1.1"更新为"M1 全部 + M2.1"，并写明下一步。② **§3 目录地图**：角色由 `Y Bot` 改为 **Roskva**（含 Meshes/Materials/_model/_textures/anim 子目录）、新增 `Gameplay/Combat` 与 `Data/Weapons`、Editor 补 `HitboxAnimationValidator.cs`、标明 `_Modules/` 已删除。③ **§5 类清单**：`WeaponConfig` 描述由"未被引用/命名空间 Game.Date"改为 M2.1 的实况；`PlayerAttack`/`PlayerEnergy` 改为判定体与武器驱动的实况（**删除"PlayEnergy.cs 不一致""从未被消耗"等已修好的旧问题描述**）；新增 **`Hitbox` / `HitboxController` / `HitboxTeam`** 三个类；`GameManager`/`CameraFollow` 描述重写（删掉"从不设 timeScale"与过时的 offset）；`ManaBar` → **`EnergyBar`**；Editor 补校验工具。④ **§6.2 战斗事件流**：由旧的"瞬时 `OverlapSphere`"重写为**判定体驱动**的真实链路，并补上「敌人受击反应（T21）」与「暂停/结算输入闸门」两张流程。⑤ **§6.4 动画事件清单**：补上实测时间，并记录重要提醒 —— `Elbow Uppercut Combo` / `Upward Thrust` 也挂了 `OnAttackHit` 但不属 4 段普攻，**M2 做特殊攻击时必须让它们用自己的判定体**。⑥ **§7 美术资产**：由 `Y Bot`/`idleAvatar` 重写为 **Roskva** 系列（含 Equips_NoSword、金发贴图、握持挂点），并写明 Humanoid 映射的正确验证方法（`isValid` 不够，`Neck` 是可选槽位）。⑦ **§10 当前进度**：由"体力从未消耗 / hitbox 未做 / 暂停菜单未做"（全部已完成的旧状态）重写为按阶段分组的真实进度 + 未修遗留问题表。⑧ **§11 文档索引**：由 5 份补全为 **8 份**（此前漏了 `ARPG-DIRECTION` / `ART-ASSETS` / `ANIM-GUIDE`），与 `README.md` 一致。⑨ **T5 标记已消除**（M2.1 完成"角色数值与攻击数值拆分"）；**T11** 补记第二处拼写错误 `VECTORY`；**T18** 标注"无暂停菜单"已不成立。⑩ 同步修正 `README.md`（头部/进度/下一步/最短路径）、`GDD.md`（§2 结束界面与暂停菜单、§4.5、§10.2/§10.3、§11.1、§12 现状表）、`ART-PIPELINE.md`（资源对照表 + 缺失清单）、`ANIM-GUIDE.md`（Humanoid 现状 + Avatar 复用目标）、`ROADMAP.md`（M5.1 体力条、§8.3 Humanoid 现状）。 **校验：9 份文档的表格列数、代码块闭合、内部链接全部通过。** |
| 2026-10-04 | **✅ M2.1 WeaponConfig 重做完成**（提交 `41472f2`/`1034503`）。⚠️ 文档记的"命名空间误写 Game.Date、零引用"的 `WeaponConfig` **已在阶段 0 清理死代码时删除** → 本次是**从零新建**。① 新增 `Data/WeaponConfig.cs`（`SpecialAttackType` 枚举 + 配置类，字段按 GDD §5.2 全到位，含取值辅助与 OnValidate 校验）。② 新增两把武器资产 `Data/Weapons/`：**剑**伤害 [12,15,10,20]/判定 长2.5~3.1·宽1.5~1.86/特殊插地；**长枪**伤害 [10,12,9,16]/判定 长3.2~3.9·宽1.0~1.2/特殊投掷（体现 §5.3 的手感定位，数值为初值待实测）。③ **全量接入**：`PlayerAttack` 读武器的伤害/连段窗口/段数/**判定盒尺寸**；`PlayerEnergy` 读武器的体力成本（成本表仍集中在本类，保持 M1.1 的统一出口原则）；`Hitbox` 新增 `SetDamage`/`ApplyShape`；`HitboxController` 新增 `GetByName`/`ApplyWeaponShapes`。④ `PlayerConfig` 的 attackRange/attackDamage/attackEnergyCost/specialEnergyCost 降级为兜底。**验证（临时实例，未触碰项目文件）：装配长枪后判定盒由 2.5~3.1 变为 3.2~3.9 且变窄（配置驱动生效）；模拟 4 段命中帧伤害依次同步为 10/12/9/16（各段独立正确）；连段窗口 0.9、段数 4。编译 0 报错 0 警告。** ⚠️ Player 仍未装配武器（装配属 M2.2）。⚠️ **另发现一个工具坑**：在编辑模式修改 `Time.timeScale` 会**持久化污染** `ProjectSettings/TimeManager.asset`（Unity 把 Fixed Timestep 改写成新的有理数格式）——暂停菜单的端到端测试触发了该写入，已 `git checkout` 恢复并验证 Refresh 后不再被改写。 |
| 2026-10-04 | **✅ M1.3 暂停菜单完成**（提交 `b7add1c`）—— **M1 阶段全部收口**。① 新增 `UI/PauseMenu.cs`：`ESC` 开/关，暂停时**两道闸同时上**（`Time.timeScale = 0` 冻结逻辑 + `InputService.SetGameplayInputEnabled(false)` **让鼠标不能再转视角**），带幂等保护、恢复时还原暂停前的 timeScale。② ⚠️ **`ESC` 必须不受输入闸门影响**：若放进 Player map 会被一起关掉 → **暂停后按 ESC 关不掉菜单**；故 `InputService` 新增 `PausePressedThisFrame`，刻意不走闸门（用旧版 `Input.GetKeyDown`，项目 Active Input Handling = Both 可用，零配置且避免与 UI 的 Cancel 撞车）。③ **`ESC` 职责转移**：`CameraFollow` 原用 ESC 解锁指针，与暂停菜单会打架（按一下做两件事）→ 已移除，ESC 统一归 PauseMenu；相机新增上升沿检测，输入恢复时自动重锁指针。④ `GameManager` 新增 `HasEnded`，避免结算后还能开暂停菜单。⑤ UI 用**复制结算面板**方式搭建（字体/按钮样式/过渡全部继承），组件挂在 **Canvas** 而非面板内（否则 `SetActive(false)` 会连带禁用脚本）。⑥ **顺手修掉第二处拼写错误**：`VictoryMsgText` 原为 `VECTORY !` → `VICTORY !`。**验证：Pause→面板可见/timeScale=0/输入关/LookDelta=0；Resume→全还原；幂等 ✓。未做"音量"（项目无音频，待 M5）。** |
| 2026-10-04 | **✅ T25 修复 + 动画 FBX 转 Humanoid**（提交 `c3f113f`，为用户在 Unity 中的 Rig 配置）。① **T25 已修**：`OVR - Roskva.fbx` 的 `human[]` 由 54 项补到 **55 项（完整）**，`Neck -> Bip001-Neck`；端到端抽检 11 个关键骨骼全部解析成功。② **重要进展**：`OVR - Roskva_Animated.fbx` 的 Rig 由 Generic 改为 **Human**，5 段动画全部 `isHumanMotion=True`、骨骼命名与玩家 **一致 54/54** → **可直接重定向**，M3「小怪巡逻」所需行走动画（`Walk`）的前提达成。③ 新增遗留 **T26**：`Roskva_Animated` 自己的 Avatar **仍缺 Neck**（54 项），用其动画时颈部不重定向，建议 M3 前补。 |
| 2026-10-04 | **✅ 修复「游戏结束后鼠标仍能转视角」**（提交 `cc40e56`）—— 根因是 **`Time.timeScale = 0` 拦不住输入**：`Update()` 在暂停时仍每帧执行，且 Input System 完全不受 timeScale 影响（相机又用 unscaledDeltaTime，照转）。① **在 `InputService` 这道唯一入口加闸**：`GameplayInputEnabled` + `SetGameplayInputEnabled()`，关闭时**只 Disable `Player` action map**（UI map 保留，否则菜单点不动），全部输入访问器加布尔拦截。② `GameManager` 在**结束瞬间**（不等结算面板的 2s 延时）锁输入，`Start` 恢复。③ **顺带修掉连带 bug**：`CameraFollow` 的指针锁定用旧版 `Input.GetMouseButtonDown`（同样不受 timeScale 影响），结算后会把指针锁回去导致**结算面板点不动** → 抽出 `HandleCursor()`，输入关闭时只解锁不锁回。**验证：Player map 可单独关闭且 UI map 不受影响；端到端输入全归零并可恢复；编译 0 报错 0 警告。** |
| 2026-10-03 | 创建本文档；完成全项目通读（未改任何代码）；建立 `Docs/` 文档目录，产出 `GDD.md`、`ART-PIPELINE.md`、`README.md` |
| 2026-10-03 | **记录第一批设计决策**（见 §10.1）：取消跳跃、体力硬性禁止、武器定为剑+长枪及其特殊攻击、敌人要视野+寻路+障碍物、Boss 定为人形、音频两种方式并用。**仍未改动任何游戏代码** |
| 2026-10-03 | **记录第二批设计决策**（见 §10.1 第 11–15 条）：存档用 JSON、寻路用自研 A\*、武器开局二选一、长枪全部细节（蓄力定距/直线/插地/同判定/瞄准减速/死亡留枪）、敌人受击打断规则。**仍未改动任何游戏代码** |
| 2026-10-03 | **修正打断规则**：由"敌人一律不打断"改为"**小怪会被打断、Boss 不会**"。连带把 T21 的结论**反转**——从"移除硬直"变为"**接通断裂的受击链路**"（现况是小怪挨打毫无反应）。**仍未改动任何游戏代码** |
| 2026-10-03 | **简化长枪**：取消蓄力与瞄准，改为**点按瞬间投出、固定距离**；**空手期间攻击键与特殊攻击键都能召回**。连带取消"长按三态输入 / 投掷指示器 UI / 蓄力数值"三项工作，并消掉 3 个待定项。**仍未改动任何游戏代码** |
| 2026-10-03 | **工程迁移**：从 `D:\Unity\xv\Demo` 迁到 `D:\Unity\Unity Project\CurriculumDesign\Demo-main`。本文档内所有工程路径已改为新位置；**Git 信息栏改写为实际状态**（迁移副本未带 `.git`，当前不是仓库、无版本历史）。**仍未改动任何游戏代码** |
| 2026-10-03 | **打通 AI 接入（MCP）**：实测 Unity MCP 链路（HTTP `127.0.0.1:8080/mcp`，服务端 v3.4.7，47 工具 / 19 资源），确认 Unity 侧已认到新工程路径与实例 `Demo-main@890c462facb3146d`、控制台 0 报错 0 警告。排掉两个坑并写入 §2：首次 `tools/list` 约 20 s（之后走缓存）、未设活动实例会返回 `no_unity_session`（已用 `set_active_instance` 设为全局活动实例）。**仍未改动任何游戏代码** |
| 2026-10-04 | **需求文档评审**（未改代码）：核对文档对现状的 12 处论断，**全部属实**；发现 3 处技术方案漏洞（hitbox 与物理组件冲突 / A\* 网格不可复用 / 长枪移动判定未设计）与 3 处文档内部旧表述。 |
| 2026-10-04 | **实机核验（借 MCP 实读）**：确认 **全场景仅 Player 一个 Rigidbody**，Boxer/Gunner 均无 Rigidbody → **否掉 `GDD §8` hitbox 方案在敌人侧的可执行性**；确认 `Player.prefab` 内既无 Rigidbody 也无 CharacterController（T2 描述属实，场景里的是实例追加组件）；确认全项目仅 `Bullet.prefab` 同时具备 Rigidbody + trigger 碰撞体，而 `Bullet.cs` 靠 `transform.position +=` 位移 —— **说明那个 Rigidbody 的唯一作用就是让 `OnTriggerEnter` 生效**；确认命名层只有 `Default/TransparentFX/Ignore Raycast/Water/UI/Enemy`（`GDD §8` 要求的 6 层缺 5 层）；确认房间内**无任何障碍物**、`SkillCD` 已存在。**仍未改动任何游戏代码**。 |
| 2026-10-04 | **决策翻转 T14：寻路改用 Unity 自带 NavMesh**（放弃自研 A\*）。理由：`AStar.FindPath` 不重置邻居节点 `G/F/Parent`，复用网格重算会因残留 `Parent` 成环导致 `Reconstruct` 死循环卡死编辑器（而"每 0.3 s 重算"必须复用网格）；且 NavMesh 内置网格烘焙/障碍标记/路径平滑/动态避障。`AStar` + `AStarSelfTest` **保留不动**，降级为报告算法章节素材。同步更新 `GDD §6.5 / §13.3 / §13.4 / §15.2`、本文件 `§9 T14`、`§10.1 #12`；**新增 `Docs/NAVMESH-GUIDE.md`**（手把手教学手册）。**仍未改动任何游戏代码**。 |
| 2026-10-04 | **产出 `Docs/ROADMAP.md`（完成路线图）**：编排"阶段 0 开工前置 + M1–M6"的完整分步计划，含每步产出/验收标准/依赖/阻塞项。核实并记录若干前置事实：输入系统**缺 `Special` Action**（`PlayerControls.cs` 为自动生成，需改 `.inputactions` 后重新生成）；`PlayerConfig` **无任何体力消耗/再生字段**；`.git` **已建**（`main` 分支、`origin` = `xxvv-111/Trial.git`、初始提交已推送，§2 已同步）；NavMesh 决策使"房间范围数据缺失"问题**自动消失**（整场烘焙，无需 `RoomController` 提供边界）。**仍未改动任何游戏代码**。 |
| 2026-10-04 | **✅ 执行阶段 0 + 全项目脚本清理与优化（首次改动游戏代码）**，提交 `d4789c8` / `9b8c638` / `e21d805`。① **阶段 0**：新建 5 个层；输入补 `Special`（Q / 右键）并**删除与 `Dash` 绑键冲突的 `Jump`**；`PlayerConfig` 补体力字段与 `hitInvulnTime`；移除 Player 上的 `_DebugCheatsW7` 组件。② **删除 19 个死代码脚本**（`Tools/` 7 个 + `WeaponConfig` + `_Modules/` 11 个），脚本数 **50 → 37**；`AStar` + `AStarSelfTest` 保留作报告算法章节素材。③ **修复 4 个真 bug**：`PlayerDash` 开局白送 0.5s 无敌、`EnemyMeleeAI` 在 `Update` 里每帧 `SetTrigger` 致动画重置、`GameManager` 结算不暂停 + `VICTPRY` 拼写、多处 NRE 风险。④ **可读性**：3 处文件名=类名（`PlayEnergy`/`CameraMove`/`ManaBar`）、命名空间统一（`Game.GamePlay`→`Game.Gameplay`、`AttackStateBehaviour` 补命名空间、`DamagePopup` 裸 `Fx`→`Game.Fx`）、清 5 处多余 using、清各处 w6/w7 注释死代码、补类与方法注释及 null 保护。同步更新 §9（T9–T13/T15/T16 标记已修复，**新增 T22–T24**）与 §10 进度。**验证：编译 0 报错 0 警告；场景 Missing Script = 0。** |
| 2026-10-04 | **⚠️ 方向变更评估：房间制 → ARPG 单场景**（产出 `Docs/ARPG-DIRECTION.md`）。用户拟放弃《哈迪斯》式三房间结构，改为**一条连续通路直达 Boss、沿途布置小怪**。核查结论：① **技术可行**，玩家侧 100% 可复用、NavMesh 包已装（`NavMeshSurface`/`Agent`/`Obstacle` 均可用）；② **代码改动比预想小**——现有 `RoomController` 本质已是"进入区域→刷怪→清空→开通路"，**改语义即可当作 ARPG 遭遇点**，建议采用"保留触发机制"的混合方案（否则玩家会一路跑过所有怪直奔 Boss）；③ ⚠️ **这是范围转移而非缩减**——房间制白送了"节奏"，单场景需手动设计遭遇密度；④ 🔴 **Boss 从"可延后"升级为关键路径**（整条路的目标），而招式表至今未定、Boss 资产为零。**同时勘误 `ART-ASSETS.md §1.1`**：早先称"敌人无渲染组件、隐形"**有误**，实测敌人使用 `SkinnedMeshRenderer`（可见、能播动画），真实问题只是**材质零贴图（纯白）**，且 **Boxer/Gunner 共用同一网格**（= 实际只有一种敌人外观）。 |
| 2026-10-04 | **✅ M1.2 Hitbox 系统完成**（提交 `4593136` / `8596c11` / `59ee4e0` / `e5f874d`）—— **T3 + T21 双结案**。① **归层 + 碰撞矩阵**：22 个根物体归层（Player 8 / Enemy 6 / Environment 12），矩阵按 GDD §8 配好（PlayerHitbox 只碰 Enemy、EnemyHitbox 只碰 Player、两者互不碰）。② **新增 `Hitbox` + `HitboxController`**（`Gameplay/Combat/`）：判定体自带 **kinematic Rigidbody**（⚠️ 必需——敌人身上无 RB，否则 OnTriggerEnter 永不触发）、已命中集合去重、`HitboxController.TryProcessHit` 为**唯一命中出口**、支持定时自动关闭、F1 运行时 Gizmos。③ **玩家侧**：`PlayerAttack` 删除瞬时采样，改为开启 `Hitbox_Attack1~4`（4 段各自尺寸/伤害）；`AttackFxBridge` 订阅源改为 `HitboxController.OnHit`；`PlayerFSM` 离开 Attack 时清残留判定。④ **敌人侧**：`EnemyHealth` 新增**实例事件** `Damaged`；`EnemyMeleeAI` 订阅后实现硬直+闪白+击退，按 `canBeInterrupted` 区分小怪/Boss；闪白改用 `MaterialPropertyBlock`（修掉材质实例化泄漏）；`Boxer` 新增判定体。⑤ **校验工具** `Editor/HitboxAnimationValidator.cs`（菜单 `Tools/Hitbox/校验攻击动画事件`）。**实测：4 段连段各有 `OnAttackHit`、敌人 `TickAttack` 存在；11 项碰撞关系全对； |
| 2026-10-04 | **✅ 方向变更决策落定 + 相机改造完成**（提交 `7c0cc3a`）。① **5 项决策**：封路采用**方案 C（不阻拦）**、敌人**提供新外观**、**Boss 留到最后设计**、一路 **5–10 只小怪**、相机**跟随主角 + 鼠标控制视角**。② **相机重写**（`CameraFollow` 固定偏移 → 球面环绕）：`_yaw`/`_pitch` 环绕 + `_distance` 缩放（滚轮，4–22），俯角限幅 15–85°，焦点抬高 1.2 看向胸口，平滑只做在位置上，用 `unscaledDeltaTime` 保证暂停时不卡死；`_lockCursorOnStart` 默认 false 便于调试。③ ⚠️ **连带必要改动**：`PlayerMotor` 移动方向改为**相对相机**（`fwd*axis.y + right*axis.x`），否则转视角后按 W 会往斜里走。④ **输入**：Player map 新增 `Scroll`（绑 `<Mouse>/scroll`），`InputService` 暴露 `LookDelta`/`ScrollDelta`。**验证：编译 0 报错 0 警告，相机到焦点距离实测 10.00。** |
| 2026-10-04 | **✅ 玩家模型换成 Roskva**（UE 素材包接入，提交 `78922b3`）。① **Rig 转换**：原 `animationType=2(Generic)` → 改 `Human`，生成 Avatar「OVR - RoskvaAvatar」，**isHuman=True、54/54 骨骼槽全映射、0 未映射** → 现有 `PlayerAC` 的 Idle/Run/Dash/Attack1-4/Hit/Death **全部可直接复用**。② **尺寸与朝向实测均无需调整**：高 1.853、脚底 y≈0、面朝 +Z；自带 `B_Weapon_L/R` 武器挂点供 M2 用。③ **替换方式**：保留 Player 物体本身，把 Roskva 作为子物体 `RoskvaModel` 接入 → **场景引用不断**（EnergyBar→PlayerEnergy、HPBar→PlayerHealth、CameraFollow→Player 均在）；移除子物体多余 Animator 与 Y Bot 的 2 个渲染器。④ **验证**：7 个人形骨骼经 `GetBoneTransform` 全部解析到 Roskva 骨骼且都在 `RoskvaModel` 下；Player 12 个组件完好；场景 Missing Script = 0；遮罩贴图通道已符合 URP（未重建材质）。⑤ `.gitignore` 新增 `*.psa`/`*.psk`（Unreal 私有格式，Unity 读不了，约 26MB）。**文档同步 ART-ASSETS.md §2.1 / §8 / §10。** |
| 2026-10-04 | **✅ M1.1 体力系统接入**（提交 `845fece`）—— **首次让体力真正被消耗**（T4 结案）。① **统一出口**：成本表集中在 `PlayerEnergy`（其它组件只依赖它，不必各持 `PlayerConfig`），提供 `TrySpend` / `TrySpendDash` / `TrySpendAttack(comboIndex)` / `TrySpendSpecial(override)`。② **3 个消耗点**：`PlayerFSM.TryDash`、`PlayerFSM.TryAttack`（先付体力再切状态）、`PlayerAttack.TryNextCombo`（体力不足则该段不接续）。③ **再生**：消耗后 0.6s 静置 + 25/s 回复，值未变不刷 UI。④ **不足反馈**：`OnSpendFailed` 事件 + `EnergyBar` 闪烁（`WaitForSecondsRealtime`，避免暂停卡住）。⑤ `ManaFill` 由蓝改绿。⚠️ 提示音留待 M5；⚠️ 数值待实测校准。**验证：编译 0 报错 0 警告，组件无 Missing。** |
| 2026-10-03 | **建立 Git 仓库并接入远端**：在 `Demo-main\` 执行 `git init -b main`，新增 `.gitattributes`（Unity 资产禁用行尾转换，避免假 diff），初始提交 **`da8cc75`**（670 个文件：`Assets` 637 / `ProjectSettings` 25 / `Docs` 4 / `Packages` 2 / `.gitignore` / `.gitattributes`）。`Library`、`Temp`、`Logs`、`UserSettings` 由 `.gitignore` 排除。远端 `origin` = `https://github.com/xxvv-111/Trial.git`，`main` 跟踪 `origin/main`，**初始提交已推送、本地与远端一致**。**仍未改动任何游戏代码** |
