# 完整地图迁移执行清单（放弃关卡制）

> 目标：把 `Assets/Scenes/URP_SiegeOfPonthus.unity`（城堡整图）变成可完整游玩的唯一场景。  
> 现状（2026-10-07 17:00 实测）：场景里**自有脚本实例 0 个**、无 NavMesh、19 个 Camera、96 盏灯、3672 个预制体实例。  
> 相关文档：[NAVMESH-GUIDE.md](NAVMESH-GUIDE.md)（烘焙）、[GDD.md](GDD.md)（玩法）、[ROADMAP.md](ROADMAP.md)（M3.7 已作废）。

标记说明：**🤖 = 我可以代做**（改脚本 / 改仓库文件）｜**🖱️ = 需在 Unity 里操作**（我目前进不去：Unity MCP 8080 端口未监听）

---

## 阶段 0 · 前提确认（阻塞，先做完再往下）

- [x] **0.1 确认城堡材质不是洋红** 🖱️  
  选中 `Assets/LeartesStudios/SiegeOfPonthus/HDRP/Art/Materials/M_Brick_Height_Blend_Wall.mat`，看 Inspector 最上方 **Shader** 栏。  
  实测该材质引用的 `00e1dde34a61ac840b1e96e36cf9d8ef` = `HDRP/Art/ShaderGraphs/S_SnowHeight.shadergraph`，而该图的 `m_ActiveTargets` **同时启用了 BuiltIn + HD + Universal 三个目标** ⇒ 同一 shader 资产内含 URP 子着色器，**理论上正常**。  
  **判据**：Shader 栏显示 `S_SnowHeight`（不是 `Missing`），Scene 视图不是一片亮粉。  
  **失败回退**：若真是 Missing/洋红，本清单阶段 3 之后全部作废 —— 必须先换成 URP 变体的预制体重搭（工作量翻倍），请停下来告诉我。
- [x] **0.2 备份** 🤖
  - 场景：`Assets/Scenes/URP_SiegeOfPonthus.unity` → `.workbuddy/backup/`
  - 会被改的脚本：`GameManager.cs`、`RoomController.cs`、`RoomTrigger.cs`、`EnemyAIController.cs`
  - 会被改的第三方资源：**不动**（层改动一律走"场景实例覆盖"，不 Apply 到 Prefab）

---

## 阶段 1 · 引导层搬入（消除那 3729 条 NRE）

**根因**：`InputService.cs` 的 guid `3ad7ec71…` 在 `Game.unity` 出现 1 次、在新场景出现 **0 次**，于是 `PlayerFSM.cs:189` 每帧抛 `NullReferenceException`。

- [x] **1.1 从 `Assets/Scenes/Game.unity` 复制这 7 个根对象到新场景** 🖱️  
  用 `Ctrl+C` / `Ctrl+V`（在两个场景之间粘贴会保留对象内部引用）：
  | 对象              | 关键组件                                                                  | 少了会怎样                                          |
  | --------------- | --------------------------------------------------------------------- | ---------------------------------------------- |
  | `InputService`  | InputService                                                          | **每帧 NRE 刷屏**（玩家 / 相机 / 暂停全瘫）                  |
  | `Main Camera`   | Camera + AudioListener + CameraFollow + UniversalAdditionalCameraData | `Camera.main` 为 null ⇒ 伤害飘字与打击火花静默失效           |
  | `FxSystem`      | HitFxSystem                                                           | 日志里那 2 条 `AttackFxBridge.cs:24 [fx 未指定]`       |
  | `GameManager`   | GameManager + HitStop                                                 | 死亡/胜利不结算；顿帧静默失效（`HitboxController.cs:184` 有判空） |
  | `Canvas`        | PauseMenu / HPBar / ManaBar                                           | 无 HUD、ESC 无法暂停                                 |
  | `EventSystem`   | EventSystem                                                           | UI 点不动                                         |
  | `Global Volume` | Volume（URP 后处理）                                                       | 画面无后处理                                         |
- [x] **1.2 不要复制的对象** 🖱️  
  `Room1` `Room2` `Room3`（关卡制）、`Ground` `Obstacles`（新场景有自己的地形与建筑）、`NavMeshRoot`（阶段 4 重建）、`Directional Light`（新场景自带 1 盏平行光 + 96 盏点光源）。
