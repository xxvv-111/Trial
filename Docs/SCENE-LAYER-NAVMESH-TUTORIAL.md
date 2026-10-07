# 城堡地图：层设置 + 碰撞体 + NavMesh 烘焙（手把手）

> 目标：让 `Assets/Scenes/URP_SiegeOfPonthus.unity` 这张完整地图能烘焙出可用的导航网格，敌人能在里面正常寻路。
> 本教程里的所有数字都是**实测**的，不是估的。相关：[NAVMESH-GUIDE.md](NAVMESH-GUIDE.md)（原始接入指南）、[SCENE-MIGRATION-CHECKLIST.md](SCENE-MIGRATION-CHECKLIST.md)（总清单）。

---

## 0. 先看懂原理（3 分钟，别跳）

### 0.1 层（Layer）在这里是干嘛的

`NavMeshSurface` 组件上有一个 **Include Layers** 字段。**只有被勾中的层里的物体，才会被烘进导航网格。** 别的层完全不看。

你的工程从旧场景那里继承的约定是：

| 东西 | 层 | 值 |
|---|---|---|
| 可行走几何（地面、墙、障碍） | `Environment` | 12 |
| 门（要被排除、改用 NavMeshObstacle 动态阻挡） | `Door` | 13 |
| 玩家 | `Player` | 8 |
| 敌人 | `Enemy` | 6 |
| 其它一切（道具、UI、灯光、相机） | `Default` 等 | 0 / 5 |

旧场景里 `Ground` 和 `Obstacles` 都在 `Environment`，`NavMeshSurface` 的 mask 就是 `4096`（只收层 12）。

### 0.2 ⚠️ 核心判据：什么该参与烘焙

一句话：**「玩家会被它挡住的东西」就应该参与烘焙，「玩家能穿过去 / 在头顶上 / 细得能钻过去的东西」就不该。**

为什么要这么定？因为烘焙出来的网格是**敌人也要遵守的路**。如果敌人不能穿过木桶而你也不能，那木桶就该烘进去 —— 两边一致，才不会有"敌人贴着桶绕来绕去"的怪象。

### 0.3 ⚠️ 一个关键机制：没碰撞体的物体根本不会被烘

`NavMeshSurface` 的 **Use Geometry** 字段你选的是 **`Physics Colliders`** —— 意思是**按物理碰撞体**来烘，不是按模型网格。

所以：**没有 Collider 的物体，即使层对了也不会参与烘焙。** 这条能替我省掉一大半手工分类。我实测了新场景里全部 204 种预制体，装饰物分两类：

- **没有碰撞体（自动被忽略，不用管）**：`SM_Rope04`(40 个)、`SM_Shrub`(35)、`SM_Banner02/03`(27)、`SM_Plane`(13)、`SM_Twig_02`(12)
- **有碰撞体（要人为判断）**：`SM_Candle_2`(29)、`SM_Metal_Ring`(28+12)、`SM_Wall_Torch`(22)、`SM_ChainLantern`(18)、`SM_Bucket`(15)、`SM_Sword`(38)、`SM_Armour_A_*`、`SM_Barrel`(40) …

第二类里，**木桶是该挡路的**（玩家也撞得到），**蜡烛 / 火把 / 灯笼 / 水桶 / 挂在墙上的剑和甲胄是不该挡路的**。这就是下面 §2 要处理的部分。

---

## 1. 准备工作（2 分钟）

1. 打开 `Assets/Scenes/URP_SiegeOfPonthus.unity`
2. **先 `Ctrl+S` 保存**，保证磁盘上是最新状态
3. 备份场景文件（我可以代做，或者你自己把 `.unity` 复制一份）
4. 清空 Console（Console 窗口右上角 `Clear`），这样后面只看新产生的信息
5. 记一下现状数字，用于最后对比：
   - Console 里 `BoxCollider does not support negative scale` 警告 **288 条**

> ⚠️ **改层之前不要进 Play 模式**，编辑模式的改动才好回滚。

---

## 2. 设置层（主要工作量）

### 2.1 现状（我实测的）

新场景的根节点是：`Meshes`、`Lights`、`Cameras`、`DynamicCameras`、`Particles`、`FogPlane`、`Terrain`、`Global Volume`、`StaticLightingSky`、`Room`、`SceneIDMap`(×2) —— **全部在层 0（Default）**。城堡的 **204 种预制体 / 3672 个实例也全部在层 0**。

