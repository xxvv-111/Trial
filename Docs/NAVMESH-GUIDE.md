# Unity 自带寻路（NavMesh）接入指南

> **这份文档是写给"使用这个项目的人"看的操作手册**，不是设计文档。
> 目标：让你从零学会用 Unity 自带的 AI Navigation 让敌人绕开障碍物追击玩家。
> 每一步都写明「点哪里、填什么、为什么」，照着做就能跑通。
>
> 最后更新：2026-10-05 | 状态：**待执行**（包已装好，尚未烘焙）
> 相关文档：`GDD.md` §6.5（寻路方案）、`PROJECT-CONTEXT.md` §9 T14
>
> ⚠️ **2026-10-05 关卡结构变更提示**：关卡由"单场景内 3 个房间"改为 **多关卡独立场景**
> （`Level_01` / `Level_02` / `Level_03`）。本文中出现的"**房间**"请理解为
> "**某个关卡场景内的战斗区**" —— 烘焙、Agent 改造、门阻挡等**操作步骤全部不变**，
> 只是**每关各自烘焙一次**（每个 `Level_0N.unity` 里各放一个 `NavMeshSurface`）。

---

## 0. 怎么用这份文档

| 你是… | 建议读法 |
|---|---|
| 第一次接触 NavMesh，想赶紧跑起来 | 先跳去 **§5 手把手**，15 分钟做出效果，再回头读 §2 概念 |
| 想知道为什么不用原来那个 A* | 读 **§1 决策记录** |
| 正在写代码，怕用错 API | 读 **§4 已核实 API**（这节的成员名都是本项目里实测反射出来的，可直接照抄） |
| 遇到问题卡住了 | 直接查 **§11 常见坑速查表** |
| 想知道要改哪些文件 | 看 **§12 改动对照表** |

**标记约定**：`✅` 已具备 / `⚠️` 容易踩的坑 / `❌` 缺失 / `🔧` 需要你动手

---

## 1. 为什么改用 NavMesh（决策记录）

### 1.1 变更内容

| 项 | 原方案 | 现方案 |
|---|---|---|
| 寻路实现 | 接入**自研 A***（网格烘焙 + 调用 `AStar.FindPath` + 路径平滑） | ✅ **Unity 自带 AI Navigation（NavMesh）** |
| 决策来源 | `GDD.md` §6.5、`PROJECT-CONTEXT.md` §10.1 第 12 条 | 本次变更 |

### 1.2 改的三条理由

**① 自研 A\* 有一个硬缺陷，不修就不能用**

`AStar.FindPath` 只重置了起点（`start.G / F / Parent`），对邻居节点的 `nb.G`、`nb.F`、`nb.Parent` **一次都不重置**。而判断语句 `if (tentative < nb.G || !open.Contains(nb))` 正好要读这些残留值。

后果比"路径不好"严重得多：`Reconstruct` 是**无条件顺着 `Parent` 往上走**的，一旦新旧指针混成环，它会**死循环，直接把编辑器卡死**。

而 `GDD.md` §6.5 第 6 条要求"每 0.3s 重算一次"——这必须复用同一张网格。所以这不是"顺手加个 Reset"，而是**不加就不能接入**。

**② 工作量大**：自研方案要自己做网格烘焙、障碍物标记、路径平滑、动态避障。这四件事 NavMesh 全部内置。

**③ 效果差**：NavMesh 有成熟的避障（RVO）、区域代价、Off-Mesh Link。自研要达到同等水平，远超课程设计预算。

### 1.3 代价（要如实知道）

⚠️ **报告里不能再写"自行实现 A\* 并接入游戏"了。** 这是原方案唯一的优势（§6.5 把它列为"报告含金量高"）。

🔧 **补救办法（建议采用）**：`Assets/_Game/Scripts/AI/AStar.cs` 和 `Assets/Editor/AStarSelfTest.cs` **保留不动**。它们在报告里作为**独立的"路径搜索算法实现"章节**，与实际运行方案分开叙述：

- 算法章节：写 A\* 的原理、你自己实现的 `Node` 结构、曼哈顿启发式、4 邻域扩展
- 测试章节：`Tools/A* 自测`（5×5 绕障，校验不穿墙）与 `Tools/A* 自测2：无路`（整列堵死应返回 `null`）**两个用例可以直接截图当测试证据**
- 工程选型章节：写"实际运行时选用 NavMesh，因为……"（把 §1.2 的理由抄过去）

这样"算法能力"和"工程判断力"两头都能写，甚至比原来只写一句"接了自研 A\*"更有说服力——**能说明你为什么放弃自己写的东西**，是加分项。

---

## 2. 概念速通（10 分钟）

NavMesh 就四个东西要理解：

