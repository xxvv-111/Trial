# 项目上下文 / 交接文档（PROJECT CONTEXT）

> **用途**：这份文档是为了"**上下文长度不足、开启新会话时快速接管整个项目**"而写的。
> 任何新的 AI 会话或协作者，**读完本文即可理解项目全貌并开始干活**，不需要重新通读全部代码。
> 配套文档见文末 §11 文档索引。
>
> 最后更新：2026-10-05 | 项目阶段：**阶段 0 ✅ · M1 系统骨架全部 ✅（体力/Hitbox/暂停/文案）· M2.1 武器配置 ✅ · 方向已变更为 ARPG 单场景（相机已改造）**
>
> **下一步：M2.2 开局二选一**

---

## 1. 一句话定位

Unity 6 的 **3D 近战动作游戏**（武术/暗黑奇幻题材）。框架自研，包含状态机、事件总线、对象池、A\* 等模块。当前正从"学习项目"改造为**课程设计项目**。

> ⚠️ **2026-10-04 起方向变更为 ARPG 单场景**：放弃《哈迪斯》式"三房间清怪"，
> 改为**一条连续通路直达 Boss、沿途布置小怪**（不封路）。详见 [`ARPG-DIRECTION.md`](ARPG-DIRECTION.md)。
> 玩家模型已换成 **Roskva**（UE 素材包接入，Humanoid）；相机已改为**鼠标控视角的后置跟随**。
> **`GDD.md` 中"三房间"相关描述为变更前的原方案**，结构部分以 `ARPG-DIRECTION.md` 为准。

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
| 分支状态 | ✅ 初始提交 `da8cc75` 已推送，**本地与远端一致**；仓库含 `.gitignore` + `.gitattributes` |

**运行**：Unity 打开工程 → 打开 `MainMenu` 场景 → Play（或先 Play 再走开始按钮）。`Game` 场景也可直接 Play。

**注意**：`Assets/_Game/Notes/` 是空目录（仅 `.gitkeep`）；`Assets/_Game/Audio/` 空；`Assets/_Game/Prefabs/UI/` 空。

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
│   │   └── Game.unity              # 主关卡（3 房间 + 玩家 + Canvas + GameManager）
│   ├── Editor\
│   │   ├── AStarSelfTest.cs        # A* 的编辑器自测（菜单 Tools/A* 自测、自测2：无路）
│   │   ├── FieldInspectorMenu.cs   # 反射打印脚本字段（调试工具）
│   │   └── HitboxAnimationValidator.cs  # ★ M1.2 新增：校验攻击动画是否挂了判定事件
│   ├── Settings\                   # URP 配置资产（PC/Mobile RPAsset、Volume Profile）
│   ├── TextMesh Pro\               # TMP 资源
│   ├── TKDstyle_AnimSet\           # 第三方武术动画资源包（⚠️ 署名要求，见 §9 T17）
│   ├── InputSystem_Actions.inputactions / PlayerControls.cs（自动生成）
│   ├── _Game\                      # ★ 游戏主体
│   │   ├── Art\
│   │   │   ├── characters\Roskva\  # ★ 玩家角色（UE 素材包接入，**Humanoid**）
│   │   │   │   ├── _model\fbx\     # OVR - Roskva.fbx（主模型）+ OVR - Roskva_Animated.fbx
│   │   │   │   │                   #  ⚠️ 后者已改 Human，含 Walk 2.47s / Idle01 等 5 段可重定向动画
│   │   │   │   ├── _textures\      # 贴图（含 _urp 子目录；Hair_Gold 为金发变体）
│   │   │   │   ├── Meshes\         # ★ Equips_NoSword.asset（去掉剑的装备网格）
│   │   │   │   ├── Materials\      # Mat_Roskva_* 材质
│   │   │   │   └── anim\
│   │   │   ├── Animations\Player\  # PlayerAC.controller + Idle/Run/Dash/combo_01_1-4/Hit/Death
│   │   │   ├── Animations\Enemy\   # EnemyAC.controller（只有 idle + combo_01_1 两状态）
│   │   │   └── Materials\
│   │   ├── Audio\                  # ❌ 空（见 §9 T18）
│   │   ├── Notes\                  # ❌ 空
│   │   ├── Prefabs\
│   │   │   ├── Player\Player.prefab      # 含 HitboxController + Hitbox_Attack1~4（M1.2）
│   │   │   ├── Enemy\{Boxer, Gunner, Enemy_Slime, Bullet}.prefab  # Boxer 含 Hitbox_Attack
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
| `PlayerConfig` | **只含角色自身属性**（M2.1 起攻击数值已迁出）：移动 `moveSpeed 6`/`dashSpeed 10`/`dashTimer 0.5`/`dashDelay 0.1`/`iFrameTime 0.5`；生存 `maxHp 100`/`hitInvulnTime 0.8`；体力 `maxEnergy 100`/`dashEnergyCost 20`/`energyRegenDelay 0.6`/`energyRegenRate 25`。⚠️ `comboWindow`/`attackRange`/`attackDamage`/`attackEnergyCost`/`specialEnergyCost` 已迁到 `WeaponConfig`，此处仅作**未装配武器时的兜底** |
| `EnemyAIConfig` | `displayName`、`hp 30`、`aggroRange 8`、`attackRange 1`、`moveSpeed 3`、`windupTime 1`、`recoverTime 1.5`、`damage 10`、`prefab`、`hitEffect`；**受击反应（M1.2 新增）**：`canBeInterrupted true`（⚠️ **Boss 必须设 false**）/`hitStunTime 0.4`/`knockBackDistance 0.4`/`flashTime 0.15` |
| `WeaponConfig` | **M2.1 新建**（原同名文件已在阶段 0 清理死代码时删除）。标识 `displayName`/`icon`；连段 `comboWindow`/`damage[4]`/`energyCost[4]`；判定盒 `hitboxLength[4]`/`hitboxWidth[4]`/`hitboxHeight`/`hitboxCenterY`；特殊攻击 `specialType`(枚举 `SpecialAttackType`)/`specialDamage`/`specialEnergyCost`/`specialCooldown`/`specialRange`；外观 `modelPrefab`/`modelLocalPosition`/`modelLocalEuler`/`gripBoneName`。附取值辅助 `GetDamage`/`GetEnergyCost`/`GetHitboxSize`/`GetHitboxLocalPosition` |