⚠️ **所以这里不要用"把 Include Layers 勾上 Default"这个偷懒办法。** 我实测过：连 `SM_Barrel`、`SM_Candle_2`、`SM_Sword`、`SM_Armour_A_Helmet` 这些都有碰撞体，勾 Default 会让它们全部变成障碍物，敌人会在蜡烛和铁环之间绕路。

### 2.2 第一步：整批设成 Environment（一次搞定）

用 **Hierarchy 搜索 + 多选**，这是纯 UI 操作，不需要写代码：

1. 点 Hierarchy 窗口顶部的**搜索框**
2. 输入 `SM_`
3. 等列表刷新完，**点中任意一个结果**，然后按 **`Ctrl+A`**
   - ⚠️ 多选之后 Inspector 顶部的 **Layer** 下拉会显示成 `—`（横杠），这是**正常的**，表示"选中项的值不一致"
4. 点那个 Layer 下拉 → 选 **`Environment`**
5. 这一次就把绝大多数的建筑与道具都设好了

再补三个搜索词，把不在 `SM_` 前缀下的也设上：

| 搜索词 | 是什么 |
|---|---|
| `SM_` | 城堡本体（地面 / 墙 / 砖 / 脚手架 / 道具） |
| `Terrain` | 地形（那一大片地面） |
| `Room_Floor` | 你自己铺的房间地板 |

> ⚠️ **别把 `Lights` / `Cameras` / `Particles` / `FogPlane` / `Global Volume` 改到 Environment** —— 它们不在上面这几个搜索词里，只要你不手动去选就没事。

### 2.3 第二步：把"不该挡路"的挑回 Default

现在 Environment 层里混进了一批**有碰撞体但玩家不该被挡住**的小物件。**逐个搜下面这些词，每次搜完 `Ctrl+A` → Layer 改回 `Default`**：

| 搜索词 | 实例数 | 为什么不该挡路 |
|---|---|---|
| `SM_Candle` | 29 | 桌上蜡烛，玩家能跨过去 |
| `SM_Wall_Torch` | 22 | 挂在墙上的火把 |
| `SM_ChainLantern` | 18 | 吊在空中的灯笼 |
| `SM_Lantern` | 14 | 提灯 |
| `SM_Bucket` | 15 | 水桶 |
| `SM_Metal_Ring` | 40 | 墙上铁环（挂绳用的装饰） |
| `SM_Sword` | 38 | 插着/挂着的剑 |
| `SM_Sheild` | 若干 | 盾牌 |
| `SM_Armour` | 若干 | 甲胄（挂起来展示的） |
| `SM_Window` | 22 | 窗户嵌在墙里，会形成薄片障碍 |
| `SM_Perch` | 23 | 高处的栖木 |
| `SM_Brazier` | 11 | 火盆 |
| `Smoke` | 若干 | 烟 |
| `Fire` | 若干 | 火焰特效 |
| `Fog` | 若干 | 雾面片 |

**有争议、你自己定**（我先说清楚两边的理由）：

| 搜索词 | 保留的理由 | 排除的理由 |
|---|---|---|
| `SM_Barrel` (54) | 真实的木桶，玩家也撞得到，烘进去一致 | 数量多，会让通路变窄 |
| `SM_Rock` (119) + `SM_Tree` (89) | 岩石和树本来就该挡路 | 它们的碰撞体是简化 Box，可能比模型大，会挡出"看不见的墙" |
| `SM_Pointy_wood` (167) | 拒马本来就该挡路 | 数量最多，且很多是斜插的，会不会在斜坡上出问题要实测 |
| `SM_Table` / `SM_Bench` / `SM_Shelf` / `SM_Rack` | 家具是实体 | 室内家具挡住路未必是你想要的 |

👉 **我的建议**：`SM_Barrel`、`SM_Table` 这一批**先保留在 Environment**；`SM_Rock`、`SM_Tree`、`SM_Pointy_wood` **先排除**（改回 Default），等第一版烘出来跑一圈，看看"没有岩石/树当障碍"是不是有问题，再决定要不要加回来。理由：石头和针木的碰撞体形状最不可控，先排除能让第一版更干净。

### 2.4 验证层设对了

⭐ Hierarchy 搜索框支持 **层过滤**，拿它来验收：

1. 搜索框输入 **`l:Environment`**
2. 应该只剩建筑、地面、地形和你决定保留的道具
3. 再输入 **`l:Default`**，看看剩下的是不是都是"应该被忽略的"

