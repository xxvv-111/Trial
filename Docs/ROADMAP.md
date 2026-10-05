# 项目完成路线图（ROADMAP）

> **用途**：从"当前状态"走到"可交付 exe + 报告"的完整分步计划。
> 每一步都写明 **做什么 / 产出 / 验收标准 / 依赖什么**，按**依赖顺序**排列，不是按愿望排列。
>
> 最后更新：2026-10-05 | 状态：**阶段 0 ✅ · M1 全部完成 ✅ · M2.1 ✅ · M2.2 ✅ · M2.3 ✅ · M2.4 ✅（M2 收口）· 关卡结构改为「多关卡独立场景」· 武器范围收敛为「只做剑」**
> **下一步：M3 敌人 + 寻路**（⚠️ `Walk` 动画已就绪；不再是"开局二选一"；长枪与武器切换已移入 §13 额外项目）
> 相关文档：`GDD.md`（要做什么）、`PROJECT-CONTEXT.md`（现状与坑）、`NAVMESH-GUIDE.md`（寻路手册）、`ARPG-DIRECTION.md`（**方案选型存档：ARPG 单场景未采纳；§4.6 相机仍然生效**）、`ART-PIPELINE.md`（资源方案）

---

## 0. 总览：进度与关键路径

```
阶段 0  开工前置 ✅ 已完成（2026-10-04）── 建层 / 补输入 / 补配置 / 清死代码（脚本 50→37）
   │
   ├─► M1  系统骨架 ✅ 全部完成                  ◄── 后面全靠它
   │     ├─ M1.1 体力统一出口 ✅ 845fece
   │     ├─ M1.2 Hitbox 系统     ✅ 4593136 / 8596c11 / 59ee4e0 / e5f874d（T3+T21 结案）
   │     ├─ M1.3 暂停菜单        ✅ b7add1c（ESC 开关 + 冻结时间 + 锁输入）
   │     └─ M1.4 修文案         ✅（VICTPRY 阶段 0 修；VECTORY 在 M1.3 修）
   │
   ├─► M2  武器（剑）（1–2 天）⚠️ 范围收敛为「只做剑」
   │     ├─ M2.1 WeaponConfig 重做 ✅ 41472f2（配置化 + 全量接入）
   │     ├─ M2.2 装配剑            ✅（Weapon_Sword 已挂 Player1/Player2 的 PlayerAttack + PlayerEnergy）
   │     ├─ M2.3 剑的模型 + 4 段连段动画接入（✅ 用户已接：`1_atk_sword03`/`2_atk_sword04`/`3_atk_sword05`/`4_atk_sword01`）
   │     └─ M2.4 特殊攻击 —— **火球 · 远距离 · 命中爆炸** ✅（代码 + 预制体 + Animator 全通；Play 实测扣 30 体力、爆炸扣 30 血）
   │
   ├─► M3  敌人+寻路（3–4 天）── NavMesh / 视野 / 剑兵 / 弓兵 / **巡逻（Walk 动画已就绪）**
   │
   ├─► M3.5 关卡 ── **3 个独立关卡场景**（Level_01/02/03）+ LevelFlow + 结算与选关
   │
   ├─► M4  Boss（3–5 天）── ⚠️ 决策为「**留到最后设计**」
   │
   └─► M5  收口（2–3 天）── HUD / 音频 / 存档 / 构建
         └─► M6  文档交付（2 天）── 报告 / PPT / 演示

并行线（不阻塞主路径，但需你参与）：
   ▶ 美术资源获取（敌人外观 / Boss 模型 / 音频）—— 见 §8 与 `ART-ASSETS.md`
   ▶ 相机 ✅ 已完成（鼠标控视角 + 后置跟随 + 可仰视）
   ⏸️ 额外项目（加分项，非交付必需）—— 见 §13：长枪·投掷召回 / 武器切换与开局二选一
```

**合计约 12–17 人日**（比原估 14–20 有净减少：长枪移出主线 −1～2 天；新增多关卡场景拆分 +1～2 天）。

**⚠️ 关卡结构（2026-10-05 定稿）**：由"三房间串联（单场景）"改为 **多关卡独立场景** ——
`Level_01` / `Level_02` / `Level_03` 各自一个 `.unity` 场景，逐关清怪推进。
（中间曾评估"ARPG 单场景：一条通路直达 Boss"，**未采纳**，评估存档见 [`ARPG-DIRECTION.md`](ARPG-DIRECTION.md)。）
**武器范围同步收敛为「只做剑」**；长枪与"开局二选一"移入 **§13 额外项目**。

**关键路径上的三个高风险点**：
1. **hitbox（M1.2）** —— ✅ **已完成**，风险解除
2. **Boss（M4）** —— 最重且有未定项（招式表 + 模型均无）；虽已降级为"收尾"，仍需钉死时间预算
3. **动画资源** —— 已大幅缓解（Roskva 是 Humanoid，**55/55 骨骼槽全映射**，可复用全部现有动画）；✅ **`Walk` 2.47s 已就绪**（来自 `OVR - Roskva_SwordInHand.fbx`，M3 巡逻不再缺料）；⚠️ 该 FBX 的 Avatar 缺 `Neck`（T26）；⚠️ **剑的 4 段连段动画 FBX 尚待生成**

---

## 1. 三条技术前提（✅ 已解决两条，剩一条要落地）

评审阶段发现的三个"改一处、后面全跟着改"的前提，现状：

| # | 前提 | 现状 |
|---|---|---|
| 1 | **hitbox 必须自带 kinematic Rigidbody** | ⚠️ **待落地**（写进设计 + 建层时一并处理） |
| 2 | A\* 不能复用网格（会死循环） | ✅ **已解除** —— 改用 NavMesh，不再接入自研 A\* |
| 3 | 关卡范围数据从哪来 | ✅ **已解除** —— NavMesh 整场烘焙，**不需要** `RoomController` 提供房间边界 |

**前提 1 的实测依据**（很重要，别凭记忆）：

- 全场景 **只有 Player 有 Rigidbody**；`Boxer` / `Gunner` / `Enemy_Slime` 三个预制体**都是 0 个 Rigidbody**
- 全项目只有 `Bullet.prefab` 同时有 Rigidbody + trigger 碰撞体，而 `Bullet.cs` 是靠 `transform.position +=` 位移的 → **那个 Rigidbody 的唯一作用就是让 `OnTriggerEnter` 生效**
- Unity 规则：两个碰撞体之间要触发 `OnTriggerEnter`，**至少一方必须有 Rigidbody**
- 而 `GDD.md` §8 计划用 trigger hitbox → **敌人侧的 hitbox 永远不会触发**

✅ **决策**：**每个 hitbox 自带一个 kinematic Rigidbody**。
好处：与宿主的物理配置彻底解耦 → `T2`（收敛物理组件）可以放心做，长枪的"移动判定"也顺带解决。

---

## 2. 阶段 0：开工前置 ✅ **已完成（2026-10-04）**

> 已全部执行完毕，提交 `d4789c8`（脚本清理与优化）+ `e21d805`（建层）。
> 另附带修复了 4 个真 bug（详见 `PROJECT-CONTEXT.md` T22–T24）。**M1 可以开始了。**

> 这些都是"一次做完、后面少踩坑"的事，成本极低但收益很大。

