# 手 K 动画指南（HAND-KEYING GUIDE）

> **这份文档回答：如果我要自己手动 K 动画，该怎么做？**
> 面向"会一点 Unity、没做过动画"的人，重点是**本项目的硬约束**和**最容易翻车的环节**。
>
> 最后更新：2026-10-04
> 相关：`ART-ASSETS.md`（缺哪些资源）、`ART-PIPELINE.md`（资源从哪来）、`GDD.md` §5.4（动画方案）

---

## 0. 先看这个：你真正需要手 K 的只有 2 个

> ⚠️ **在学动画之前先看这节。** 我把 `TKDstyle_AnimSet` 全库翻了一遍（实测），结论是**大部分需求都能白拿或改造**。

### 实测发现：库里藏着 35 个动作，你只用了 8 个

`TKDstyle_AnimSet/Animation/` 下有 **Humanoid 与 Generic 两套，各 69 个 FBX = 35 个唯一动作**。其中 Humanoid 那套（`animationType: 3`，**可重定向**）包含：

```
combo_01, combo_01_1~4, combo_02, combo_02_1~3     ← 两套连段（combo_02 你完全没用！）
idle
kick_01~08, kick_03_1, kick_03_2, kick_04_1~3,
kick_05_1, kick_05_2, kick_06_1~3,
kick_07_1, kick_07_2, kick_air                       ← 约 20 个跆拳道腿法
versus_01_1, versus_01_2, versus_02_1, versus_02_2   ← 对打动作
```

⭐ **两个重要发现**：

1. **`combo_02` 系列基本零引用**（实测：4 个 GUID 里 3 个「被引用 0 处」）——**一整套第二连段完全闲置**。
2. **每个动作都有 `_inplace` 版本**（34 个）—— 这是**去掉根位移**的版本，正是游戏内播放需要的。

### 需求分类：复用 / 改造 / 必须手 K

| 动画 | 来源 | 结论 |
|---|---|---|
| 剑兵 Idle / Run | `idle` / `Standard Run` | ✅ 直接用 |
| 剑兵 **Attack** | `combo_01_1`（已在 `EnemyAC` 里）+ 可从 20 多个 kick / `combo_02` 里挑，**还能换花样** | ✅ 直接用，且有富余 |
| 剑兵 Hit | `Stomach Hit` | ✅ 直接用 |
| 剑兵 Death | `Sword And Shield Death` | ✅ 直接用 |
| 弓兵 Idle / Run / Hit / Death | 同上 | ✅ 直接用 |
| 玩家 剑·插地 | 改 `Elbow Uppercut Combo`（闲置）或 `combo_02_*` | 🟡 可改造 |
| 玩家 长枪·投掷 | `Upward Thrust`（闲置） | 🟡 可改造 |
| 玩家 长枪·召回 | **投掷倒放**（Unity 里可直接改速度方向） | 🟡 可改造 |
| **弓兵 Aim** | 库里没有拉弓动作 | 🔴 **需手 K 或 Mixamo** |
| **弓兵 Shoot** | 同上 | 🔴 **需手 K 或 Mixamo** |
| Boss 招式 | 库里大概率没有 | 🔴 需手 K（⚠️ 招式表未定，量未知） |

**→ 结论：真正"必须手 K"的只有弓兵的 `Aim` / `Shoot` 两个**（且 Mixamo 上有现成的拉弓射击动画，可以先去找）。

**所以建议的顺序是**：先动手复用/改造跑通玩法 → 只在确实不够用时才手 K → 手 K 优先补"库里完全没有"的（弓兵瞄准射击）。

---

## 1. 工具选型：三条路，按修改量选

| 方案 | 适合 | 优点 | 缺点 |
|---|---|---|---|
| **A. 直接改现有动画**（Unity Animation 窗口） | **微调**（改节奏、改某个姿势、去根位移） | ⚠️ **不用装任何新软件**，改完立刻预览 | 全身上下手 K 很痛苦，曲线编辑弱，无洋葱皮 |
| **B. Mixamo 下载后改** | 需要"大致对"的动作 | 质量稳定、免费 | 需登录下载；风格未必贴合 |
| **C. Blender 从零 K** | 库里完全没有的动作（弓兵 Aim/Shoot、Boss 招式） | 完整控制权，专业工具 | 需学 Blender；⚠️ **本机未安装**（实测） |

**我的建议**：

1. **先试方案 A**（改 `Upward Thrust` → 投掷）。**零环境成本**，改完能立刻在编辑器里看效果。很多"感觉差一点"的动画，改改时长和关键姿势就够了。
2. 弓兵 `Aim`/`Shoot` **先去 Mixamo 搜**（"draw bow" / "shoot arrow" 是常见动作），大概率比手 K 的好。
3. **只在 1、2 都解决不了时才上 Blender。**