资产路径：
- `Assets/_Game/Scripts/Data/{PlayerConfig,EnemyAIConfig}.asset`
- `Assets/_Game/Scripts/Data/Weapons/{Weapon_Sword,Weapon_Spear}.asset`（**M2.1 新增**）

### 5.3 `Game.Gameplay`（玩家 + 战斗判定）

| 类 | 职责 / 要点 |
|---|---|
| `PlayerFSM` | **玩家状态机（核心）**。`Dictionary<PlayerState, Action>` 三张表（`_enter` / `_update` / `_exit`）；`Change(next)` 做 exit→set→enter；`Death` 状态是**终态**（拒绝一切切换）。实现 `IDamageable.TakeDamage`，内部判 `Invulnerable`（`_invulnTimer` 0.8s 或 `Dash.IsInvulnerable`） |
| `PlayerState` | 枚举：`Idle, Run, Dash, Attack, Hit, Death` |
| `PlayerMotor` | 移动。读 `InputService.Move` → `CharacterController.Move()`；`Face()` 用 `RotateTowards` 转向；驱动动画混合树参数 `speed`。⚠️ **`enabled=false` 会连重力一起停掉**（见 §9 T1） |
| `PlayerDash` | 冲刺。`BeginDash()` 设 `_dashTimer` + 无敌帧；`LateUpdate` 里按 `transform.forward * dashSpeed` 位移；`IsDashing` / `IsInvulnerable` 供 FSM 查询 |
| `PlayerAttack` | 普攻与连段。`StartCombo()` / `TryNextCombo()`（连段）；动画事件 **`OnAttackHit()`** 只负责**开启该段判定体**（M1.2 起，不再是瞬时采样）→ 命中结算由 `HitboxController` 统一收口。数值（伤害/连段窗口/段数/判定盒尺寸）读 `WeaponConfig`，未装配时回退 `PlayerConfig`；`SetWeapon()` 供 M2.2 装配；`SyncHitboxDamage()` 在开判定体前写入该段伤害；`CloseAllHitboxes()` 供 FSM 在离开 Attack 时清残留 |
| `PlayerHealth` | 血量。`MaxHp/CurHp`、`OnHpChanged` 事件、`ApplyDamage()`。⚠️ `Died` 事件与 `Die()` **被注释掉了（w6）**，实际未使用（见 §9 T8） |
| `PlayerEnergy` | 体力（文件名与类名一致 ✅）。`MaxEnergy/CurEnergy`、`OnEnergyChanged`、`OnSpendFailed`；**唯一消耗出口** `TrySpend(float)` 与 `TrySpendDash()` / `TrySpendAttack(i)` / `TrySpendSpecial(override)`。⚠️ **成本表集中在本类**（M1.1 原则）：普攻与特殊攻击成本读 `WeaponConfig`（`SetWeapon()` 切换），冲刺成本属角色属性留在 `PlayerConfig` |
| `AttackStateBehaviour` | `StateMachineBehaviour`，在攻击动画状态 enter/exit 时调 `PlayerAttack.SetAttacking(true/false)`，**替代每帧轮询** |
| `AttackFxBridge` | 把 **`HitboxController.OnHit`** 桥接到 `HitFxSystem.Play`（M1.2 起订阅源由 `PlayerAttack.OnHit` 改为判定体系统，使所有命中来源走同一条特效通道） |

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
| `EnemyMeleeAI` | 近战敌人 AI。**自带一套 switch 式状态机**：`EState { Idle, Chase, Attack, Hit, Death }`。有受击闪白（`_flashT` + `Renderer.material.color`）与硬直（`_stunT`）。伤害由动画事件 **`TickAttack()`** 触发。⚠️ 动画 `SetTrigger("Attack")` 写在 `Update` 里（每帧触发，隐患）；`OnDeath()`/`FlashRed()`/`KnockBack()` 等部分方法当前未被调用 |
| `EnemyRanged` | 远程敌人。⚠️ **完全没有状态机**：仅"距离检测 + 转向 + 冷却计时 → `Fire()`"。`Fire()` 实例化 `bulletPrefab` 并调 `Bullet.Launch(velocity, owner)` |
| `Bullet` | 弹道。`Launch(velocity, owner)`、`lifeTime 3s` 自销毁、`Update` 里按速度位移、`OnTriggerEnter` 命中 `PlayerFSM` 造成伤害（`damage 10`），撞非 trigger 物体销毁；用 `IsChildOf(_owner)` 避免自伤 |