| 概念 | 一句话 | 在本项目里对应什么 |
|---|---|---|
| **NavMesh**（导航网格） | 铺在地面上的一张"可行走多边形网"。**烘焙** = 把场景几何体算成这张网 | 三个房间的地面 + 绕开墙和柱子 |
| **NavMeshSurface** | 挂在某个对象上，负责"点一下生成 NavMesh"的组件 | 新建一个对象挂它，统一负责烘焙 |
| **NavMeshAgent** | 挂敌人身上。你只告诉它"**去哪**"，它自己算"**怎么走**" | 剑兵、法师 |
| **NavMeshObstacle** | **动态**障碍。开启 carving 后：它出现 = 在网里挖个洞，它消失 = 洞自动补回 | 🔧 **本项目关着的门**就靠它 |

**两个心智模型**（记住这个就不会用错）：

```
NavMesh   = 给敌人铺好的"人行道路网"
NavMeshAgent = 会看路牌的司机 —— 你只说目的地，别说怎么开
NavMeshObstacle = 施工路障 —— 临时把某段路挖断
```

对比一下现状就明白了。现在 `EnemyMeleeAI.MoveTowardPlayer()`（第 111–121 行）是这样的：

```csharp
transform.position += dir.normalized * (moveSpeed * Time.deltaTime);
```

这是**闭着眼朝玩家直线冲**——撞上墙/柱子就卡死。NavMesh 要替换的正是这一行。

---

## 3. 环境核对（✅ 已就绪，无需安装）

| 项 | 状态 |
|---|---|
| 包 `com.unity.ai.navigation` | ✅ **2.0.14** 已在 `Packages/manifest.json` 中 |
| 是否已有烘焙产物 | ✅ 无（干净的，没有历史污染） |
| 烘焙入口 | NavMeshSurface 组件上的「**Bake**」按钮 |

⚠️ **Unity 6 的一个重要变化**：旧的 `Window → AI → Navigation` 烘焙面板**已废弃**。
Unity 6（本项目 6000.0.83f1）的正确姿势是：**给对象挂 `NavMeshSurface` 组件，在它的 Inspector 面板上点 Bake**。

> 那个旧窗口现在还在菜单里，但只用来调**烘焙参数（Agent 半径/高度等）和可视化显示**，不用它烘焙。

---

## 4. 已核实 API（可直接照抄，避免写错）

> 以下成员名是**在本项目里用反射实测**得到的（Unity 6000.0.83f1 + 包 2.0.14），不是凭记忆写的。

### 4.1 `NavMeshSurface`（烘焙用）

- **命名空间 / 程序集**：`Unity.AI.Navigation` / `Unity.AI.Navigation`
- **方法**：`BuildNavMesh()`、`UpdateNavMesh()`、`AddData()`、`RemoveData()`、`GetBuildSettings()`
- **关键属性**：

| 属性 | 用途 |
|---|---|
| `collectObjects` | 收集哪些对象参与烘焙（All / Volume / Current Object 等） |
| `layerMask` | **只烘焙这些层** |
| `useGeometry` | 用物理碰撞体还是渲染网格 |
| `agentTypeID` | 用哪套 Agent 参数 |
| `defaultArea` | 默认区域类型 |
| `voxelSize` / `minRegionArea` | 体素精度 / 最小区域面积 |
| `navMeshData` | 烘焙产出的数据资产 |
| `ignoreNavMeshAgent` | 是否忽略自带的 Agent |
| `ignoreNavMeshObstacle` | 是否忽略 Obstacle |

### 4.2 `NavMeshAgent`（敌人移动用）

- **命名空间**：`UnityEngine.AI` / 程序集 `UnityEngine.AIModule`
- **常用方法**：`SetDestination(Vector3)`、`ResetPath()`、`Warp(Vector3)`、`CalculatePath(...)`、`SamplePathPosition(...)`、`SetAreaCost(...)`
- **常用属性**：`speed`、`angularSpeed`、`acceleration`、`stoppingDistance`、`isStopped`、`isOnNavMesh`、`updateRotation`、`updatePosition`、`autoBraking`、`autoRepath`、`remainingDistance`、`pathPending`、`steeringTarget`、`avoidancePriority`、`areaMask`、`radius`、`height`

⚠️ **已过时的成员，别用**（反射结果明确标了 Obsolete）：
`Stop()`、`Resume()`、`walkableMask`、`GetLayerCost()`、`SetLayerCost()`

| 别用 | 改用 |
|---|---|
| `agent.Stop()` | `agent.isStopped = true` |
| `agent.Resume()` | `agent.isStopped = false` |
| `agent.walkableMask` | `agent.areaMask` |

### 4.3 `NavMeshObstacle`（门的动态阻挡用）

- **属性**：`carving`、`carveOnlyStationary`、`carvingMoveThreshold`、`carvingTimeToStationary`、`shape`、`size`、`center`、`height`、`radius`、`velocity`

### 4.4 `NavMesh`（静态工具类）

- **方法**：`SamplePosition(...)`、`CalculatePath(...)`、`Raycast(...)`、`AddLink(...)`、`RemoveLink(...)`、`SetLinkActive(...)`
- **字段**：`AllAreas`
- ⚠️ 过时：`Triangulate()`、`AddOffMeshLinks()`、`RestoreNavMesh()`、`GetNavMeshLayerFromName()`