⚠️ **Blender 现状（实测）**：`which blender` 无结果、`C:\Program Files\Blender Foundation` 不存在 → **未安装**。
要装建议 **Blender 4.x LTS**，装完我可以命令行驱动它批处理（`blender --background --python script.py`）。

---

## 2. ⚠️ 本项目 4 个硬约束（不满足就是白做）

> 这节比"怎么 K"更重要。**动画做完不能用，通常不是因为 K 得不好，而是这四条没对齐。**

### 2.1 ① 必须是 Humanoid ⚠️ 最重要

**你项目的核心红利是"Humanoid 重定向"**——实测 `Animator.isHuman = true`，`Y Bot.fbx` + `idleAvatar.asset` 已配好。

- ✅ **保持 Humanoid**：新角色的模型套用现有动画，**动画成本接近 0**
- ❌ **做成 Generic**：**无法重定向**，每个角色都得单独做一整套动作

🔧 **做法**：做完动画导入 Unity 后，`Rig` 页 → `Animation Type = Humanoid` → 点 `Configure` 确认骨骼映射成功（**能看到绿色的人形骨骼图**才算成）。

> 如果直接用 `Y Bot` 的骨骼 K 动画，天然就是 Humanoid；用别的模型则必须确认能 Configure 成功。

### 2.2 ② 动画事件不能漏 ⚠️ 最容易忘

**你项目里伤害是靠动画事件触发的**（实测）：

| 动画 | 事件名 | 接收方 |
|---|---|---|
| `combo_01_1~4`、`Elbow Uppercut Combo`、`Upward Thrust` | **`OnAttackHit`** | `PlayerAttack` |
| `enemy/combo_01_1` | **`TickAttack`** | `EnemyMeleeAI` |

❌ **如果你手 K 了一个新攻击动画但没加事件 → 挥砍有动作、有特效，但「打不掉血」。**
（因为 `DoMeleeHit()` 只在事件里被调用。）

🔧 **做法**：在 Unity 的 Animation 窗口。选中动画剪辑 → `Events` 轨道 → 在**命中那一帧**右键 `Add Event` → 函数名填 `OnAttackHit`。

**怎么定"命中帧"**：就是 `GDD.md` §8 里 hitbox 开启的那一刻——**武器接触目标的那一帧**，通常在挥砍到位、动作最快的那一瞬。

### 2.3 ③ 根位移要明确（Root Motion）

**现状（实测）**：`PlayerFSM` 在进入 `Death` 状态时设了 `_anim.applyRootMotion = true`，**其他状态没有**。

所以每段动画要想清楚：**这段位移由动画带（root motion），还是由代码带？**

| 动画类型 | 建议 | 理由 |
|---|---|---|
| Death | **带根位移** | 现有代码就是 `applyRootMotion = true`，死亡倒地移位更自然 |
| 攻击 | **不带（in-place）** | 位移由 `PlayerAttack` / `PlayerDash` 代码控制，动画再带会打架 |
| Idle / Run | 不带 | 由 `PlayerMotor` 驱动 |
| 敌人移动 | 不带 | ⚠️ M3 后由 `NavMeshAgent` 驱动，动画带位移会**抖动/瞬移** |

✅ **现成红利**：TKD 库里每个动作都有 **`_inplace` 版本**（34 个），**那就是去掉根位移的版本**，直接用能省掉调整工作。

### 2.4 ④ 时长要与代码参数对齐

代码里有**写死的时间常量**，动画长度不匹配会出现"动作播完了判定还没结束"这类怪事：

| 代码位置 | 参数 | 影响 |
|---|---|---|
| `PlayerConfig.comboWindow = 1s` | 连段窗口 | 动画太长会接不上下一段 |
| `EnemyAIConfig.windupTime / recoverTime` | 敌人前摇 / 后摇 | 动画与状态机计时对不上 |
| `GDD.md` §6.3 弓兵 | 瞄准前摇 **0.5s**、冷却 2.2s | `Aim` 动画长度应≈0.5s |
| `GDD.md` §7.3 Boss | **前摇必须做足**（Boss 不会被打断，前摇是玩家唯一反应窗口） | 决定 Boss 战"打不打得明白" |

🔧 **建议**：**先把这张表里的时长定下来，再动手 K**。否则 K 完了发现节奏对不上，要重来。

---

## 3. 手 K 实操：三阶段

> 新手最大的错误是**第一个关键帧就开始雕细节、调曲线**。正确做法是先"搭骨架"。