- [x] **1.3 重连丢失的引用** 🖱️ ← **最容易漏的一步**  
  复制过来的对象，凡是"指向没被一起复制的东西"的字段都会变成 `None`。逐个检查：
  | 对象            | 字段                                              | 连到哪                 |
  | ------------- | ----------------------------------------------- | ------------------- |
  | `Main Camera` | `CameraFollow._target`                          | 新场景里的 Player        |
  | `GameManager` | `gameOverPanel` / `victoryPanel` / `resultText` | Canvas 下的面板         |
  | `Canvas`      | `PauseMenu` 的面板引用                               | Canvas 下对应面板        |
  | `Canvas`      | `HPBar` / `ManaBar` 的目标                         | Player              |
  | `FxSystem`    | HitFxSystem 的 DamagePopup / HitSpark 预制体        | `_Game/Prefabs/Fx/` |
  | Player 预制体实例  | `AttackFxBridge.fx`                             | `FxSystem`          |
  > 参考值（旧场景 `Main Camera` 上的 CameraFollow，已实测）：`_focusHeight 1.5`、`_distance 3`、`_pitch 30`、`_minPitch -60`、`_maxPitch 60`、`_groundClearance 0.3`、`_autoDetectGroundY 1`、`_minDistance 1.5`、`_maxDistance 10`、`_zoomStep 0.5`、`_lookSensitivity 0.35`、**`_smoothTime 0`**（必须保持 0，硬跟随）、`_followTargetYaw 0`、`_alignYawToTargetOnStart 1`。
- [x] **1.4 判据** 🖱️  
  把 Player 预制体放进新场景 → Play：**Console 无任何 `NullReferenceException`**；WASD 能走、鼠标能转视角、攻击有火花与飘字。

---

## 阶段 2 · 相机清理（19 → 1）

- [x] **2.1 处理资产包自带的展示相机** 🖱️  
  实测新场景有 **19 个 `Camera` 组件**，物体名：`Camera01` ~ `Camera12`、`CameraAnim02`、`CameraAnim04` 等（配 `HDRP/Art/Animations/CameraAnimXX.controller` 做展示运镜）。  
  做法：在 Hierarchy 里搜 `Camera`，除 `Main Camera` 外全部 **取消勾选（SetActive false）**。  
  **判据**：Scene 视图只剩一个相机视锥；Play 时 Game 视图画面由 CameraFollow 控制。
  > 不建议直接删除 —— 万一你想参考原展示场景的构图，禁用的对象还在。
- [ ] **2.2 检查 CameraFollow 在新地形上的地面检测** 🖱️  
  `_autoDetectGroundY = 1` 会在 `Start` 用 `Physics.Raycast` 探地面。城堡地面高度与旧白盒不同，Play 后确认相机没穿地、没卡在空中。不对就把 `_autoDetectGroundY` 关掉手动填 `_groundY`。

---

## 阶段 3 · 层与碰撞体（烘焙的前置条件，阻塞）

- [ ] **3.1 决定可行走几何走哪个层** 🖱️ ← **需要你拍板**  
  实测旧场景的约定是：`Ground` 与 `Obstacles` 都在 **Environment(12)**，`NavMeshSurface` 的 `m_LayerMask = 4096`（**只收层 12**）。  
  而城堡预制体**全部在 Default(0)**（抽样 20/20）。⇒ **按现状直接烘焙会得到一张空网格。**
  | 方案                 | 操作                                                                                              | 代价                                              |
  | ------------------ | ----------------------------------------------------------------------------------------------- | ----------------------------------------------- |
  | **A（推荐）改场景实例的层**   | 把城堡可行走部分（Floor / Wall / Stairs / Wall_Arch / Blockade_Arch / Terrain）的实例 Layer 改成 `Environment` | 需要批量脚本（3672 个实例手改不现实）；只覆盖场景实例，**不污染第三方 Prefab** |
  | B 改 Include Layers | NavMeshSurface 的 `Include Layers` 勾上 `Default`                                                  | 一分钟搞定，但木桶/旗帜/绳索/树/铁甲装饰等**全部**会被烘成障碍 ⇒ 敌人绕远路、被卡住 |
  | C 改 Prefab 资源      | 直接改 `HDRP/Art/Prefabs/*.prefab` 的 Layer                                                         | ❌ 污染第三方资源，且以后重导包会被覆盖，**不建议**                    |
  **我的建议**：A。要的话我写一个编辑器脚本 `Tools/BatchSetLayer.cs`（或等 Unity MCP 可用了直接跑代码）批量把指定预制体名开头的实例设为 `Environment`。