---

## 5. 手把手：跑通最小可用（约 15 分钟）

### 第 1 步 🔧 打开场景

打开 `Assets/Scenes/Game.unity`。

### 第 2 步 🔧 建一个"烘焙负责人"

在 Hierarchy 空白处右键 → `Create Empty`，改名 `NavMeshRoot`。
（放哪都行，它只是个挂脚本的容器，建议拖到 `Ground` 下面方便管理。）

### 第 3 步 🔧 挂组件

选中 `NavMeshRoot` → Inspector → `Add Component` → 搜 `Nav Mesh Surface` → 添加。

### 第 4 步 🔧 填参数

⚠️ **这一步最容易搞混：要填的地方有两处，别在 `NavMeshSurface` 上找 Radius。**

#### （A）`NavMeshSurface` 组件上的字段（Inspector 里直接填）

| 参数 | 填什么 | 为什么 |
|---|---|---|
| **Agent Type** | `Humanoid` | 工程里只有这一个 Agent Type。⚠️ 这个下拉**只是"选哪套体型参数"**，Radius / Height **不在**这里填 |
| **Collect Objects** | `All` | 房间是 Cube 拼的白盒，All 最简单 |
| **Include Layers** | 勾上 `Default`（地面/墙都在 Default） | 决定哪些层参与烘焙 |
| **Use Geometry** | `Physics Colliders` | ⚠️ 见下方说明 |

> ⚠️ **本机反射实测**：`Unity.AI.Navigation.NavMeshSurface` 的序列化字段一共 18 个
> （`m_AgentTypeID` / `m_CollectObjects` / `m_Size` / `m_Center` / `m_LayerMask` / `m_UseGeometry` /
> `m_DefaultArea` / `m_GenerateLinks` / `m_IgnoreNavMeshAgent` / `m_IgnoreNavMeshObstacle` /
> `m_OverrideTileSize` / `m_TileSize` / `m_OverrideVoxelSize` / `m_VoxelSize` / `m_MinRegionArea` /
> `m_NavMeshData` / `m_BuildHeightMesh` / `m_SerializedVersion`）—— **里面没有任何 Radius / Height**。
> 体型参数属于 **Agent Type**，要去 (B) 改。

#### （B）Agent Type 的体型参数

菜单 `Window → AI → Navigation` → **Agents** 页 → 选 `Humanoid`：

| 参数 | 填什么 | 为什么 |
|---|---|---|
| **Radius** | **`0.4`** ⚠️ **要改** | 实测敌人 `CapsuleCollider.radius` = **0.4**，保持一致。⚠️ 工程里**当前默认值是 0.5** |
| **Height** | `2.0` | 实测敌人 `CapsuleCollider.height` = **2.0**（当前默认已是 2.0，不用改） |
| **Step Height** | `0.3` | 台阶高度，本项目是平地，0.3 够用（当前默认 0.75，改不改都行） |
| **Max Slope** | `45` | 平地，默认够用 |

> ⚠️ **为什么 Radius 必须和碰撞体一致**：烘焙用的是 Agent Type 的 Radius —— 导航网会按它**把墙边内缩**；
> 敌人身上挂的 `NavMeshAgent` 也按同一套参数走。若烘焙用 0.5、碰撞体只有 0.4，差这 0.1 m 会在
> **门框 / 窄道**处显形：导航网比实际能通过的宽度更窄 → 敌人**在门口卡住或绕远路**。
> （项目里实测数据：Agent Type `Humanoid` = radius **0.5** / height 2 / slope 45 / climb 0.75；敌人胶囊 = radius **0.4** / height 2。）

> ⚠️ **为什么推荐 `Physics Colliders` 而不是 `Render Meshes`**：
> 本项目的墙、地面、门都是 **Cube + BoxCollider**（白盒）。用碰撞体烘焙，结果会和实际物理边界**完全一致**。用渲染网格有时会因为模型缩放/非凸网格出偏差。

### 第 5 步 🔧 点 Bake

> ⚠️⚠️ **烘焙前必做：先把所有门的 `NavMeshObstacle` 临时禁用（或取消勾选 Carving）！**
> 本机实测：**Carving 挖出的洞会被写进烘焙数据**。同一份场景、唯一差别是"烘焙时门 obstacle 开不开"：
>
> | 烘焙时门 obstacle | 门线扫描结果（`#`=可走 `.`=不可走） | 结果 |
> |---|---|---|
> | **启用** | `######.....######` | ❌ 门洞被**永久烘死**，开门也走不通 |
> | **禁用** | `#################` | ✅ 门洞通的 |
>
> 所以正确的顺序是：**临时禁用门的 Carving → Bake → 再把 Carving 启用回来**。
> 烘焙完请用 §10 的方法复核一次门线（本手册 §15.4 有可抄的判据）。

点 Inspector 上那个 **Bake** 按钮。
**成功的标志**：Scene 视图里地面浮现一层**蓝色半透明网格**。

### 第 6 步 🔧 验证它真的能用（别跳过）