### 0.1 ✅ 版本控制（已完成，无需操作）

已核实：`.git` 存在，分支 `main`，`origin` = `github.com/xxvv-111/Trial.git`，初始提交 `da8cc75` 已推送。

✅ **好处**：M1 之后每次动核心手感都有回退点。**建议每完成一个小节就提交一次**，尤其是 M1.2（hitbox）前后——它最容易让手感变差，需要能对比。

### 0.2 🔧 建 Layer + 配碰撞矩阵

**现状（实测）**：命名层只有 `Default / TransparentFX / Ignore Raycast / Water / UI / Enemy`。
`GDD.md` §8 要求 6 个层，**缺 5 个**：

| 要新建的层 | 用途 |
|---|---|
| `Player` | 玩家本体 |
| `PlayerHitbox` | 玩家的攻击判定 |
| `EnemyHitbox` | 敌人的攻击判定 |
| `Projectile` | 箭矢 / 弹幕 |
| `Environment` | 墙、地面、障碍物（**M3 摆障碍物要用**） |

- 做法：`Edit → Project Settings → Tags and Layers`，或用 Unity MCP 的 `manage_editor` 直接建
- 碰撞矩阵：`Physics → Layer Collision Matrix`，**取消** hitbox 之间、hitbox 与本体的碰撞，只保留 `PlayerHitbox ↔ Enemy`、`EnemyHitbox ↔ Player`

- **产出**：5 个新层 + 碰撞矩阵配置
- **验收**：`Project Settings → Layers` 能看到 6 个层；碰撞矩阵勾选符合上表
- **依赖**：无

### 0.3 🔧 补输入系统的 `Special` Action

**现状（实测）**：`Assets/InputSystem_Actions.inputactions` 的 Player map 里只有 `Move / Look / Attack / Dash / Jump / Sprint / Crouch / Interact`。
⚠️ **没有 `Special`** —— 而特殊攻击是 M2 的核心。

- 做法：在 `.inputactions` 的 **Player** Action Map 里新增一个 `Special`（Button），绑定到键盘（建议 `Q` 或右键）
- ⚠️ **关键**：`Assets/PlayerControls.cs` 是**自动生成**的（`InputActionCodeGenerator`）。改完 `.inputactions` 后需**重新生成**（Unity 会自动，或右键资产 → `Generate C# Class`），**不要手改生成文件**
- 然后在 `InputService.cs` 补一行（照抄现有的风格）：

```csharp
public bool SpecialPressedThisFrame => _controls.Player.Special.triggered;
```

- **产出**：`Special` action + `InputService` 暴露的查询属性
- **验收**：控制台无报错；在任意脚本里读 `InputService.Instance.SpecialPressedThisFrame` 能取到按键
- **依赖**：无

### 0.4 🔧 补 `PlayerConfig` 的体力字段

**现状（实测）**：`PlayerConfig.cs` 有 `moveSpeed / dashSpeed / dashTimer / dashDelay / iFrameTime / maxHp / maxEnergy / comboWindow / attackRange / attackDamage`，但：

❌ **没有任何体力消耗/再生字段** —— 而 `GDD.md` §4.8 要求：

| 要补的字段 | 建议值（⚠️ 标"待实测"） |
|---|---|
| 冲刺消耗 | 20 |
| 普通攻击 4 段消耗 | 8 / 10 / 12 / 15（⚠️ 见 §7 阻塞项） |
| 特殊攻击消耗 | 30 |
| 再生延迟 | 0.6 s |
| 再生速率 | 25 / s |

另：⚠️ 受击无敌 `0.8 s` 目前**硬编码在 `PlayerFSM.cs` 第 59 行**，建议一并移到配置（`iFrameTime` 已有字段但没被用于此处）。

- **产出**：`PlayerConfig` 字段扩充（+ `PlayerConfig.asset` 同步数值）
- **验收**：Inspector 里能看到全部体力字段
- **依赖**：无

### 0.5 🔧 清死代码

**现状（实测）**：

| 位置 | 内容 |
|---|---|
| `Assets/_Game/Scripts/Tools/` | 6 个旧方案死代码：`PlayerInput` / `EnemyMelee` / `EnemyConfig`(+asset) / `EnemyDiedEvent` / `EnemyDeathLogger` / `Target` |
| `Assets/_Game/Scripts/Tools/_DebugCheats_w7.cs` | ⚠️ **调试作弊键，实测确实挂在 Player 上**（`J` 扣血 / `E` 扣蓝 / `K` 秒杀）—— 交付物不能带这个 |
| `Assets/_Modules/` | 11 个教学脚本（296 行），与游戏无关 |
| `Assets/_Game/Scripts/Data/WeaponConfig.cs` | 命名空间误写 **`Game.Date`**（应为 `Game.Data`），且零引用 → M2.1 重做 |
| `Assets/_Game/Scripts/AI/AStar.cs` 第 2 行 | 多余的 `using Unity.VisualScripting;` |

⚠️ **但 `AStar.cs` + `Editor/AStarSelfTest.cs` 要保留**（报告算法章节素材，见 `NAVMESH-GUIDE.md` §1.3）。

- **产出**：删除上述死代码（保留 `AStar` / `AStarSelfTest` / `FieldInspector`）
- **验收**：控制台 0 报错；`_DebugCheatsW7` 已从 Player 上移除且文件删除
- **依赖**：无。⚠️ **重要**：删前先提交一次 git，便于回退

---

## 3. M1：系统骨架（2–3 天）—— **最关键阶段**

> M1 做完就能在编辑器里试手感。**后面 M2/M3/M4 全部依赖 M1 的 hitbox 与体力出口。**

### M1.1 统一体力消耗出口 ⭐ ✅ **已完成（2026-10-04，提交 `845fece`）**

> **实测终态**：成本表集中在 `PlayerEnergy`（其它组件不必各持 `PlayerConfig`），
> 3 个消耗点接入（冲刺 / 普攻首段 / 连段后续），体力不足则**硬性拒绝动作**并触发 HUD 闪烁；
> 再生按"消耗后 0.6s 静置 + 25/s 回复"实现；体力条已由蓝改绿。
> ⚠️ 提示音留待 M5 音频系统；⚠️ **数值待实测校准**（见 §10 阻塞项 3）。

**原始问题**：`PlayerEnergy`（文件 `PlayEnergy.cs`）实测**游戏内从未被消耗**，唯一调用点是作弊脚本。体力条是纯摆设。

**做什么**：
1. `PlayerEnergy` 增加再生逻辑（停止消耗 0.6 s 后按 25/s 回复）
2. 建立**唯一检查出口**：所有动作走 `TrySpend(cost)`
3. 接入四个消耗点：冲刺（`PlayerDash`）、普攻各段（`PlayerAttack`）、特殊攻击（M2）
4. 体力不足 → **硬性禁止该动作** + 反馈（HUD 体力条闪烁 + 提示音）
5. 连段细则：体力不足以支撑下一段 → 该段不可接续（前一段正常播完）
6. `ManaBar`（蓝）改为体力条（换色）

- **产出**：可用的体力系统 ✅ 已交付
- **验收**：连续攻击到体力见底时动作**确实被拒绝**且 UI 有反馈；作弊键删掉后体力仍会被消耗
- **依赖**：0.4（配置字段）✅
- ⚠️ **这是 T4 的结案点** ✅ 已结案