**判据**：`l:Environment` 里**一眼扫不出**旗帜、绳索、蜡烛、灯笼、窗户、甲胄这类东西。

---

## 3. 处理碰撞体告警（先别急着改！）

### 3.1 ⚠️ 先更正一个说法

我在上一版清单里写过"城堡预制体用了负缩放" —— **这是错的，实测推翻了**：

| 位置 | 负缩放处数 |
|---|---|
| 城堡预制体资源（188 个 `.prefab`） | **0** |
| 你自己建的 RoomBuild 预制体（14 个） | **0** |
| 旧场景 `Game.unity` | **0** |
| **新场景 `URP_SiegeOfPonthus.unity`** | **250 处，分布在 217 个实例上** |

所以**负缩放完全来自你摆地图时的镜像操作**（把同一块模型 scale 的某一轴设成 `-1` 来翻转复用），跟第三方资源无关，跟旧工程也无关。这也意味着**修改权完全在你手上**，不涉及"改别人的资源"。

分布（实测，按处数排序）：

| 预制体 | 负缩放处数 |
|---|---|
| `SM_Blockade_Pillar` | 73 |
| `SM_Blockade_Top` | 21 |
| `SM_Wall02` | 20 |
| `SM_ScafSet_Wood04` | 17 |
| `SM_Wall_Pillar` | 16 |
| `SM_ScafSet_Wood03` | 16 |
| `SM_Window_01` | 9 |
| `SM_Pointy_wood` | 9 |
| `SM_Armour_A_Soi_SimpleArmor_Gloves8` | 8 |
| 其余 21 种 | 各 1~7 |

受影响的实例名（可直接在 Hierarchy 搜）：`SM_Blockade_Pillar1` ~ `Pillar13`、`SM_Blockade_Top1` ~ `Top4`、`Wall22` ~ `Wall33`、`SM_ScafSet_Wood090`、`ScafSet_Wood116 (1)` 等。

### 3.2 这到底会不会出问题

`BoxCollider does not support negative scale or size` 这句警告的意思是：Unity **把负的尺寸取了绝对值**，所以碰撞盒还在，只是**尺寸/位置可能和模型对不上**。

- 对**中心对称的方块**（墙、柱、砖）：取绝对值后基本没影响
- 对**非对称的东西**（拱门、楼梯、斜插的拒马）：可能偏，表现为"玩家穿过墙体的一部分"或"撞到空气"

### 3.3 怎么找到它们

两个办法：

1. **手动核对**：Hierarchy 里搜 `SM_Blockade_Pillar`，逐个点开看 Inspector 的 **Transform → Scale** 有没有负数
2. **用脚本列清单**（我写好暂存在 `.workbuddy/staging/`，你要用就拷进 `Assets/Editor/`）：
   - `NegativeScaleReporter.cs` —— 菜单 `Tools/城堡工具/列出负缩放物体`，把全部负缩放物体的**完整路径 + 具体哪一轴**打到 Console，点一下就跳转到该物体

### 3.4 处理选项

| 选项 | 怎么操作 | 代价 | 建议 |
|---|---|---|---|
| **A 先不动** | 什么都不做 | 保留 288 条警告 | ⭐ **先选这个** |
| B 逐个修正 | 把负的那一轴 scale 改回正数，同时把 Rotation Y 加/减 180°，视觉保持不变 | 要改 217 个实例，工作量大 | 只修实测出问题的 |
| C 换 convex MeshCollider | 选中实例 → 删 `BoxCollider` → `Add Component` → `Mesh Collider` → 勾 `Convex` | 支持负缩放、形状更贴合；但凸包会把凹形填平，碰撞体数量多时性能略差 | 个别疑难物件用 |
| D 永久接受 | 什么都不做 | 无 | 若 Play 实测无异常就不用管 |

⭐ **推荐流程：选 A → 进 Play 用玩家在城堡里跑一圈（重点测墙边、门洞、楼梯）→ 只修真的穿模/卡住的地方。**

理由：警告本身**不影响功能**，真正的判据是"玩家能不能正常走"。先修 217 个实例属于过度工程。

---

## 4. 烘焙 NavMesh

### 4.1 建烘焙负责人

1. Hierarchy 空白处**右键 → `Create Empty`**
2. 改名 `NavMeshRoot`
3. 选中它 → Inspector → **`Add Component`** → 搜索 **`Nav Mesh Surface`** → 添加

> ⭐ 这个对象**自己不用放任何东西**，它只是个"挂脚本的容器"。建议拖到 `Terrain` 下面方便管理。