1. 新建一个 `Cube`，随便放在房间一角
2. 给它 `Add Component` → `Nav Mesh Agent`
3. 新建一个空脚本 `NavTest.cs` 挂在它上面：

```csharp
using UnityEngine;
using UnityEngine.AI;

public class NavTest : MonoBehaviour
{
    [SerializeField] private Transform _target;   // Inspector 里把 Player 拖进来
    private NavMeshAgent _agent;

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    private void Update()
    {
        if (_target != null && _agent.isOnNavMesh)
            _agent.SetDestination(_target.position);
    }
}
```

4. 运行 → 这个 Cube 应该**绕开墙和柱子**跑向 Player

🔧 **看不到蓝色网格？** 打开 `Window → AI → Navigation`，勾上 `Show NavMesh`。
⚠️ **Bake 出来一片空白？** 九成是 **Include Layers 漏了 Ground**，或者 `Collect Objects` 的范围没盖住房间。改完**重新 Bake**。

---

## 6. 放障碍物 🔧（必须做，寻路和视野都依赖它）

**实测现状：房间内目前没有任何柱子、箱子或掩体。** 这是"绕障"能演示出来的前提，得先摆。

| 项 | 做法 |
|---|---|
| 做什么 | 用 `Cube` 缩放成柱子（细高）/ 箱子（方矮） |
| 放几个 | 3–6 个。建议**每个房间 1–2 个柱子 + 1 个箱子** |
| 放哪里 | ⚠️ 故意卡在**玩家和敌人可能连成的直线上**，这样"绕障"才看得出来 |
| 要静态吗 | ✅ **要**。静态的（Navigation Static）会被烘焙进 NavMesh |
| 不静态行不行 | 行，但要挂 `NavMeshObstacle`（运行时挖洞，不参与烘焙） |
| 摆完要做什么 | ⚠️ **必须重新 Bake**，否则导航网里没有它们 |

> 障碍物的 Layer 建议新建一个 `Environment` 层统一管理（顺便解决 `GDD.md` §8 碰撞矩阵里缺层的问题，见 §13）。

---

## 7. 改造敌人移动 🔧（核心改动）

### 7.1 现状 vs 目标

**现状**（`EnemyMeleeAI.cs` 第 111–121 行）：

```csharp
private void MoveTowardPlayer()
{
    if (_player == null) return;
    Vector3 dir = _player.position - transform.position;
    dir.y = 0f;                                    // 只在地面走
    if (dir.sqrMagnitude > 0.01f)
    {
        transform.rotation = Quaternion.LookRotation(dir.normalized);  // 脸朝玩家
        transform.position += dir.normalized * (moveSpeed * Time.deltaTime);  // ❌ 直线冲
    }
}
```

**目标**：加一个 `NavMeshAgent`，删掉 transform 位移。

```csharp
// 在 EnemyMeleeAI 顶部新增
using UnityEngine.AI;
[SerializeField] private NavMeshAgent _agent;

// Awake 里
_agent.speed = moveSpeed;
_agent.stoppingDistance = attackRange * 0.9f;   // 留 10% 余量
_agent.updateRotation = true;                    // 转向也交给 Agent

// Chase 状态里，替换掉 MoveTowardPlayer()
if (_agent.isOnNavMesh)
    _agent.SetDestination(_player.position);
```

### 7.2 三条硬性注意

⚠️ **① 绝对不要再保留 transform 位移。**
Agent 在改 `transform.position`，你的代码也在改 → 两者打架，表现为**敌人抖动、抽搐、瞬移**。要么全交给 Agent，要么全自己写，不能混。

⚠️ **② 攻击时要用 `isStopped`，不是 `Stop()`。**
`Stop()` 已被标记过时。`EnemyMeleeAI` 进入 `EState.Attack` 时：

```csharp
_agent.isStopped = true;    // 攻击时定住
```

攻击结束回 `Chase` 时 `_agent.isStopped = false;`

⚠️ **③ `stoppingDistance` 免费帮你实现了「攻击范围」。**
`GDD.md` §6.1 要求"进入攻击范围才能发起攻击"。设 `stoppingDistance = attackRange * 0.9` 后，Agent 会自动在离玩家 `attackRange` 处停下 —— 不用再自己算距离了。这就是原 `EState.Chase` 里 `if (distSqr <= attackRange * attackRange)` 那段判断的替代品。

### 7.3 敌人被 SetActive 开关的问题

`RoomController` 用 `e.gameObject.SetActive(true)` 来激活敌人（实测源码如此）。
Agent 在对象被重新激活后可能需要确认位置合法性：

```csharp
private void OnEnable()
{
    if (_agent == null) return;
    if (!_agent.isOnNavMesh) _agent.Warp(transform.position);   // 强行吸附到网格上
}
```

---

## 8. 门的动态阻挡 ⚠️（本项目特有的坑，务必看）

### 8.1 问题

**实测现状**（`DoorController.cs` 全文只有 17 行）：