- [ ] **3.2 修掉 288 处负缩放告警** 🖱️  
  日志实测：`BoxCollider does not support negative scale or size` **×288** —— 城堡预制体用了负缩放，Unity 把碰撞体强制取正 ⇒ **碰撞几何与实际模型不一致**，会导致穿墙、悬空、卡住。  
  **判据**：Console 里这条警告归零。  
  ⚠️ 修法是改模型层级里的 scale 符号或换 convex `MeshCollider` —— 属于改第三方资源，**先告诉我你打算怎么改，我不擅自动**。

---

## 阶段 4 · NavMesh 烘焙

- [ ] **4.1 建烘焙负责人** 🖱️  
  Hierarchy 空白处右键 → `Create Empty` → 改名 `NavMeshRoot` → `Add Component` → 搜 `Nav Mesh Surface`。
- [ ] **4.2 填参数**（照搬旧场景的取值）🖱️
  | 字段              | 填什么                 | 说明                     |
  | --------------- | ------------------- | ---------------------- |
  | Agent Type      | `Humanoid`          | 工程里唯一的 Agent Type（见下表） |
  | Collect Objects | `All`               |                        |
  | Include Layers  | 按阶段 3 的决定           | 关键项                    |
  | Use Geometry    | `Physics Colliders` | 与物理边界完全一致              |
  | Default Area    | `Walkable`          |                        |
  | Voxel Size      | `0.13333334`        | 旧场景原值                  |
  Agent Type 实测参数（`ProjectSettings/NavMeshAreas.asset`，**已就绪不用改**）：`Humanoid` → `agentTypeID 0`、`radius 0.4`、`height 2`、`slope 45`、`climb 0.75`。敌人 `Monster1.prefab` 的 `NavMeshAgent` 是 `agentTypeID 0 / radius 0.4`，**两边一致 ✔**。
- [ ] **4.3 点 Bake** 🖱️  
  ⚠️ 烘焙前若有门类物体，**先临时禁用它的 `NavMeshObstacle` 的 Carving** —— 实测 Carving 挖出的洞会被永久写进烘焙数据，开门也走不通（[NAVMESH-GUIDE.md](NAVMESH-GUIDE.md) §5）。本工程目前没有 `Door` 层物体，可跳过，但以后加门必须注意。  
  **判据**：Scene 视图地面浮现**蓝色半透明网格**；`Assets/Scenes/` 下生成新的 `NavMesh-*.asset`。  
  看不到蓝色网格：`Window → AI → Navigation` → 勾 `Show NavMesh`。
- [ ] **4.4 冒烟测试（别跳过）** 🖱️  
  放一个 Cube + `Nav Mesh Agent`，写 5 行脚本让它 `SetDestination(player)`，Play 看它是否**绕开墙和柱子**跑向玩家。  
  ⚠️ **Bake 出来一片空白**：九成是 Include Layers 没盖住城堡几何 —— 改完**重新 Bake**。

---

## 阶段 5 · 性能

- [ ] **5.1 压掉阴影** 🖱️  
  日志实测：`Reduced additional punctual light shadows resolution by 8 to make **234 shadow maps** fit in the 2048x2048 shadow atlas`，刷了 8925 行。新场景自身 96 盏灯（88 Point + 7 Spot + 1 Directional），其余来自火把类预制体。  
  做法：把纯装饰性的火把/壁灯 `Shadows` 设为 `None`（或降到 `Hard Shadows`）。  
  **判据**：那条 `Reduced additional punctual light shadows` 警告消失（阴影图降到 ≤ 64 张）。
- [ ] **5.2 量三个数** 🖱️  
  Play + Profiler：`Batches`、`SetPass Calls`、`Shadows`。3672 个预制体实例建议先看数再决定要不要合并静态网格。

---

## 阶段 6 · 敌人刷新改造（弃用 `RoomTrigger`）

**好消息：不需要写"传目标"的逻辑。** 实测两处现成机制可直接复用：

1. `EnemyAIController.EnsureTarget()`（337–350 行）：`Target == null` 时**每 1 秒** `GameObject.FindGameObjectWithTag("Player")` ⇒ 你手摆的敌人会**自己找到玩家**。
2. `EnemyAIController.OnEnable()`（270 行）会调 `Loco.SyncWithNavMesh()` ⇒ **`SetActive(true)` 复活后自动 `Warp` 吸附回网格**（`EnemyLocomotion.cs:297`），正是 [NAVMESH-GUIDE.md](NAVMESH-GUIDE.md) §7.3 要求的那一步，代码里已经写好了。