### 阶段 1：Blocking（关键姿势）—— 只摆姿势，不调曲线

以**一次挥砍**为例，只需要 **4 个关键姿势**：

| # | 姿势 | 作用 | 本项目对应 |
|---|---|---|---|
| 1 | **起手 / 预备**（武器举到反方向） | 蓄势，制造预期 | **前摇** = 玩家反应窗口 |
| 2 | **发力**（挥出去，动作最快的一瞬） | 蓄力释放 | — |
| 3 | **命中**（武器到达目标位置） | 判定发生的时刻 | ⚠️ **这里加 `OnAttackHit` 事件** |
| 4 | **收招**（回到可行动姿态） | 恢复 | **后摇** |

⚠️ **关键原则**：
- **姿势要夸张**，宁过火后再收敛（"极限姿势"）
- **一帧一个姿势**，不要想着"平滑过渡"，那是下一阶段的事
- **命中帧附近的时间要最短**——挥砍"唰"一下才有力量感

**先只做这 4 帧，播放看动作说得通不通。** 不通就改姿势，别急着补中间帧。

### 阶段 2：Breakdown（中间帧）—— 补关键过渡

在 Blocking 的基础上补中间帧，主要解决一件事：**运动弧线**。

- 挥砍要走**弧线**，不是直线（12 原则里的 **Arcs**）
- 武器/长发/衣摆要有**跟随与重叠**（Follow Through）：身体停了，武器还往前甩一点再回来

⚠️ 这个阶段**还是不要动曲线插值**，只加帧。

### 阶段 3：Spline（曲线精修）—— 调节奏

**到这一步才动曲线**。重点调**时间**而非空间：

- 发力前**慢**、命中瞬间**快**、收招**缓**（Timing 原则）
- 曲线的缓入缓出（Ease In / Out）决定"轻重感"

**12 原则里本项目最用得上的 5 条**（按重要性）：

| 原则 | 在你这儿怎么用 |
|---|---|
| **Anticipation 预备动作** | 攻击前摇——**玩家靠它获得反应时间**，Boss 尤其重要（不会被打断） |
| **Timing 时间控制** | 命中帧最快，前后慢。**手感主要来自这里，不是姿势** |
| **Follow Through 跟随** | 收招时武器/衣摆继续动，避免"突然定格" |
| **Arcs 弧线** | 挥砍走弧线，直线看着很假 |
| **Squash & Stretch 挤压拉伸** | 写实向可省；风格化才有用 |

### ⚠️ 循环动画（Idle）特别注意

`Idle` 要**首尾帧完全一致**，否则会"跳一下"。在 Unity 里勾上 `Loop Time` + `Loop Pose`，并确认：
- 第 1 帧 == 最后一帧的姿势
- 首尾的**速度**也连续（不然会有"顿一下"的感觉）

---

## 4. 导出 / 导入（最容易翻车的一段）

### 4.1 Blender → FBX 导出设置

| 设置项 | 值 | 说明 |
|---|---|---|
| `Limit to` | `Selected Objects` | 只导出骨架（+ 需要的网格），别把整个场景带走 |
| `Object Types` | `Armature` | 只导骨架即得动画 |
| **`Bake Animation`** | ✅ **必须勾** | 不勾会丢帧/丢骨骼动画 |
| **`NLA Strips`** | ❌ **不要勾** | 勾了会把 NLA 片段烤成一条奇怪的长动画 |
| `Sampling Rate` | `1`（30fps 下即逐帧） | 保险起见用 1，避免漏关键帧 |
| `Apply Scalings` | `FBX All` | 避免缩放错乱 |
| `Primary/Secondary Bone Axis` | **保持默认** | ⚠️ 改它会让 Humanoid 映射失败 |
| `All Actions` | ❌ 一般不要 | 会把所有动作堆进一个文件 |

### 4.2 Unity 导入设置

选中 FBX → Inspector：

**`Rig` 页**
- `Animation Type` = **`Humanoid`**
- `Avatar Definition` = **`Copy From Other Avatar`** → 选 `idleAvatar.asset`
  （或 `Create From This Model` 新建；**复用现有 Avatar 更省事**）
- 点 **`Configure`** 确认骨骼映射（**看到绿色人形骨骼图**）

**`Animation` 页**
- `Loop Time`：Idle / Run ✅ 勾；Attack / Hit / Death ❌ 不勾
- `Loop Pose`：仅循环动画勾
- `Root Transform Position (Y)` → 视是否需要根位移（见 §2.3）
  - 要根位移 → `Bake Into Pose` **取消勾选**
  - 不要 → **勾上** `Bake Into Pose`
- **`Events`** → ⚠️ **加动画事件**（见 §2.2，最容易漏）