```csharp
public void Close() { _body.SetActive(true); }    // 关门 = 门体激活
public void Open()  { _body.SetActive(false); }   // 开门 = 门体隐藏
```

门是 `Cube + BoxCollider`。如果把门**烘焙进 NavMesh**：

> 门关着的时候被烘成了障碍 → 玩家清完怪、门开了 → **NavMesh 不会自动更新**，敌人依然认为那里走不通，会绕远路甚至卡住。

### 8.2 ✅ 推荐解法：给门挂 `NavMeshObstacle`，开启 carving

| 步骤 | 操作 |
|---|---|
| 1 | 选中门体对象，`Add Component` → `Nav Mesh Obstacle` |
| 2 | 勾上 **`Carving`** |
| 3 | `Shape` 选 `Box`，`Size` 调到和门体一致 |
| 4 | `Carve Only Stationary` 可以**取消勾选**（门是瞬间开关的） |
| 5 | ⚠️ **门的 Layer 不要放进 NavMeshSurface 的 Include Layers** |

**效果**：
- 门 `SetActive(true)`（关） → Obstacle 生效 → 在 NavMesh 上**挖个洞** → 敌人过不去
- 门 `SetActive(false)`（开） → 洞自动**补回** → 通路恢复

⚠️ **carving 有默认延迟**（`carvingTimeToStationary`）。如果开门后敌人反应迟钝，把 `Carving Move Threshold` 调小、`Time To Stationary` 调到 `0.1` 左右。

### 8.3 备选解法（不推荐）

门开着时在运行时调用 `surface.BuildNavMesh()` 重新烘焙。
❌ **不推荐**：重烘焙很慢（几十毫秒到几百毫秒），每次开门都卡一下，且会打断所有 Agent。

---

## 9. 法师的「保持距离」（kiting）⚠️ 最容易做错的地方

### 9.1 核心认知（务必记住）

> **NavMesh 只解决「怎么走到某点」，不解决「该走哪」。**

所以下面这种写法是**完全错误**的：

```csharp
_agent.SetDestination(_player.position);   // ❌ 法师会直接冲向玩家
```

法师要的是"保持 6m 距离"，这需要**你的状态机先算出一个目标点**，再交给 Agent。

### 9.2 ✅ 正确做法

```csharp
// 算出"远离玩家"的一个方向
Vector3 dirAway = (transform.position - _player.position).normalized;

// 沿这个方向退到舒适距离
Vector3 want = _player.position + dirAway * comfortDistance;   // 舒适距离 6m

// ⚠️ 关键一步：把这个"纸面上的点"吸附到 NavMesh 上的合法点
if (NavMesh.SamplePosition(want, out NavMeshHit hit, 4f, NavMesh.AllAreas))
    _agent.SetDestination(hit.position);
else
    _agent.ResetPath();     // 找不到合法点就原地不动
```

⚠️ **`NavMesh.SamplePosition` 必须用。** 否则你给的点如果落在了墙里或 NavMesh 外，Agent 会报错：

```
"SetDestination" can only be called on an active agent that has been placed on a NavMesh.
```

### 9.3 三种行为的实现

对照 `GDD.md` §6.3 法师状态机（🔄 2026-10-06：这些走位**都在 `Chase` 里算**，法师**没有**独立的 `Reposition` 状态；剑兵的 `Reposition` 则只做"攻击后退开"，见 §6.2）：

| 状态 | 目标点怎么算 |
|---|---|
| **BackOff**（距离 < 6m，后撤） | `player.position + dirAway * comfortDistance`（如上） |
| **Approach**（距离 > 9m，靠近） | `player.position` 直接给（但要留 `stoppingDistance`） |
| **Strafe**（6–9m，横向绕行） | 在上面基础上，再加一个**垂直于连线的偏移**：`Vector3.Cross(dirAway, Vector3.up)` |

### 9.4 ⚠️ NavMesh 不管视线遮挡

`GDD.md` §6.1 要求"被墙挡住就看不见/不射击"，§6.3 要求"射箭也要检查视线"。
**NavMesh 完全不提供这个能力**，必须自己写：

```csharp
if (Physics.Linecast(transform.position + Vector3.up, _player.position + Vector3.up, out RaycastHit hit))
    // 打到了墙 → 视线被挡，不射击
```

---

## 10. 视野感知（与 NavMesh 无关，别混淆）

`GDD.md` §6.1 的「感知 = 距离 + 视野扇形 + 视线射线遮挡」是**另一个独立系统**，NavMesh 帮不上忙。

要自己写一个 `EnemyPerception` 组件，判三件事：
1. 距离是否在 `aggroRange` 内
2. 玩家是否落在扇形视野角内（`Vector3.Angle(transform.forward, dirToPlayer) < 视野角/2`）
3. `Physics.Raycast` 是否被墙挡住

**职责分清**：
- **视野组件**答"**我有没有看见玩家**"
- **寻路（NavMesh）**答"**我该怎么走过去**"
- **状态机**根据前两者的结果决定"**我现在该做什么**"

---