### 5.5 `Game.Gameplay`（房间与流程）

| 类 | 职责 / 要点 |
|---|---|
| `RoomController` | 房间。`_entryDoor`、`_prevRoom`（用于校验上一房已清）、`isFinalRoom`、`_enemies[]`、`_hint`。`Start` 里**禁用所有敌人并关门**；`OnPlayerEntered()` 关门 + 提示 + **激活全部敌人并统计 `_alive`**；订阅 `EnemyHealth.Died` 递减，归零 → `FinishClear()`；若 `isFinalRoom` 则 `GameEvents.RaiseBossDied()`。状态：`Started` / `Cleared` |
| `RoomTrigger` | 触发器。`EMode { OpenEntryDoor, StartFight }`，`OnTriggerEnter` 判 `Player` tag |
| `DoorController` | 门。`Open()` / `Close()` 只是 `_body.SetActive()` 开关 |
| `GameManager` | 流程。单例；`Start` 重置 `Time.timeScale = 1` **并恢复输入**、记录 `_startTime`、隐藏两个面板；订阅 `GameEvents.PlayerDied/BossDied`；`_ended` 防重入（**公开为 `HasEnded` 供 `PauseMenu` 判断**）；死亡 → **立刻锁游戏性输入** + 延时 2s 显示 `gameOverPanel`，通关 → 写 `resultText`（`VICTORY! TIME:...s`）+ 延时 0.5s 显示 `victoryPanel`；面板显示时 `Time.timeScale = 0`；`RestartRun()` / `BackToMenu()`（都会先还原 `timeScale`）。⚠️ **`timeScale = 0` 拦不住输入**，故结束时会同时调 `InputService.SetGameplayInputEnabled(false)`（见 §6.4） |
| `CameraFollow` | 第三人称相机（类名与文件名一致 ✅）。机位算法：焦点 = 角色 + `_focusHeight(1.0)`，机位 = 焦点**沿视线后退** `_distance(3)` → 视线必然穿过角色、**角色永远居中**；基准俯角 `_pitch 40°`，**鼠标可上下偏移（含仰视）**；滚轮缩放 1.5~10、`_smoothTime 0`（硬跟随）；⚠️ **`_followTargetYaw` 必须为 false**（相机 yaw 归鼠标，若跟随角色朝向会与"移动相对相机 + 角色转向移动方向"构成**正反馈 → 按 WASD 视角持续旋转**）；开局 `SnapToTarget()` 瞬移到位；`HandleCursor()` 在游戏性输入关闭时**只解锁指针、不锁回**（否则结算/暂停面板点不动）。详见 `ARPG-DIRECTION.md` §4.6 |

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
| `EnergyBar` | 订阅 `PlayerEnergy.OnEnergyChanged`（体力条，原 mana 条改造）。⚠️ 闪烁用 `WaitForSecondsRealtime`（暂停时不能卡在中间态） |
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
   → PlayerFSM.UpdateNeutral 检测到 → TryAttack()（先付体力）→ Change(Attack)
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
- **`EnemyAC.controller`**（参数：`Attack`）
  状态：**仅 `idle` + `combo_01_1`**（共 2 个）⚠️