### M1.2 Hitbox 系统改造 ⭐⭐ ✅ **已完成（2026-10-04，提交 `4593136` / `8596c11` / `59ee4e0` / `e5f874d`）**

> **交付终态**：判定体 + 控制器 + 玩家/敌人双侧接入 + T21 接通 + 校验工具。
> **验收实测**：动画事件校验显示 `combo_01_1~4` 各有 `OnAttackHit`、敌人 `combo_01_1` 有 `TickAttack` ✓；
> 碰撞矩阵 11 项关系全部符合预期 ✓；编译 0 报错 0 警告。
> ⚠️ **运行时手感（能否打中、判定范围/硬直时长是否合适）仍需进 Play 实测调参。**

**原始问题**：`PlayerAttack.DoMeleeHit()` 是**动画事件里的瞬时 `Physics.OverlapSphere` 采样** —— 漏帧即丢判定，不满足需求。

**做什么**：
1. 新建 `Hitbox`（挂角色子物体）：
   - `Collider`（`isTrigger = true`）
   - ⚠️ **自带一个 kinematic `Rigidbody`**（见 §1 前提 1，**必须**）
   - 字段：伤害、阵营、所有者、**"已命中目标集合"**（防同一次挥砍重复扣血）
2. 新建 `HitboxController`（每角色一个）：注册 / 按名开关 / `OnTriggerEnter` → 过滤阵营 → `IDamageable.TakeDamage()` → 打击特效
3. 开关方式：**`StateMachineBehaviour` 为主**（美术改动画不会漏挂事件）+ 动画事件为辅（一招多段）
4. 提供 `OnDrawGizmos` 可视化
5. 把 `PlayerAttack` 与 `EnemyMeleeAI` 的伤害触发改为"开启 hitbox"
6. ⚠️ 顺带接通 **T21**：`EnemyHealth.TakeDamage` 只扣血、**从不通知 AI** → 小怪挨打毫无反应。需让受击通知 AI 进入 `Hit` 硬直 + 闪白（**是"接通"，不是"删除"**）

- **产出**：帧驱动的攻击判定系统
- **验收**：
  - 编辑器 Gizmos 能看到判定体在动画特定帧出现/消失
  - 玩家打小怪 → **小怪有闪白 + 硬直**（T21 接通）
  - 敌人打玩家 → 伤害正常
  - 长枪去程/返程能各自命中一次（为 M2.4 铺垫）
- **依赖**：0.2（层）—— ⚠️ **但 0.2 只"建了层"，下面两件事必须在本步补完**
- ⚠️ **风险**：改造触碰核心手感。**改前先 git 提交**，改完对比手感

#### ⚠️ 开工前置：实证状态（2026-10-04 核对）

| 项 | 实测 |
|---|---|
| **6 个层** | ✅ **已建**：`Player(8)` / `Enemy(6)` / `PlayerHitbox(9)` / `EnemyHitbox(10)` / `Projectile(11)` / `Environment(12)` |
| **物体归层** | ❌ **一个都没归** —— 550 个物体全在 `Default`，20 个在 `UI`。Player / Enemy / Environment 层是**空的** |
| **碰撞矩阵** | ❌ **全开**（任意两层都"碰撞"）—— 没按 §8 矩阵要点配置 |
| **敌人物理组件** | ⚠️ **只有 `CapsuleCollider`，没有 `Rigidbody`、没有 `CharacterController`** |
| **玩家物理组件** | ⚠️ `Rigidbody(kinematic)` + `CharacterController` + `CapsuleCollider` **三者并存**（T2） |

**两条必须先解决的前提**：

1. **判定体必须自带 `kinematic Rigidbody`** ⚠️
   原因：`OnTriggerEnter` 需要**至少一方有 Rigidbody**，而**敌人身上没有**。
   判定体自带 kinematic RB 是最小改动方案（kinematic 不参与物理模拟，不影响角色移动）。
   *（备选是给每个敌人加 RB，但改动大且可能影响敌人行为。）*
2. **物体必须归层，否则碰撞矩阵形同虚设**
   配矩阵前先把 Player / 敌人 / 墙地面分别移到对应层，否则矩阵配了也没效果。

### M1.3 暂停菜单 ✅ **已完成（2026-10-04，提交 `b7add1c`）**

**原问题**（已过时）：曾记录"`GameManager` 从不设置 `Time.timeScale = 0`" ——
**该问题已在阶段 0 修复**。

**交付终态**：`PauseMenu` 脚本 + 场景 UI（`PausePanel`，复制结算面板搭建、风格一致）。

| 项 | 实现 |
|---|---|
| 开关 | `ESC`（`InputService.PausePressedThisFrame`） |
| 冻结 | `Time.timeScale = 0`（恢复时还原暂停前的值） |
| **锁输入** | `InputService.SetGameplayInputEnabled(false)` —— ⚠️ **必需**，`timeScale` 拦不住鼠标 |
| `ESC` 不受闸门影响 | ⚠️ 若放进 Player map，暂停后按 ESC **关不掉菜单** |
| `ESC` 职责转移 | 从 `CameraFollow` **移除** ESC 监听（原用于解锁指针），避免与暂停菜单打架 |
| 指针 | 暂停时相机自动解锁指针；恢复时上升沿检测自动重锁 |
| 按钮 | 继续（`PauseMenu.Resume`）/ 重新开始 / 返回主菜单 |
| 结算后保护 | `GameManager.HasEnded` → 结算后按 ESC 不再打开暂停菜单 |

⚠️ **未做"音量"**：项目目前**完全没有音频**（T18），加了也无处可调，待 M5 加音频时一并补。

**验收实测**：`Pause()` → 面板可见 / `timeScale=0` / 输入关 / `LookDelta=0`（鼠标不再转视角）✓
`Resume()` → 全部还原 ✓ 幂等 ✓ **⚠️ 需进 Play 实测 ESC 与三个按钮。**

### M1.4 修文案错别字 ✅ **已完成**

`GameManager` 的结算文案实测已是 `VICTORY! TIME:...s`（原 `VICTPRY` 已在阶段 0 修正）。
另：`VictoryOverPanel/VictoryMsgText` 原为 **`VECTORY !`**（**第二处不同的拼写错误**）已在 M1.3 一并修正。

---

## 4. M2：武器（剑）（1–2 天）⚠️ 范围收敛为「只做剑」

### M2.1 `WeaponConfig` 重做 ✅ **已完成（2026-10-04，提交 `41472f2`）**

**⚠️ 原描述已过时**：文档记的"命名空间误写 `Game.Date`、零引用"的 `WeaponConfig.cs`
**已在阶段 0 清理死代码时被删除** → M2.1 是**从零新建**，不是改造。

**交付终态**：
- `Data/WeaponConfig.cs`：`SpecialAttackType` 枚举 + `WeaponConfig`（字段按 §5.2 全到位，
  含取值辅助与 `OnValidate` 校验）