## 11. 常见坑速查表

| 症状 | 原因 | 解法 |
|---|---|---|
| 控制台报 `SetDestination can only be called on an active agent that has been placed on a NavMesh` | Agent 不在导航网格上（没烘焙 / 位置在网格外） | 检查是否 Bake 过；用 `Warp()` 吸附；或改 `SamplePosition` 找合法点 |
| 敌人**抖动、抽搐、瞬移** | transform 位移和 Agent 同时改位置 | 把 `MoveTowardPlayer()` 里的 `transform.position +=` 删掉 |
| 敌人卡在门口 / 门开了还是绕路 | 门被烘焙进 NavMesh 了 | 改 §8：门用 `NavMeshObstacle` + `Carving`，并从 Include Layers 里排除 |
| 敌人**走进墙里** | 墙没参与烘焙，或 Agent Radius 小于实际体型 | 检查 Include Layers；把 Agent Radius 调到与碰撞体一致（0.4） |
| Bake 完**一片空白** | Include Layers 漏了 Ground；Collect Objects 范围不对 | 补上 Ground 层，重新 Bake |
| 运行时改了场景，寻路**失效** | 烘焙是**静态**快照 | 动态物体用 `NavMeshObstacle`；或运行时 `BuildNavMesh()` |
| Agent 到不了玩家身边，总差一点 | `stoppingDistance` 太大 | 调小它（它等于"攻击范围"，和 §7.2 ③ 是一回事） |
| 一堆敌人**挤成一团** | 没用避障 | 调 `avoidancePriority`（各不相同）、`radius`，确认 `obstacleAvoidanceType` 非 None |
| 开着 Play 模式改了 NavMesh 参数 | 运行时不生效 | 退出 Play 模式再 Bake |

---

## 12. 改动对照表（要动哪些文件）

| 文件 / 资产 | 改动 | 里程碑 |
|---|---|---|
| `Assets/_Game/Scripts/Gameplay/Enemy/EnemyMeleeAI.cs` | ✅ **已完成（M3.2）**：`MoveTowardPlayer()` 与其中的 `transform.position +=` **已删除**，移动改由 `EnemyLocomotion` 统一处理；攻击 / 硬直时走 `Stop()`（内部用 `isStopped`，**没用已过时的 `Stop()`**）。⚠️ 剩余：给预制体挂 `NavMeshAgent` 组件、补 `Alert` / `Reposition` | M3.2 ✅ / M3.4 ⏳ |
| `Assets/_Game/Scripts/Gameplay/Enemy/EnemyCaster.cs` | ✅ **已完成（M3.5）**：由 `EnemyRanged.cs` **改名而来**（`git mv` 保 GUID），已从零搭好状态机 + kiting 目标点计算 + `SamplePosition` 吸附；⚠️ 覆写了 `AgentStoppingDistance = 0.3`（否则默认值会让 kiting 失效） | M3.5 ✅ |
| `Assets/_Game/Scripts/Gameplay/Room/DoorController.cs` | ✅ **已完成（M3.1）**：门体移到新建的 `Door` 层（slot 13）+ 挂 `NavMeshObstacle`（`Carving` ✓、`Carve Only Stationary` ✗）。⚠️ **代码未改动**，全在场景里做的 | M3.1 ✅ |
| 🆕 `EnemyStateMachine.cs`（基类） | ✅ **已完成（M3.2）**：把「**选目标点**」与「**交给 Agent 走**」分层。实为 `EnemyFsmBase`（非泛型外壳）+ `EnemyStateMachine<TState>`（**纯 C#** 三字典表驱动）；⚠️ **刻意为纯 C#**，因为 Unity 不支持泛型组件的序列化，`[SerializeField]` 放进泛型基类有丢引用的风险 | M3.2 ✅ |
| 🆕 `EnemyAIController.cs` / `EnemyLocomotion.cs` | ✅ **已完成（M3.2，原计划外新增）**：前者是**非泛型**共用层（组件 / 数值 / 受击反应 / 动画触发）；后者是「交给 Agent 走」层，**没挂 Agent 或没烘焙时自动退回直线位移**，烘焙后自动切换，调用方代码不变 | M3.2 ✅ |
| 🆕 `EnemyPerception.cs` | ✅ **已完成（M3.3）**：扇形视野 + Raycast 遮挡（与 NavMesh 无关，见 §10）。⚠️ 两个实现要点：结果**按帧缓存**、遮挡层**自动排除自己与目标所在的层**（否则每次都判成"被遮挡"） | M3.3 ✅ |
| 🆕 `NavMeshRoot` + `Assets/Scenes/NavMesh-Game.asset` | ✅ **已完成（M3.1）**：烘焙 **580 ms** / **651 三角形** / **73 KB**，覆盖 ±249.5 米。配置：Agent Type `Humanoid` / `Collect Objects = All` / **Include Layers = 只勾 Environment** / `Use Geometry = Physics Colliders` | M3.1 ✅ |
| 🆕 房间内**障碍物**（柱子/箱子 3–6 个） | ✅ **已完成（M3.1）**：`Obstacles` 父物体下 `Obstacle_01`–`Obstacle_06`（1.5×2.5×1.5 米白盒 Cube，`Environment` 层）。每个房间 2 个，其中一个**故意卡在"入口门 → 该房间敌人"的连线上** | M3.1 ✅ |
| `Assets/_Game/Scripts/AI/AStar.cs` | ✅ **保持不动**，作为报告算法章节素材 | — |
| `Assets/Editor/AStarSelfTest.cs` | ✅ **保持不动**，两个用例当报告测试证据 | — |