- [ ] **6.1 新增 `EncounterZone.cs`** 🤖  
  一个遭遇区 = 一个 `BoxCollider(isTrigger)` + 一个组件：
  - `_enemies[]` —— 区域内你手摆的敌人，`Start` 时全部 `SetActive(false)`
  - **激活半径 25m / 脱战半径 40m**（**两个值必须不等**，否则边界反复开关）
  - `_maxConcurrent`（同区同时活跃上限，建议 4~6）
  - 玩家进入 Trigger 后**隔帧激活 1~2 个**，避免一次性激活卡顿
  - 敌人死亡计数；全灭后按策略处理（见 6.2）
- [ ] **6.2 选重生策略** 🖱️（默认已选 A）
  | 策略              | 行为                                                         | 适用                            |
  | --------------- | ---------------------------------------------------------- | ----------------------------- |
  | **A 不重生（推荐默认）** | 清空即永久清空                                                    | 完整地图探索；天然给出通关条件；零额外开销         |
  | B 据点循环          | 全灭后延迟 `respawnDelay` 重刷同一批（`SetActive` 复用，不 `Instantiate`） | 少数防守据点；**必须设波数上限**，否则玩家永远走不出去 |
  | C 事件驱动          | 只在 Boss / 剧情事件后放一批                                         | 用 `GameEvents` 扩一个事件          |
- [ ] **6.3 弃用旧机制** 🤖
  - `RoomTrigger` / `RoomController` 的"进门 → 关门 → 开打"（`RoomController.OnPlayerEntered` 还强绑"上一间房已清"的线性顺序）与开放地图冲突，全部停用。
  - `DoorController` 只是 `_body.SetActive()`，可留作以后做可开关的门。
  - `RoomHintText` 可以留用，改成 Zone 的提示。
  - 旧场景里的 `Room1/2/3` 对象不要搬过来（阶段 1.2）。

---

## 阶段 7 · 胜利条件改代码（**唯一必须动的逻辑**）

- [ ] **7.1 改 `GameManager` 的胜利触发** 🤖  
  现状：`RoomController.FinishClear()` 里只有 `isFinalRoom == true` 才调 `GameEvents.RaiseBossDied()`，`GameManager` 订阅它弹胜利面板。  
  新地图没有"最终房间" ⇒ 必须改成"**全部 Zone 清空**"或"**指定 Boss 死亡**"。  
  **做法**：`GameEvents` 加 `ZoneCleared`；`EncounterZone` 全灭时上报；`GameManager` 统计总 Zone 数，全清则 `OnVictory()`（也可保留一个真 Boss，走 `BossDied`）。  
  **判据**：清完所有 Zone 后胜利面板弹出，`resultText` 显示时间。

---

## 阶段 8 · 收尾验收

- [ ] **8.1 全流程 Play 一遍** 🖱️：Console 干净（除已知的 Polygonmaker fallback 提示）、能走能打、敌人会绕路、清空后弹胜利、ESC 能暂停。
- [ ] **8.2 git 别把大包带进去** 🤖：`Assets/LeartesStudios/` **4.3 GB 目前未被 `.gitignore` 覆盖**，`git ls-files` 计数为 0（未跟踪）。照前四个大包的注释风格补一段忽略规则。
- [ ] **8.3 文档同步** 🤖：`ROADMAP.md` 里 M3.7「场景拆分 + 关卡流转」作废；`GDD.md` 的关卡/房间章节改为开放地图描述；`NAVMESH-GUIDE.md` §5 的「Include Layers 勾 Default」与实际约定（层 12 Environment）不一致，需修正。改完跑 `.workbuddy/check_docs.py`。

---

## 依赖关系速查

```
阶段0（洋红确认）
  └─ 阶段1（引导层）──→ 阶段2（相机）
  └─ 阶段3（层）────→ 阶段4（烘焙）──→ 阶段6（敌人激活后才有意义）
                          └─ 阶段5（性能，可并行）
  └─ 阶段6 ──→ 阶段7（胜利条件）──→ 阶段8
```

**阻塞项**：阶段 0.1（洋红）、阶段 3.1（层方案）。