### 4.2 填参数

⚠️ **最容易搞混的地方：体型参数不在这个组件上。** 组件上只填下面这些：

| 字段 | 填什么 | 为什么 |
|---|---|---|
| **Agent Type** | `Humanoid` | 工程里唯一的 Agent Type |
| **Collect Objects** | `All` | 最省事 |
| **Include Layers** | 勾 `Environment`（**只勾这一个**） | 决定哪些层参与烘焙 |
| **Use Geometry** | `Physics Colliders` | 和玩家实际撞到的边界完全一致 |
| **Default Area** | `Walkable` | |
| **Voxel Size** | `0.13333334` | 旧场景的原值，保持体素密度一致 |

Agent Type 的体型参数**已经就绪，不用改**（我实测过 `ProjectSettings/NavMeshAreas.asset`）：

| 参数 | 值 |
|---|---|
| Radius | `0.4` |
| Height | `2` |
| Slope | `45` |
| Step Height（Climb） | `0.75` |
| Cell Size | `0.16666667` |

敌人 `Monster1.prefab` 的 `NavMeshAgent` 是 `agentTypeID 0` / `radius 0.4` —— **和上面这套完全对得上**，所以不用动。
（要改 Agent Type 就去菜单 `Window → AI → Navigation` → `Agents` 页。）

> ⚠️ 如果以后加了门，烘焙前**必须临时禁用门的 `NavMeshObstacle` 的 Carving** —— 实测 Carving 挖出的洞会被**永久写进烘焙数据**，开门也走不通。详见 [NAVMESH-GUIDE.md](NAVMESH-GUIDE.md) §5。现在地图里没有门，可跳过。

### 4.3 点 Bake

在 `NavMeshSurface` 组件上点 **`Bake`** 按钮。

**成功的标志**：Scene 视图里地面浮现**一层蓝色半透明网格**。

看不到蓝色网格 → 菜单 `Window → AI → Navigation` → 勾上 `Show NavMesh`。

### 4.4 怎么判断烘得好不好

1. **看面积**：蓝色网格应该覆盖地面、台阶、桥面，并且**停下来**在墙边（墙体内侧应该有一圈空白的"退让"——那是 Agent Radius 0.4 的内缩，是正常的）
2. **看有没有爬墙**：如果网格爬到了垂直墙面上，说明那面墙的碰撞体不对
3. **看有没有悬空薄片**：如果空中出现孤立的蓝色小块，多半是某个装饰物的碰撞体被烘进去了 → 回到 §2.3 把它改回 Default 再烘

### 4.5 冒烟测试（别跳过）

1. 新建一个 `Cube`，随便放在地图一角
2. `Add Component` → `Nav Mesh Agent`
3. 新建脚本 `NavTest.cs`：

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

4. 进 Play → 这个 Cube 应该**绕开墙和柱子**跑向 Player

---

## 5. 常见问题速查

| 症状 | 原因 | 解法 |
|---|---|---|
| Bake 完一片空白 | Include Layers 没勾 Environment，或物体还是 Default 层 | 回到 §2 检查层，重新 Bake |
| 蓝色网格只覆盖一小块 | `Collect Objects` 不是 All，或某片区域的地面没在 Environment 层 | 改 `All`；用 `l:Environment` 检查 |
| 敌人卡在门口 / 关着的门也能穿 | 门的 Carving 在烘焙时是开着的 | 烘前禁用 Carving，重烘 |
| 敌人走进墙里 | 墙的碰撞体不是实体（或 Agent Radius 比实际体型小） | 检查墙有没有 Collider；Radius 保持在 0.4 |
| 空中出现孤立蓝色薄片 | 装饰物的碰撞体被烘进去了 | 搜到那个物体 → 层改回 Default → 重烘 |
| Console 报 `SetDestination can only be called on an active agent that has been placed on a NavMesh` | Agent 不在网格上 | 确认烘过了；`EnemyLocomotion.SyncWithNavMesh()` 会自动 `Warp` 吸附 |
| 开着 Play 模式改了参数 | 运行时不生效 | **退出 Play 再 Bake** |

---

## 6. 做完之后

- 回报我：**蓝色网格有没有出来**、`l:Environment` 里还剩什么、负缩放的 288 条警告有没有变化
- 我这边接着做：放置 `EncounterZone`、改 `GameManager` 的胜利条件（总清单的阶段 6.2 / 7）