---

## 13. 这次决策连带改掉的其它文档条目

| 位置 | 原内容 | 改为 |
|---|---|---|
| `GDD.md` §6.5 | 方案 A（自研 A*）**已选** | 改为 NavMesh 已选；对比表保留作报告选型素材 |
| `GDD.md` §13.3 | 需新增"网格生成 + 路径平滑" | 改为"NavMesh 接入" |
| `GDD.md` §13.4 | 工作量含 A* 网格生成 | 下调（NavMesh 省掉自建网格与平滑） |
| `GDD.md` §15.2 | 待定：A* / NavMesh 二选一 | ✅ 已解决 |
| `PROJECT-CONTEXT.md` §9 **T14** | "A* 未接入游戏，已升级为必做" | 改为"改用 NavMesh；A* 降级为报告算法章节素材，不再接入游戏" |
| `PROJECT-CONTEXT.md` §10.1 **#12** | 寻路接入自研 A* | 翻转为 NavMesh |

⚠️ **连带的影响**：`GDD.md` §8 的「分层与碰撞矩阵」要求 6 个层（Player / Enemy / PlayerHitbox / EnemyHitbox / Projectile / Environment），但**实测项目里只有 `Enemy` 一个存在**。这次摆障碍物正好需要 `Environment` 层，建议**一起建掉**。

---

## 14. 官方参考

| 主题 | 链接 |
|---|---|
| AI Navigation 包手册 | https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html |
| NavMeshSurface 组件 | https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavMeshSurface.html |
| NavMeshAgent API | https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html |
| NavMeshObstacle（carving） | https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavMeshObstacle.html |

> 💡 **本项目的 API 名称都已用反射在本机核实过**（见 §4）。如果官方文档和你写的代码对不上，**以 §4 为准**——那是你这个 Unity 版本实际存在的成员。

---

## 15. 实际执行记录：M3.1 到底是怎么做的（2026-10-06）

> 这一节是**照着做就能复现**的操作记录，不是设计说明。每一步都写了「手动点哪里 / 填什么 / 怎么算成功」。
> 本轮由 AI 通过 Unity MCP 通道执行，所以下面同时给出**手动路径**（你自己做时点哪里）和**实测数值**（判据）。

### 15.1 七步总览

| # | 做什么 | 手动路径 | 关键数值 | 验收判据 |
|---|---|---|---|---|
| 1 | 改 **Agent Type** 体型 | `Window → AI → Navigation` → **Agents** 页 → 选 `Humanoid` | Radius **0.5 → 0.4** | 读 `NavMesh.GetSettingsByID(0).agentRadius == 0.4` |
| 2 | 新建 **`Door` 层**并把门移过去 | `Edit → Project Settings → Tags and Layers` → Layers 加 `Door` | slot **13** | `Door1/2/3` 的 `layer == Door` |
| 3 | 门挂 **`NavMeshObstacle`** | 选中 `Door1/2/3` → `Add Component` → `Nav Mesh Obstacle` | `Shape=Box`，勾 **`Carving`**，取消 `Carve Only Stationary`，`size` 跟随 BoxCollider | 关掉该组件后门线应变可走 |
| 4 | 摆 **6 个障碍物** | 新建 6 个 `Cube`，挂在空物体 `Obstacles` 下 | 缩放 **1.5 × 2.5 × 1.5**，`layer = Environment` | 与 4 个敌人出生点间距 **≥ 4 米** |
| 5 | 建 **`NavMeshRoot` + `NavMeshSurface`** | 空物体 `NavMeshRoot` → `Add Component` → `Nav Mesh Surface` | 见 §5 第 4 步两张表 | — |
| 6 | ⚠️ **先禁用门的 Carving，再 Bake** | Inspector 上的 **`Bake`** 按钮 | — | 三条门线扫描全 `#` |
| 7 | 给敌人挂 **`NavMeshAgent`** | 编辑 `Boxer.prefab` / `Gunner.prefab` → `Add Component` → `Nav Mesh Agent` | `type=Humanoid`，`Speed 3`，`Stopping 0.9`，敌兵/法师 `Avoidance Priority` 分别 **50 / 60** | 场景 4 个实例都出现该组件 |

### 15.2 ⚠️ 三个必须知道的坑（都是本轮实测撞出来的）

**① Carving 会被写进烘焙数据 —— 这是最坑的一个。**
同一份场景、唯一差别是"烘焙时门上的 `NavMeshObstacle` 开不开"：