---

## 7. 美术资产现状（对改造很关键）

| 资产 | 路径 | 关键事实 |
|---|---|---|
| **`OVR - Roskva.fbx`** | `Art/characters/Roskva/_model/fbx/` | **玩家角色主模型**（UE 素材包接入）。`animationType = Human` ✅ / `isHuman` ✅ / 9 个渲染器（Body / Equips / Eyeshadow / Face / Fur / Hair / Hair2 / Lower Body / Upper Body） |
| **`OVR - Roskva_Animated.fbx`** | 同上 | 角色自带动画。**Rig 已改为 `Human`**（原为 Generic）→ 5 段动画全部 `isHumanMotion`、**骨骼命名与玩家 Avatar 一致 54/54 → 可直接重定向**。⚠️ 但其 Avatar 仍缺 `Neck`（**T26**） |
| `Equips_NoSword.asset` | `Art/characters/Roskva/Meshes/` | **去掉剑的装备网格**（顶点 4516 / 27 骨骼 / 材质 `Mat_Roskva_Parts`）。玩家预制体已用它覆盖 `Equips` 渲染器 → 为 M2 换武器做准备 |
| Roskva 贴图与材质 | `Art/characters/Roskva/{_textures,Materials}/` | 含 `_urp` 子目录；`Mat_Roskva_Hair*` 已接入**金发变体**（`T_Roskva_Hair_Gold`，接在 `_BaseMap` 基础色槽，sRGB 正确） |
| 握持挂点 | Roskva 模型内 | `B_Weapon_L` / `B_Weapon_R`（武器模型挂点，`WeaponConfig.gripBoneName` 默认取 `B_Weapon_R`） |
| TKDstyle_AnimSet | `Assets/TKDstyle_AnimSet/` | **第三方**武术动画包，项目动画的来源之一（⚠️ 署名要求，见 §9 T17） |
| PlayerAC / EnemyAC | `_Game/Art/Animations/` | 两个 Animator Controller |
| 敌人预制体 | `_Game/Prefabs/Enemy/` | `Boxer`（近战，已含 `Hitbox_Attack`）、`Gunner`（远程）、`Enemy_Slime`、`Bullet` |

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
| **T6** | 敌人无共用状态机基类（近战 switch 硬编码 / 远程完全没有） | `EnemyMeleeAI` / `EnemyRanged` | 加两种敌人 = 两份重复代码 |
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
| **T23** | **`EnemyMeleeAI` 每帧 `SetTrigger("Attack")`**（写在 `Update` 里）→ 攻击动画不断被重置 | `EnemyMeleeAI` | ✅ **已修复 2026-10-04**：移到 `SetState`，进入 Attack 时触发一次 |
| **T24** | 多处 **NRE 风险**：`HitFxSystem`（预制体未连线）、`AttackFxBridge`（fx 未赋值）、`DamagePopup`（无 Text / 无 Camera.main）、`HPBar`/`EnergyBar`（`_fill` 为 null）、`RoomHintText`（`_text`） | 多处 | ✅ **已修复 2026-10-04**：均已补 null 保护（对应 T19） |
| T17 | `TKDstyle_AnimSet` 为第三方资源包 | `Assets/` | 提交/演示需注意署名与授权 |
| ~~T25~~ | ~~Avatar 缺少 `Neck` 映射~~ | ✅ **已修复 2026-10-04**：`human[]` 项数 **54 → 55（完整）**，`Neck -> Bip001-Neck`；端到端抽检 11 个关键骨骼全部解析成功。成因备查：`human[]` 原为空数组（Unity 全自动映射，含 Neck），改为**显式列表**时漏掉 Neck —— **显式列表不会自动补齐未列出的槽位** | 已消除（⚠️ 见 T26） |
| **T26** | **`OVR - Roskva_Animated` 的 Avatar 仍缺 `Neck`**（`human[]` 54 项），而玩家 Avatar 已是 55 项 | `OVR - Roskva_Animated.fbx` | ⚠️ 两者不一致 → 用 `Walk` / `Idle01` 时**颈部动作不会重定向**（对走路循环影响很小，但迟早会踩）。修法同 T25：Rig → Configure 把 `Neck` 映射到 `Bip001-Neck`。**建议在 M3 接入巡逻动画前补掉** |
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