### 4.3 接入 Controller

1. 把剪辑拖进 `PlayerAC.controller` / `EnemyAC.controller`
2. 连线、设 Trigger（照抄现有状态的写法）
3. ⚠️ **`PlayerAttack` / `EnemyMeleeAI` 的方法名必须与事件名完全一致**（`OnAttackHit` / `TickAttack`），大小写敏感
4. 若用了 `AttackStateBehaviour`（挂在攻击状态上）→ **新攻击状态也要挂**，否则 `isAttacking` 计数不对，状态机会卡住

---

## 5. 两个"省事"技巧

### 5.1 倒放 = 免费的第二段动画 ⭐

**长枪·召回** 不需要单独 K：在 Unity 里选中 `SpearThrow` 剪辑，设 `Speed = -1` 即得倒放。

（若要更自然，可复制一份剪辑再倒放，避免影响正放。）

### 5.2 改造现有动画比从零 K 快 10 倍

**在 Unity Animation 窗口里直接改**（不用 Blender）：

1. 在 Project 里选中要改的 `.anim`（如 `Upward Thrust`）→ **Ctrl+D 复制**（⚠️ 别改原文件）
2. 双击打开 → 直接拖时间轴上的关键帧改节奏
3. 或选中骨骼在 Scene 里摆姿势 → 点 `Add Key`

**典型改造**：
- **拉长/压缩整体时长**（全选关键帧拖）
- **改起手姿势**（前几帧摆新姿势）
- **去掉根位移**（删掉 Root 的位移曲线）

---

## 6. 常见坑速查表

| 症状 | 原因 | 解决 |
|---|---|---|
| **有动作但打不掉血** | ⚠️ 动画事件漏了 | 在命中帧加 `OnAttackHit` / `TickAttack` |
| **角色在场景里抽搐 / 瞬移** | 动画带根位移 + 代码/Agent 也在移动 | 用 `_inplace` 版本，或勾 `Bake Into Pose` |
| **导入后是粉色的 / 不动** | 骨骼被认成 Generic | `Rig` 页改 `Humanoid` 并 `Configure` |
| **Idle 循环时"跳一下"** | 首尾帧不一致 | 首帧复制到末帧；勾 `Loop Time` + `Loop Pose` |
| **状态机卡在攻击状态出不来** | `AttackStateBehaviour` 没挂到新状态上 | 新攻击状态也要挂该 Behaviour |
| **Blender 导出后在 Unity 里是 T-pose** | `Bake Animation` 没勾 | 重新导出，勾上它 |
| **动画比手感"慢半拍"** | 与 `comboWindow` / `windupTime` 对不上 | 回 §2.4 对齐时间常量 |
| **模型比例不对 / 悬空** | 导出时 `Apply Scalings` 选错 | 用 `FBX All` |

---

## 7. 建议的动手顺序

| 顺序 | 做什么 | 为什么 |
|---|---|---|
| **1** | **先不装 Blender**，用 Unity Animation 窗口改 `Upward Thrust` 试作投掷 | 零环境成本，先验证"能不能接受现成动作" |
| **2** | 把 `combo_02` / kick 系列**分配给剑兵**，看观感够不够 | 库里有 20 多个闲置动作，先用完再说 |
| **3** | 弓兵 `Aim` / `Shoot` **先去 Mixamo 搜** | 比手 K 快，质量更稳 |
| **4** | 上面都解决不了 → 装 **Blender 4.x LTS** 手 K | 只补"确实没有"的动作 |
| **5** | ⚠️ 每段动画做完**立刻检查 4 条硬约束**（§2） | 越早发现越省事 |

**动手前请先确认 §2.4 那张时间表**——动画时长和代码常量对不上，K 得再好也要返工。

---

## 8. 参考

| 主题 | 链接 |
|---|---|
| Unity 动画事件 | https://docs.unity3d.com/Manual/script-AnimationWindowEvent.html |
| Humanoid Avatar 配置 | https://docs.unity3d.com/Manual/ConfiguringtheAvatar.html |
| 动画导入设置（Rig / Animation 页） | https://docs.unity3d.com/Manual/class-AnimationClip.html |
| 12 动画原则（速查） | https://www.animationmentor.com/blog/12-principles-of-animation/ |
| Blender 入门 | https://docs.blender.org/manual/zh-hans/latest/animation/index.html |
| Mixamo（免费动画库） | https://www.mixamo.com/ |

> 💡 本文里所有"实测"结论（库里有 35 个动作、`combo_02` 零引用、`animationType: 3`、Blender 未安装、动画事件名）都是在**你这个工程里实际核对过的**。若与官方文档冲突，以本工程实测为准。