| 烘焙时门 obstacle | 门线扫描（`#`=可走 `.`=不可走） | 结果 |
|---|---|---|
| **启用** | `######.....######` | ❌ 门洞**永久烘死**，开门也走不通 |
| **禁用** | `#################` | ✅ 门洞通的 |

→ **烘焙前必须临时禁用门的 `NavMeshObstacle`，烘完再启用。** 用 Inspector 的 `Bake` 按钮时，
记得先取消勾选 `Carving`，烘完再勾回来。（本轮已按"禁用→烘焙→启用"的顺序落地，并用"Carving 全关
+ 重载资产"独立复核过资产是干净的。）

**② `NavMeshSurface` 上根本没有 Agent Radius / Height 字段。**
本机反射实测该组件只有 **18 个**序列化字段（`m_AgentTypeID` / `m_CollectObjects` / `m_LayerMask` /
`m_UseGeometry` / `m_VoxelSize` / `m_NavMeshData` …），**没有任何体型字段**。
体型参数属于 **Agent Type**，只能去 `Window → AI → Navigation` → **Agents** 页改。§5 第 4 步已据此拆成两张表。

**③ `NavMeshObstacle` 的 Carving 不是当帧生效的。**
在同一帧里"关掉组件 → 立刻采样"会读到**旧结果**，很容易误判成"门没被排除出烘焙"（本轮就被骗了一次）。
跨帧（下一次调用）再看才对。做这类验证时务必把"改状态"和"读结果"**分到两次执行**里。

### 15.3 本轮实测数值（可当报告数据用）

| 项 | 数值 |
|---|---|
| Agent Type | 全工程 **1** 个：`id=0 Humanoid`，`radius 0.4` / `height 2` / `slope 45` / `climb 0.75` |
| 敌人碰撞体 | `Boxer` / `Gunner` 都是 `CapsuleCollider radius 0.4 / height 2`（Radius 取 0.4 就是为了跟它对齐） |
| 烘焙耗时 | **580 ms** |
| 导航网规模 | **651 三角形** / 1368 顶点 / **≈ 248553 m²** |
| 烘焙资产 | `Assets/Scenes/NavMesh-Game.asset`，**73 KB** |
| 覆盖范围 | `min = (-249.5, 0.1, -249.5)` → `max = (249.5, 5.1, 249.5)`（整块地面，因为 `Ground` 是 500×500 的 Plane） |
| `layerMask` 生效验证 | mask 只留 `Door` 层时，导航网面积降到 **0 m²**（若 mask 被忽略，应是整块地面）→ 证明 **Include Layers 真的在过滤** |
| 资产干净性验证 | Carving 全关 + 把资产重新挂载重载 → 三条门线**全 `#`** |
| 寻路（玩家在同一房间内） | `Boxer1` / `Boxer2` / `Boxer3` / `Gunner` **全部 `PathComplete`** |
| 绕障 | `Boxer1(0,0,10) → 门内侧(0,0,1)`：路径 **9.18 米** / 直线 9 米，**绕行比 1.020**；拐点在 `Obstacle_01` 的 z 区间内偏到 `x = -0.8`（确实绕开了柱子） |

### 15.4 复现用的验证手法（不用 Play 也能判）

```csharp
// 扫一条线看某段导航网通不通（半径要 > 导航网的 y 偏移，否则会误判成"全不可走"）
//   ⚠️ 本轮踩过：用 0.05 米半径扫，结果全是"不可走" —— 因为导航网在 y≈0.066 平面上
for (float z = -1.6f; z <= 1.61f; z += 0.2f)
{
    NavMeshHit h;
    bool walkable = NavMesh.SamplePosition(new Vector3(0f, 0f, z), out h, 0.2f, NavMesh.AllAreas);
    Debug.Log(z + " : " + walkable);
}
```

```csharp
// 算一条路径并看它是不是真的通了（PathComplete = 通；PathPartial = 中途被截断）
var path = new NavMeshPath();
NavMesh.CalculatePath(startOnMesh, endOnMesh, NavMesh.AllAreas, path);
Debug.Log(path.status + " 拐点=" + path.corners.Length);
```

⚠️ 两个判定陷阱：
- **采样半径太小会误判**：`SamplePosition` 找的是"半径内最近的可走点"，导航网在 y≈0.066，半径 0.05 就够不着 → 全判成不可走。用 **0.2~0.3** 比较稳。
- **`PathPartial` 不一定是 bug**：玩家在房外、门关着时房间本就该封死，此时 `PathPartial` 是**正确行为**。

### 15.5 还没验证的（必须在 Play 模式确认）

- **门的动态 Carving**：关门堵、开门（`DoorController` 把 `_body.SetActive(false)`）通。
  编辑模式下 Carving 的更新时机不可靠，**必须在 Play 模式下按 `RoomController` 的实际流程确认**。
- **敌人的实际移动手感**：抖动 / 瞬移 / 一堆敌人挤成一团（`Avoidance Priority` 已给 50 / 60 做区分）。