**M2 · 武器系统（进行中）**
- ✅ **M2.1 WeaponConfig 重做**（`41472f2`）：`WeaponConfig` + 剑/长枪两份资产；伤害/连段窗口/段数/**判定盒尺寸**/体力成本全部接入

**其他并行进展**
- ✅ 相机改造完成：鼠标控视角 + 后置跟随（基准俯角 40°，**可仰视**）；**游戏结束后不能转视角**（输入闸门）
- ✅ 玩家模型换为 **Roskva**（Humanoid，**T25 Neck 已修**）；`Equips_NoSword` 无武器网格已接入
- ✅ `OVR - Roskva_Animated` 改 **Humanoid** → `Walk` / `Idle01` 等 5 段动画**可重定向**（M3 巡逻前提）

### 未完成（V1 缺口，见 `GDD.md` §3.1）

**M2 剩余**
- ❌ M2.2 开局二选一（选择 UI + `WeaponManager` 装配）
- ❌ M2.3 剑·插地（特殊攻击）
- ❌ M2.4 长枪·投掷与召回 ⭐ 最复杂
- ❌ 武器**图标**与**模型**（当前用无武器网格跑动作）

**M3 / M3.5**
- ❌ 敌人视野判定、寻路（**NavMesh 包已装但未烘焙**）、障碍物
- ❌ 剑兵 / 弓兵完整状态机 + 敌人动画状态机
- ❌ ARPG 单场景关卡（通路白盒 + 撒 5–10 只小怪）

**M4–M6**
- ❌ 真正的 Boss + Boss 血条（决策为**留到最后设计**）
- ❌ 音频（`Audio/` 为空）、存档
- ❌ HUD 完善、Windows 构建
- ❌ 报告 / PPT / 演示视频脚本

### 遗留问题（未修，见 §9）

**共 26 项已记录，其中 13 项已消除**（T3 / T4 / T5 / T9–T13 / T15 / T16 / T21 / T22–T25）。
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
| **T26** | `Roskva_Animated` 的 Avatar **仍缺 `Neck`** | 用其动画时颈部不重定向（**建议 M3 前补**） |
| **T18** | 音频 / 存档 / Boss 实体 | V1 缺口，分属 M5 / M4 |

### 10.1 已确认的设计决策（速查 · 2026-10-03）

> 完整描述见 `GDD.md`，此处仅供快速恢复上下文。