- `Data/Weapons/Weapon_Sword.asset` / `Weapon_Spear.asset`：两把武器的完整数值
- **接入**（关键，否则又是死代码）：
  `PlayerAttack` 读武器的伤害/连段窗口/段数/**判定盒尺寸**；
  `PlayerEnergy` 读武器的体力成本（**成本表仍集中在 PlayerEnergy**，保持统一出口）；
  `Hitbox` 新增 `SetDamage` / `ApplyShape`；`HitboxController` 新增 `GetByName` / `ApplyWeaponShapes`
- `PlayerConfig` 的 `attackRange` / `attackDamage` / `attackEnergyCost` / `specialEnergyCost`
  降级为"未装配武器时的兜底"

**两把武器的差异化**（按 §5.3 手感定位，⚠️ 数值为初值待实测；⚠️ 长枪属**额外项目**）：

| | 伤害 | 判定盒（长 / 宽） | 特殊攻击 |
|---|---|---|---|
| **剑** | 12/15/10/20（均衡） | 2.5~3.1 / 1.5~1.86（**中等且宽**） | **火球**：伤害 30，**远距离**（🔄 原 SwordSlam 半径 3.5 已作废） |
| **长枪** | 10/12/9/16（**单段略低**） | 3.2~3.9 / 1.0~1.2（**更长更窄**） | SpearThrow：伤害 20，飞行 6 |

**验收实测**（在临时实例上验证，未触碰预制体与场景）：
未装配保持原值 ✓ / 装配剑与原值一致 ✓ / **装配长枪判定盒变为 3.2~3.9 且变窄 → 配置驱动生效** ✓ /
模拟 4 段命中帧伤害依次同步为 10/12/9/16（**各段独立正确**）✓ / 连段窗口 0.9、段数 4 ✓

⚠️ **Player 仍未装配武器**（保持现状行为）—— 装配是 M2.2 的职责。
⚠️ 未做：武器图标（M2.2 的选择 UI）、武器模型（当前用无武器网格试动作）。

- **依赖**：M1.1（体力字段设计）✅、M1.2（判定盒）✅ —— 均已满足

### M2.2 装配剑 ✅ **已完成（2026-10-05）**

**决策**：V1 只做剑 → 进入关卡**默认装配剑**，**不做选择 UI**。
（原"开局二选一"的完整设计保留在 **§13 额外项目**。）

**实际做法**（⚠️ 与本节原计划有一处偏离，见下）：
把 `Weapon_Sword.asset` 挂到 **`Player1.prefab` / `Player2.prefab` 的 `PlayerAttack._weapon`
与 `PlayerEnergy._weapon`**（各 2 处），而不是挂在 Level 场景的实例上。
理由：V1 的语义是"**进关卡默认装配剑**"——这是角色级属性，应当跟着预制体走；
挂预制体还能自动覆盖场景实例（实测**场景实例 0 条 override 即继承**），
且不依赖场景、不弄脏场景文件。

- **产出**：玩家真正握着剑（**数据侧**）✅
- **依赖**：M2.1 ✅

#### ⚠️ 重要：原「验收标准」在现状下无法区分装与不装

实测发现 **`Weapon_Sword` 与 `PlayerConfig` 兜底值几乎逐项相同**：

| 数据 | `Weapon_Sword` | `PlayerConfig` 兜底 | 差异 |
|---|---|---|---|
| 4 段伤害 | `[12,15,10,20]` | `attackDamage` `[12,15,10,20]` | ❌ 完全相同 |
| 4 段体力 | `[8,10,12,15]` | `attackEnergyCost` `[8,10,12,15]` | ❌ 完全相同 |
| 特殊攻击体力 | 30 | 30 | ❌ 完全相同 |
| 连段窗口 | 1 | 1 | ❌ 完全相同 |
| 判定盒尺寸 | 2.5~3.1 / 1.5~1.86 | **无此字段**（尺寸烘在预制体上） | ⚠️ 唯一差异源，但预制体里的既有值**恰好已等于剑的数值** |
| `specialType/specialDamage/specialRange` | 1 / 30 / 3.5 | **没有** | ⭐ **只存在于武器** |

→ 所以「装完后判定盒变 2.5~3.1、伤害变 12/15/10/20」这套判据**看不出任何变化**。
（`damage` 与 `energyCost` 在 M2.1 从 `PlayerConfig` 复制到武器时取了同值。）

#### ✅ 改用「换武器探针」做真正的功能验收

把 `Weapon_Spear`（判定盒 `3.2~3.9 / 1.0~1.2`、连段窗口 `0.9`）当探针，
在 **Play 模式** 调 `PlayerAttack.SetWeapon()`，看判定盒是否真的被改写：

| 步骤 | Weapon | combopWindow | Hitbox_Attack1 | … | Attack4 |
|---|---|---|---|---|---|
| ① 进 Play（`Awake` 里 `ResolveWeaponData()`） | **Weapon_Sword** | 1 | (1.50, 1.60, **2.50**) | … | (1.86, 1.60, **3.10**) |
| ② `SetWeapon(长枪)` | Weapon_Spear | **0.9** | (1.00, 1.60, **3.20**) | … | (1.20, 1.60, **3.90**) |
| ③ `SetWeapon(剑)` 换回 | Weapon_Sword | 1 | (1.50, 1.60, **2.50**) | … | (1.86, 1.60, **3.10**) |

**②尺寸与窗口都随之变化 → 配置驱动确认生效**；①证明预制体上的 `_weapon` 被 `Awake` 正确解析。
（`SetWeapon` 只在运行时改，退出 Play 即还原，场景未被写回 —— 实测场景里武器 GUID 出现 0 次。）

#### ⚠️ 遗留陷阱：`_weapon` 有**两份**，且无人同步

`PlayerAttack` 与 `PlayerEnergy` **各有一个独立的 `_weapon` 字段**，
`SetWeapon()` 也只改自己那一个 —— 实测 ②步只调了 `PlayerAttack.SetWeapon()`，
`PlayerEnergy._weapon` **仍停留在 Weapon_Sword**。
→ **将来做武器切换（§13 额外项目）时必须同时调两者**，否则会出现
"伤害按新武器算、体力按旧武器扣"。目前靠预制体上两处都填好才一致。
建议后续把 `SetWeapon` 收敛为一个统一入口（如挂在角色上的 `WeaponHolder`）。

#### ⚠️ M2.2 的实际价值（已由 M2.4 验证）
`specialType` / `specialDamage=30` / `specialEnergyCost=30` / `specialRange`
**只存在于武器配置里** —— 不装配武器，特殊攻击就没有数据来源。
所以 M2.2 是 M2.4 的前置。
⚠️ 下列是**装配当时的旧值，M2.4 已改**：`specialType=1（SwordSlam）` → **`Fireball`**、
`specialRange=3.5` → **`15`**（`specialDamage` / `specialEnergyCost` 未动，仍 30 / 30）。

### M2.3 剑的模型 + 4 段连段动画接入 ✅ **已完成（2026-10-05，用户接入）**

**结论：4 段连段动画已就位，且走的是比原计划更优的路线。**

| 项 | 状态 |
|---|---|
| 剑模型 | ✅ **已就位，且不需要"挂剑"这一步**。`OVR - Roskva_Sword.fbx`（持剑版）自带 `Roskva_Sword_Hand` 网格（717 顶点），**100% 蒙皮到 `Bip001-R-Hand.002`**（`Bip001-R-Hand` 的子级 → 刚性跟随）。实测世界尺寸 **(0.364, 0.264, 1.321)**、中心 y=0.950 → **剑在右手、长约 1.32 m，尺寸正常**。⚠️ 它是**随网格自带骨骼绑定**，并未挂在 `B_Weapon_R` 下 —— 所以"用 `RoskvaAttachSword.cs` 把剑挂到右手"**本来就不需要**。 |
| **4 段连段动画** | ✅ **已由用户接入**，素材取自工程内已有的 **`Assets/Magical-Knight_Set/Animation/Humanoid/`**（一个 **100+ 动作的完整 Humanoid 动作包**），提取到 `Art/Animations/Player/` 后指给 `PlayerAC`：<br>· `Attack1` → `1_atk_sword03`（1.000 s / 30 帧）<br>· `Attack2` → `2_atk_sword04`（1.200 s / 36 帧）<br>· `Attack3` → `3_atk_sword05`（1.133 s / 34 帧）<br>· `Attack4` → `4_atk_sword01`（1.633 s / 49 帧）<br>四段**各自都挂着 `OnAttackHit`**（0.233 / 0.333 / 0.567 / 0.433 s），全部 `isHumanMotion=True`。 |
| 附带完成 | ✅ `Locomotion` 换成 `Idle01` + `strafe_run_strafe_front`；✅ `Dash`→`roll_front`、`Hit`→`hit_light_F_body`、`Death`→`dead_01`。<br>👉 **旧 TKD 剪辑（`combo_01_1~4` / `Standing Dive Forward` / `Stomach Hit` / `Sword And Shield Death`）已彻底不再使用**，全部移入 `Art/Animations/Player/old/`。 |

> ⚠️ **原计划的"Blender 走 BVH 生成挥砍动画"路线已被放弃**（我实际跑通过整条链路并产出过
> `SwordCombo_Roskva.fbx` + 4 段 `.anim`，但质量不如 `Magical-Knight_Set` 的手工动画：
> 我的版本 4 段全部等长 0.967 s，缺节奏差异）。**相关产物与工具已清理**：
> `Editor/SwordComboImporter.cs`、`SwordCombo_Roskva.fbx`（工程内 + 源目录）均已删除。
> 若将来仍需该路线，可用 `D:/ArtAssert/tools/sword_combo/make_sword_combo.py --process`
> 从现有 `sword_combo_01~04.bvh` 重新生成（链路已验证可用）。

- **依赖**：M2.1 ✅、M2.2 ✅
- **产出**：4 段连段有专用的**持剑**挥砍动画 ✅

### M2.4 特殊攻击 —— **火球 · 远距离** ✅ **已完成（2026-10-05）**

**形态（用户补充设定）**：游戏背景允许使用魔法 → 玩家的特殊攻击是**发射一个火球、远距离攻击**，
**命中后小范围爆炸**。⚠️ 这**取代**了原先的「剑 · 插地（`SwordSlam`，以自身为中心的圆形范围伤害）」设计。

**实现**（全部为本次新增/修改）：

| 文件 | 改动 |
|---|---|
| `Scripts/Gameplay/Player/Fireball.cs` | **新增**。直线飞行 → 命中/到射程就在原地爆炸 → `Physics.OverlapSphere` 对半径内**每个** `IDamageable` 结算一次（**去重**，避免多碰撞体被打多次）→ 自毁。⚠️ **命中伤害与溅射合并成一次 OverlapSphere** —— 爆炸中心就是命中点，直接命中者必然在范围内，这样**天然不重复** |
| `Scripts/Gameplay/Player/SpecialStateBehaviour.cs` | **新增**。挂在 `Special_Attack` 上，进/出时通知 `PlayerFSM.SetCasting()`（**不复用 `AttackStateBehaviour`**，因为它改的 `PlayerAttack.isAttacking` 被连段逻辑共用，会让语义混） |
| `Scripts/Gameplay/Player/PlayerState.cs` | 枚举加 `Special` |
| `Scripts/Gameplay/Player/PlayerFSM.cs` | 加 `Special` 三表项 + `TrySpecial()`（冷却→武器→体力，顺序不可换）+ `SpawnFireball()` + `SetCasting()`；`UpdateNeutral` 与 `Attack` 状态都可接特攻；`Update` 里走冷却计时。⚠️ 回 Idle 的判据用 `_specialAnimStarted && !_casting`，**否则 `SetTrigger` 当帧就会误判"动画已结束"**；另加 3 s 兜底防卡死 |
| `Scripts/Data/WeaponConfig.cs` | `SpecialAttackType` 加 **`Fireball = 3`**；新增 `specialProjectileSpeed` / `specialExplosionRadius` / `specialCastDelay` / `specialSpawnHeight` / `specialProjectilePrefab` |
| `Scripts/Data/Weapons/Weapon_Sword.asset` | `specialType` **SwordSlam → Fireball**；`specialRange` **3.5 → 15 m**；弹速 12 m/s、爆炸半径 2 m、出手延迟 0.55 s、出手高度 1.2 m；挂火球预制体。**普攻数据未动** |
| `Prefabs/Fx/Fireball.prefab` | **新增**。layer `Projectile(11)` + `SphereCollider`(isTrigger, r=0.35) + **kinematic `Rigidbody`**（`OnTriggerEnter` 需要）+ `Fireball` 脚本 + 球体视觉（`Mat_Fireball`，橙色自发光） |
| `Art/Materials/Mat_Fireball.mat` | **新增**。URP/Lit + Emission |
| `Art/Animations/Player/PlayerAC.controller` | 加参数 `Special`(Trigger)；**5 条进入转场**（`Locomotion` + `Attack1~4` → `Special_Attack`，`If:Special`）；**1 条转出**（`Special_Attack` → `Locomotion`，exitTime 0.85）；`Special_Attack` 挂 `SpecialStateBehaviour`。⚠️ 原状态是「孤岛」：进出 transition 与 behaviours **都是 0** |

**验收（Play 模式实测）**：

| 判据 | 结果 |
|---|---|
| `TrySpecial()` 首次调用 | ✅ 成功；**体力 100 → 70**（扣 30）；冷却 = 3 s |
| 冷却期内二次调用 | ✅ 返回 false，体力**不再扣** |
| `SpawnFireball()` | ✅ 生成成功；layer 11 `Projectile`；位置 = 玩家上方 1.2 m + 前方 0.4 m；组件 `Transform/SphereCollider/Rigidbody/Fireball` 齐全 |
| 爆炸范围伤害 | ✅ `Boxer1` **hp 30 → 0**；死亡触发 `Die()` → `SetActive(false)` |
| 自身排除 | ✅ 在自己脚下引爆，**玩家 hp 100 → 100 未受伤** |

⚠️ **未验证（受工具限制）**：① **实际按键触发**与**手感**——需你进 Play 按 Q / 右键实试；
② **火球飞行途中命中**（本次是手动引爆验证的伤害链路）；③ **`Special_Attack` 动画播完自动回 Idle**。
原因：MCP 无法模拟输入，且**测试时 Unity 窗口未聚焦 → 游戏循环暂停**（`Time.frameCount` 卡在 2），
所以逐帧行为只能靠手动激活/反射调用来验。

**可调参数**（都集中在 `Weapon_Sword.asset`）：`specialRange` 15 m / `specialProjectileSpeed` 12 m/s /
`specialExplosionRadius` 2 m / `specialCooldown` 3 s / `specialCastDelay` 0.55 s。

- **依赖**：M1.2（hitbox）✅、阶段 0 的 Special 输入 ✅、M2.2（武器数据）✅
- **产出**：远距离火球特殊攻击 ✅

### ~~M2.5 长枪 · 投掷与召回~~ ⏸️ **已移出主线 → §13 额外项目**

> 完整设计（投掷 / 飞行 / 落地 / 空手 / 召回 / 回手，及去程返程双判定等要点）
> 已移至 **§13 额外项目 / 加分项**。数据资产 `Weapon_Spear.asset` 已就绪，随时可捡回。

---

## 5. M3：敌人 + 寻路（3–4 天）

> 📘 **操作细节全在 `NAVMESH-GUIDE.md`**，本节只列步骤与验收。

### M3.1 NavMesh 烘焙 + 摆障碍物

**现状（实测）**：关卡内**没有任何障碍物**，无法演示"绕障"。包 `com.unity.ai.navigation 2.0.14` 已装、**尚无烘焙产物**。

**做什么**（详见手册 §5、§6）：
1. 建 `NavMeshRoot` 挂 `NavMeshSurface`，`Agent Radius 0.4 / Height 2.0`（与实测敌人碰撞体一致），`Use Geometry = Physics Colliders`
2. 摆 **3–6 个静态障碍物**（柱子/箱子），位置**故意卡在玩家—敌人连线上**
3. ⚠️ **摆完必须重新 Bake**
4. 门挂 `NavMeshObstacle` + 勾 **`Carving`**，并从 Include Layers 排除（⚠️ 关键：`DoorController` 只是 `_body.SetActive()`，门若被烘进 NavMesh，开门后导航网**不会自动更新**）

- **验收**：Scene 视图有蓝色导航网；门开/关时导航网上的洞能自动挖出/补回
- **依赖**：0.2（`Environment` 层）

### M3.2 敌人状态机基类

**现状**：`EnemyMeleeAI` 是**硬编码 switch**（`EState { Idle, Chase, Attack, Hit, Death }`），`EnemyRanged` **完全没有状态机**。

**做什么**：抽 `EnemyStateMachine` 基类（与玩家 FSM 同风格——状态表驱动），差异只在"状态集合 + 转移条件 + 数值"。
关键抽象：**把"选目标点"与"交给 Agent 走"分层**。

- **验收**：加第三种敌人（如盾兵）几乎只是纯配置
- **依赖**：无

### M3.3 视野感知组件

`GDD.md` §6.1：感知 = **距离 + 视野扇形 + 视线射线遮挡**。
⚠️ **NavMesh 完全不提供这个能力**，需自写 `EnemyPerception`。

- 判三件事：距离 / 扇形角度（`Vector3.Angle`）/ `Physics.Raycast` 遮挡
- **依赖**：无

### M3.4 剑兵（近战）

- 改造 `EnemyMeleeAI`：加 `NavMeshAgent`，⚠️ **删掉 `MoveTowardPlayer()` 里的 `transform.position +=`**（否则抖动/瞬移）
- 攻击时用 `_agent.isStopped = true`（⚠️ 不是已过时的 `Stop()`）
- 补状态：`Alert` / `Reposition`（现有五状态缺这两个）
- 🔧 需动画 5 个：Idle / Run / Attack / Hit / Death

### M3.5 弓兵（远程）

- ⚠️ **从零搭状态机**（现 `EnemyRanged` 只有一个冷却计时器）
- **kiting 核心**（手册 §9）：状态机先算"远离玩家的目标点" → `NavMesh.SamplePosition` 吸附 → 再交给 Agent
  ⚠️ **绝不能直接 `SetDestination(player.position)`**，那样弓兵会冲向玩家
- 前方摇被打断时**直接取消这次射击**（不要硬直后继续放箭）
- 弓箭也要检查视线遮挡
- 🔧 需动画 6 个：Idle / Run / Aim / Shoot / Hit / Death

### M3.6 敌人动画状态机

**现状（实测）**：`EnemyAC.controller` **只有 `idle` + `combo_01_1` 两个状态**，必须大幅扩充。

- **验收**：剑兵/弓兵的行为与动画状态一一对应，无"每帧 `SetTrigger`"隐患（现有 `EnemyMeleeAI` 的 `_anim.SetTrigger("Attack")` 写在 `Update` 里，属隐患）

---

### M3.7 多关卡场景拆分 + 关卡流转 ⭐ **结构变更新增（2026-10-05）**

**现状**：⚠️ 只有 `Game.unity` 一个游玩场景，3 个房间全在里面；
**没有任何关卡流转代码**（`GameManager` 只会"重载当前场景"或"回主菜单"）。

**做什么**：

| 步骤 | 内容 |
|---|---|
| ① 拆场景 | 由 `Game.unity` 拆出 `Level_01` / `Level_02` / `Level_03`，每关保留自己的战斗区 + 一份 GameManager / Canvas / Camera |
| ② `LevelFlow` | **新增脚本**：关卡索引 / 解锁 / 加载 / 结算。末关通关 → 通关结算；任意关死亡 → 失败结算 → **重开本关**（不是重开整局） |
| ③ 出口 | 每关清完后开启出口 → 玩家走进出口 → `LevelFlow` 加载下一关 |
| ④ 选关 | 主菜单加"关卡选择"（3 关，未解锁置灰，进度走 JSON 存档）；⚠️ 工期紧可降级为"直接进第一个未通关关卡" |
| ⑤ HUD | 加"Level N/3"进度 |

⚠️ **拆场景的执行方式（重要）**：Unity 编辑器通道当前**未连接**（无 Unity MCP），
手工编辑 `.unity` YAML 来拆分**风险高**（fileID / GUID 引用易断）。
**建议在 Unity 编辑器里操作**：复制 `Game.unity` → 逐份删掉不属于该关的房间 / 门 / 触发 → 另存为 `Level_0N.unity`。

- **产出**：3 个独立关卡场景 + 关卡流转 + 选关
- **验收**：主菜单 → Level_01 清怪 → 自动进 Level_02 → Level_03 通关 → 通关结算 → 回主菜单；
  中途死亡 → 失败结算 → 重开本关
- **依赖**：M3.1（各关各自烘焙 NavMesh）、M2.2（建议先装配剑）

---

## 6. M4：Boss（3–5 天）⚠️ **最大风险**

> ⚠️ **本阶段的招式表仍是"暂缓"状态** —— 这是全项目最大的未定义缺口，也是唯一"不补就没法排期"的地方。

### 前置：必须先钉死 Boss 范围

建议**最低粒度**（不必先做数值平衡）：

1. **招式名 ×4**（如：近战横扫 / 瞬移冲刺 / 跳跃砸地 / 远程投射）
2. 每招的**判定形状**（盒/球/扇形）
3. 每招的**前摇时长**（⚠️ 关键：Boss 不会被打断，**前摇是玩家唯一的反应窗口**，直接决定 Boss 战"打不打得明白"）
4. 阶段数（2 还是 3）
5. 是否召唤小怪

### 然后才是实现

| 步骤 | 内容 |
|---|---|
| M4.1 | `BossController`（**决策层**：评估玩家距离模式 → 选招） |
| M4.2 | 招式层（每招一个子状态机：前摇 → 判定 → 后摇 → 冷却） |
| M4.3 | 阶段切换（HP 50% → 短无敌 + 表现 → 阶段 2） |
| M4.4 | Boss 血条 UI |
| M4.5 | ⚠️ 瞬移冲刺的**预警表现**（避免"无预兆秒杀"，这是体验红线） |

**决策已定**：Boss 是**人形** → 可复用 Humanoid 重定向动画；**不会被打断**（只闪白，不引入硬直条/破防）。
🔧 需动画：全部招式动作（工作量最大，待招式表定了才知道量）

- **依赖**：M3（敌人状态机基类可复用）、招式表决策

---

## 7. M5：收口（2–3 天）

### M5.1 HUD 完善

| 元素 | 现状（实测） |
|---|---|
| 生命条 | ✅ `HPBar` + `HPFill` |
| 体力条 | ✅ **`EnergyBar`**（原 `ManaBar`，M1.1 改名换色并接入消耗/再生） |
| 关卡提示 | ✅ `RoomHintText`（⚠️ 文案英文硬编码 "Clean The Room" / "Door Open"） |
| 当前武器显示 | ⏸️ **V1 可省略**（只做剑、无切换 UI） |
| **关卡进度** | ❌ 新增（"Level 1/3"） |
| **Boss 血条** | ❌ 新增 |
| 伤害飘字 / 火花 | ✅ `HitFxSystem`（对象池） |
| 技能冷却 | ⚠️ **`SkillCD` 对象已存在**（Canvas 下的占位 `Image`），缺的只是冷却逻辑 |

### M5.2 结束界面

- 修正 `VICTPRY`（M1.4 已做）
- 补充统计：击杀数、剩余生命、到达关卡、存活时间
- ⚠️ 面板弹出时暂停（M1.3 已做）

### M5.3 音频 ⭐ 完全缺失

**现状**：`Assets/_Game/Audio/` **只有 `.gitkeep`**，无 `AudioManager`、无任何音频资源。

**决策已定**：**两种方式都用**
1. 程序化合成（无需 Key，可合成挥砍/命中/投掷/死亡/体力耗尽等 + 简单循环 BGM）
2. 现成素材替换（关键音效 + BGM）
- 🔧 **关键设计**：`AudioManager` 走 **"音效 ID → AudioClip"查表** → 换音源完全不碰代码，可先用程序化占位再逐个替换

### M5.4 存档（JSON）

**决策已定**：方式 B（JSON 文件），`Application.persistentDataPath` + `JsonUtility`。
⚠️ 注意：`JsonUtility` **不支持 `Dictionary`**，存档结构要避开。

实现要求：
- 带 `version` 字段（留迁移余地）
- 读写**包 `try/catch`**：文件不存在 → 用默认值；内容损坏 → 用默认值并写回
- ⚠️ 用 `persistentDataPath`，**不要**用 `dataPath`（打包后只读）
- 存盘时机：通关/死亡结算时 + 设置变更时（**不必每帧**）

存档内容：最好通关时间、通关/死亡次数、音量设置、最后使用的武器。

### M5.5 Windows 构建

- **验收**：从主菜单 → 选武器 → 打完一层 → 结算 → 重开，全流程在 exe 里跑通

---

## 8. 并行线：美术资源（⚠️ 必须尽早启动）

> **这是唯一"我无法代劳、且可能卡住主路径"的部分。** 详见 `ART-PIPELINE.md`。

### 8.1 需要你亲自做的事

| # | 事项 | 为什么必须你来做 |
|---|---|---|
| 1 | **配置 API Key**（Tripo 或 Meshy 二选一 + Sketchfab） | 在 Unity 的 MCP Tools 窗口操作，Key 存在安全存储里 |
| 2 | **从 Mixamo 下载动画**（需 Adobe 账号） | 需浏览器登录下载 FBX，我拿不到账号 |
| 3 | **决定关卡是否美化** | 白盒也完全能交付，这是取舍判断 |
| 4 | **Boss 招式表的最终确认** | 这是设计决策，不是技术决策 |

### 8.2 资源缺口清单

| 类别 | 缺口 | 优先级 |
|---|---|---|
| 玩家动画 | ✅ **已齐**：4 段连段 + Locomotion + Dash/Hit/Death + 特殊攻击（`atk_energy01`）—— 2026-10-05 用户已全部接入，另有 `Magical-Knight_Set` 100+ 动作可扩展 | 🟢 低 |
| 玩家动画 | ~~剑·插地~~（方案已改为**火球**，动画沿用 `atk_energy01`） | ⚪ 作废 |
| 玩家动画 | **长枪·投掷 / 召回**（2 个）—— 额外项目 | 🟡 中 |
| 武器模型 | **剑、长枪** 各 1 | 🟡 中 |
| 剑兵 | 模型 + 5 个动画 | 🔴 高 |
| 弓兵 | 模型 + 弓 + 6 个动画 | 🔴 高 |
| Boss | 模型（人形，放大体型）+ N 个招式动画 | 🔴 高（量待招式表） |
| 障碍物 | 柱子 / 箱子 3–6 个 | 🟡 中（**M3.1 需要**） |
| 音频 | BGM 3 首 + SFX ~10 个 | 🟡 中 |
| UI | 武器图标、技能图标、按钮、体力条样式 | 🟡 中 |

### 8.3 关键前提（决定了成本能否压到接近 0）

✅ **现有角色是 Humanoid 骨骼**（实测 `Animator.isHuman = true`，avatar = **`OVR - RoskvaAvatar`**）
→ **新角色模型可以复用现有全部动画**（Unity Humanoid 重定向），不必为每个新角色重做动作。

💡 **额外收获**：`OVR - Roskva_Animated.fbx` 的 Rig **已改 Humanoid** → 其 `Walk`（2.47 s）/`Idle01` 等 5 段
**可直接重定向**，**M3「小怪巡逻」所需的行走动画已有**（原动画库只有跑、没有走）。
⚠️ 但该 Avatar 仍缺 `Neck`（**T26**），建议 M3 前补。

⚠️ **真正的瓶颈不是模型，是动画。** 拿到任何新模型，第一件事：**设为 Humanoid 并 Configure**，否则无法复用动画。

---

## 9. M6：文档交付（2 天）

| 产出 | 内容 |
|---|---|
| 课程设计报告 | 含"方案选型"章节（可写：**为什么放弃自研 A\* 改用 NavMesh**）、"算法实现"章节（自研 A\* + 两个测试用例截图）、"数据持久化"章节（JSON 存档）、"测试"章节 |
| PPT | 含演示截图 |
| 演示视频 | V1 只有剑 → **无需为武器分段**；建议**按关卡分段演示**（3 关 + Boss），并突出"体力管理"这一差异化点 |
| 第三方资源清单 | ⚠️ 必交：**三个动画包** —— `TKDstyle_AnimSet`（257 MB）、**`Magical-Knight_Set`（790 MB，现役连段动画的来源）**、**`Rapier_Anim_Set`（793 MB，未使用）** —— 连同 `OVR - Roskva`（UE 素材包）、Sketchfab / Mixamo / AI 生成物一并列出（**报告加分项**）。⚠️ **三个包都不进 git**（合计约 1.84 GB），**但署名不能省**：见 `ART-ASSETS.md` §10 与 `PROJECT-CONTEXT.md` §2.1 |

---

## 10. 阻塞项：现在必须你拍板的事

按"是否阻塞开工"排序：

| # | 事项 | 是否阻塞 | 建议 |
|---|---|---|---|
| 1 | **Boss 招式表**（4 个招式名 + 判定形状 + 前摇时长） | ⚠️ **阻塞 M4** | 不阻塞 M1–M3，**但要尽早定**，否则 M4 无法排期 |
| 2 | **交付截止日期** | ⚠️ 影响范围取舍 | 有 deadline 才能判断要不要砍 Boss 规模 |
| 3 | **体力数值**（8/10/12/15 是否下调） | ❌ 不阻塞 | ✅ **建议标"待实测"而非现在写死** —— hitbox 化会改变有效命中率（瞬时采样 → 常驻判定体，命中更容易），命中率一变"打几套见底"就跟着变。**在测量前写死，是拿一组未校准的数换另一组** |
| 4 | 长枪三点：体力分配（投掷收费/召回免费）、飞行途中能否召回、投掷距离与速度 | ❌ 不阻塞任何主线 | ⏸️ **属额外项目**（§13），做长枪时再定 |
| 9 | **关卡间玩家状态**（生命 / 体力是否延续） | ❌ 不阻塞 | 建议**每关重置为满**（简单可控） |
| 5 | 命中是否回体力 | ❌ 不阻塞 | 建议 V1 不做 |
| 6 | 敌人丢失目标后的搜索行为 | ❌ 不阻塞 | 建议 V1 用"原地警戒" |
| 7 | 敌人之间是否分离/不重叠 | ❌ 不阻塞 | 建议做（NavMesh 自带避障，几乎免费） |
| 8 | 关卡是否美化 | ❌ 不阻塞 | 白盒可交付 |

---

## 11. 哪些环节能加速

| 环节 | 加速方式 |
|---|---|
| 建层、配碰撞矩阵 | Unity MCP 直接改 ProjectSettings，几秒完成 |
| 新建脚本、改核心代码 | 我写完可直接 `read_console` 验证编译 |
| 场景搭建（障碍物、UI 对象） | Unity MCP 可批量创建/摆放 |
| 每次改动后的编译检查 | `read_console` 一键查，不必切窗口 |
| **手感调参** | ⚠️ **无法代劳** —— 必须你在 Play 模式里亲自试 |

> ⚠️ **2026-10-05 现状**：Unity 编辑器通道**当前未连接** ——
> 上表中依赖"Unity MCP"的项（建层 / 配矩阵 / 场景搭建）暂时**需要你在编辑器里手动做**。

---

## 12. 下一步建议

**当前下一步（按优先级）**：**M2 已全部收口** ✅（M2.1 配置 / M2.2 装配 / M2.3 连段动画 / M2.4 火球特攻）

1. **M3 敌人 + 寻路**：NavMesh 烘焙 → 敌人状态机基类（T6）→ 剑兵 / 弓兵
   ⚠️ 动手前先**手动实试 M2.4 火球**（按 Q / 右键），并据 `atk_energy01` 的实际出手帧调 `Weapon_Sword.asset` 的 `specialCastDelay`（初值 0.55 s）
2. **M3.7 多关卡场景拆分**：⚠️ 建议放在 **M3 之后、M4 之前** —— 那时敌人已成型，拆场景才不白拆
3. **额外项目（§13，时间富余时做）**：长枪投掷召回 / 武器切换 / 剑的额外招式

**工作分工**：

- **我（AI）能做**：写脚本、改代码、跑编译检查、写文档
- **需要你做**：Blender 出模型 / 动画、Unity 里的场景搭建与拆关、Play 模式手感调参
- ⚠️ Unity 编辑器通道未连接时，**场景操作我无法代劳**

---

## 13. 额外项目 / 加分项（⏸️ 不在主线，时间富余时做）

> 定位：**做了加分，不做不影响 V1 交付**。这些内容的设计与数据地基都已就绪，可低成本捡回。
> 与 §10 阻塞项的区别：那些是"必须拍板才能推进"，这些是"主动收起、可延后"。

### 13.1 第二把武器 · 长枪（投掷与召回）

**状态**：✅ **数据已就绪** —— `Weapon_Spear.asset`（伤害 `10/12/9/16`、
判定盒 `3.2~3.9 / 1.0~1.2`、特殊攻击 `SpearThrow` 伤害 20 / 飞行 6）已在 `Data/Weapons/`，
且 `WeaponConfig` 已支持。**只差代码与动画。**

**完整设计**（原 M2.4，2026-10-05 移入此处）：

| 阶段 | 行为 |
|---|---|
| 投掷 | 点按 → **立即直线投出**（无蓄力、无瞄准） |
| 飞行 | 直线，对**去程路径**上敌人造成伤害 |
| 落地 | 到达**固定距离**后插在地上（世界物体） |
| 空手 | **不能普攻**，但**攻击键被重映射为召回** |
| 召回 | 点按攻击键 **或** 特殊攻击键 → 飞回，对**返程路径**造成伤害 |
| 回手 | 解除空手，恢复普攻 |

**技术要点**：

- 伤害**复用 M1.2 的 hitbox**（不是另做一套"碰一下就扣血"）
- ⚠️ **去程与返程必须各用一套"已命中目标集合"**，否则去程打过的敌人返程打不到
- 玩家死亡时**长枪留在原地**，直接进结算，不做特殊回收

- 新增 `PlayerFSM` 子状态：`SpearThrow` / `Unarmed` / `SpearRecall`
- 🔧 需动画：2 个（投掷 / 召回）
- **依赖**：M1.2 ✅、0.3 ✅

### 13.2 开局二选一 / 武器切换

**状态**：✅ `PlayerAttack.SetWeapon()` 已就绪；`WeaponConfig` 已支持多套配置。

- **做什么**：主菜单加选择 UI（剑 / 长枪）→ `WeaponManager` 持有当前武器 → 进关卡时装配
- **前提**：先做 §13.1（只有一把武器时，"选择"没有意义）
- **代价**：单次演示只能看到一把武器（演示视频建议分两段各打一次）

### 13.3 剑的额外招式

**状态**：⚠️ **动画已存在但未接入** —— `Elbow Uppercut Combo` 与 `Upward Thrust`
**也挂了 `OnAttackHit`**，但它们**不属于当前 4 段普攻**。

> ⚠️ **接入时必须给它们各自的判定体**，否则会**误开 4 段普攻的判定**。
> 详见 `PROJECT-CONTEXT.md` §6.4。

### 13.4 盾兵（第三种敌人）

抽好 `EnemyStateMachine` 基类后（M3.2），加第三种敌人"几乎只是纯配置"—— M3 的架构红利。

### 13.5 保留决策

> ✅ `WeaponConfig` 配置化 与 `Weapon_Spear.asset` **一律不删**。
> 它们是额外项目的地基，而且"配置驱动"本身就是主线的一部分（剑也读 `WeaponConfig`）。
> 同理，`WeaponManager` 虽降级为可选，但**设计已写明**（GDD §5.1），捡回时不用重新决策。