| # | 决策 | 关键影响 |
|---|---|---|
| 1 | 参考《哈迪斯》，**只取"单层试炼"骨架** | 范围不膨胀 |
| 2 | **不做跳跃** | 省掉 Jump/Fall/Land 动画；**T1 物理重构降级为低优先级**；体力表去掉跳跃项 |
| 3 | **体力不足 → 硬性禁止**该动作（消耗 > 当前体力就不执行） | 所有动作必须走**统一体力检查出口** |
| 4 | **武器 2 把：剑 + 长枪**，都是**四段**普通攻击 | 现有 `Attack1–4` 动画**可复用** |
| 5 | **剑·特殊攻击**：点按 → 插地 → **圆形范围伤害** | 点按分支 |
| 6 | **长枪·特殊攻击**：**点按立即投出**（固定距离、直线）→ 落地插地 → **空手不能普攻**（攻击键改为召回）→ **点按攻击键或特殊攻击键召回**（返程路径伤害） | 新增 `SpearThrow` / `Unarmed` / `SpearRecall` 子状态；**空手时按状态重映射按键语义**（攻击键→召回） |
| 7 | 敌人需要**视野判定 + 寻路**；房间内**布置障碍物** | 新增视野组件；**T14 升级为必做**（A* / NavMesh 方案待定） |
| 8 | **Boss 是人形** | 可复用 Humanoid 重定向动画 |
| 9 | **Boss 招式表暂缓** | 先搭决策框架 + 状态机骨架 |
| 10 | 音频**两种方式都用**（程序化合成 + 现成素材） | `AudioManager` 用"ID → Clip"查表，便于替换 |
| 11 | **存档用 JSON**（`persistentDataPath` + `JsonUtility`，带 `version`） | 需处理"文件缺失 / 损坏"；会产生**可见的存档文件** |
| 12 | **寻路改用 Unity 自带 NavMesh** ⚠️（2026-10-04 翻转；原为"接入自研 A\*"） | **T14 结案**；不再需要自建网格与路径平滑；`AStar` 降级为报告素材。新增工作：房间内摆障碍物 + 烘焙 + 门的 `NavMeshObstacle`(carving)。操作手册见 `NAVMESH-GUIDE.md` |
| 13 | **武器开局二选一**（剑 / 长枪），**单局内不切换** | 省掉切换动作与切换 UI |
| 14 | **长枪细节**：**固定投掷距离**、**直线**飞行、到达终点**插在地上**、召回**与其他攻击同一套 hitbox 判定**、玩家死亡则**长枪留在原地**并直接进结算。**蓄力 / 瞄准 / 指示器 / 瞄准减速已整体取消** | 不再需要"长按三态"输入与指示器 UI；长枪需世界物体表现（飞行 / 插地 / 飞回） |
| 15 | **受击打断分两档：普通小怪会被打断**（硬直 0.4 s + 闪白，攻击中断）；**Boss 不会**（仅闪白，无硬直条/破防） | ⚠️ 现有代码里小怪的受击反应**链路是断的**（`EnemyHealth` 从不通知 AI），需要**接通**而不是删除（见 **T21**）；Boss 前摇表现必须做足预警 |
| 16 | 待定：投掷距离与投掷/召回速度的具体数值、命中回体力、敌人失去目标后的搜索行为、敌人互相分离、Boss 招式表、关卡是否美化 | 见 `GDD.md` §15.2 |

---

## 11. 文档索引（阅读顺序）

> ⚠️ 与 `Docs/README.md` 保持一致（`Docs/` 下**共 9 份** Markdown：`README` + 本文 + 下列 7 份）。

| 顺序 | 文档 | 用途 |
|---|---|---|
| 0 | `Docs/README.md` | 索引与阅读指引、**一句话现状** |
| 1 | **`Docs/PROJECT-CONTEXT.md`**（本文） | **新会话快速接管：项目全貌 + 代码地图 + 坑** |
| 2 | `Docs/GDD.md` | 游戏策划案（**要做什么**）。⚠️ 关卡结构部分已被方向变更覆盖 |
| 3 | **`Docs/ARPG-DIRECTION.md`** | **⚠️ 关卡结构以此为准**：房间制 → ARPG 单场景的 5 项决策 + 相机实现 + 架构约束 |
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
