# UI · 怪物血条制作指南

> 2026-10-15 落地：**小怪头顶悬浮血条** + **Boss 屏幕固定血条**。
> 本文是从零复现的完整做法 —— 原理、每一步点哪里、每个参数什么含义、以及实测踩到的坑。
> 相关文件见文末「改动清单」。

---

## 0. 先想清楚：为什么是两套做法

同样是「显示血量」，两种血条在 Unity 里是**完全不同的东西**：

| | 小怪头顶血条 | Boss 屏幕血条 |
|---|---|---|
| Canvas 渲染模式 | **World Space** | **Screen Space - Overlay** |
| 它住在哪 | 3D 世界里，是敌人的子物体 | Canvas 下，2D UI 图层 |
| 位置怎么定 | 敌人走它就走（免费）；但要自己转头朝向相机 | 锚点钉死在屏幕上，永远不动 |
| 大小会变吗 | 会 —— 离得远就小、离得近就大 | 不会 —— 恒定像素大小 |
| 会被 3D 物体挡住吗 | **会**（墙、柱子能遮住它） | 不会 |
| 适合谁 | 一堆小怪 | 单个大 Boss |

一句话：**世界里的东西放 World Space Canvas，屏幕上的东西放 Overlay Canvas**。
把小怪血条做成 Overlay 也不是不行，但那要每帧 `WorldToScreenPoint` 换算 + 手动管相机，纯属自找麻烦。

---

## 1. 数据层：先让血量可以被订阅

### 1.1 现有结构

`Assets/_Game/Scripts/Gameplay/Enemy/EnemyHealth.cs` 是**小怪和 Boss 共用**的血量组件，
它原本已经有两个事件：

- `static event Action<EnemyHealth> Died` —— 死亡（房间计数用）
- `event Action<EnemyHealth, int> Damaged` —— 受击，参数是「自身 + 本次伤害」

### 1.2 为什么还要新增 `HpChanged`

`Damaged` 顶替不了，原因有两个：

1. **它只在挨打时广播**。可血条还需要在「敌人被启用 / 满血重置」时同步一次 —— 敌人被
   `SetActive` 复用时（房间重开、对象池），血条必须跟着回到满格，否则会停在上一轮的空血。
2. **它给的是「伤害值」不是「当前血量」**。UI 每次都要自己算 `CurHp / MaxHp`，多一层容易错。

所以新增：

```csharp
/// <summary>血量变化通知。参数 = (当前血量, 血量上限)。</summary>
public event Action<int, int> HpChanged;

private void RaiseHpChanged()
{
    if (HpChanged != null) HpChanged(CurHp, MaxHp);
}
```

调用点只有两处：

```csharp
private void OnEnable()
{
    MaxHp = _config.hp;
    CurHp = MaxHp;
    // ... 原有的重置逻辑 ...
    RaiseHpChanged();          // ← 新增：复用复活时血条跟着回满
}

public void TakeDamage(int damage)
{
    if (CurHp <= 0) return;
    CurHp -= damage;
    if (CurHp < 0) CurHp = 0;

    RaiseHpChanged();          // ← 新增：先刷血条，再走受击 / 死亡流程
    if (Damaged != null) Damaged(this, damage);
    if (CurHp <= 0) Die();
}
```

> ✅ **不破坏任何现有代码** —— 只是多了一个事件，老订阅者照常工作。

### 1.3 血量数值（实测）

| 对象 | 配置资产 | hp | 触发事件的那个组件 |
|---|---|---|---|
| Monster1（近战怨灵） | `EnemyAIConfig.asset` | 30 | `EnemyMeleeAI` |
| Monster2（远程怨灵） | `EnemyCasterConfig.asset` | 30 | `EnemyCaster` |
| Boss_Crichton | `BossConfig.asset` | 600 | `BossController` |

---

## 2. 小怪头顶悬浮血条（World Space）

### 2.1 原理：三件事

1. **跟随** —— 血条是敌人的子物体，敌人走它就跟着走，**不需要任何代码**。
2. **朝向相机（Billboard）** —— 敌人自己会转身，子物体会跟着转，血条会侧过去看不见。
   所以每帧把血条的**世界旋转**直接抄成相机的世界旋转：

   ```csharp
   transform.rotation = camera.transform.rotation;   // LateUpdate 里
   ```

   用 `rotation`（世界）而不是 `localRotation`（本地）是关键 —— 世界旋转会覆盖父级带来的旋转。

   ⚠️ 必须放在 **LateUpdate**，不是 Update：相机（`CameraFollow`）在 Update/LateUpdate 里移动，
   放 LateUpdate 才能保证用的是相机**这一帧最终**的朝向，否则血条会抖。

3. **填充** —— 改填充块的 `anchorMax.x`。这跟项目里玩家血条 `Scripts/UI/HPBar.cs` 是**同一套手法**：

   ```csharp
   _fill.anchorMin = new Vector2(0f, 0f);
   _fill.anchorMax = new Vector2(fraction, 1f);   // fraction = 血量比例
   _fill.offsetMin = Vector2.zero;
   _fill.offsetMax = Vector2.zero;
   ```

   为什么用锚点拉伸而不是 `localScale.x`？因为拉伸不会被父级的缩放污染，也不会有「缩到 0 时
   贴图被压扁」的问题，而且几行代码就够。

### 2.2 预制体层级

```
Monster1                          ← 敌人预制体根（挂 EnemyHealth）
├── wraith_body_LOD0 ...
├── Hitbox_Attack
└── HPBar                         ← Canvas (World Space) + EnemyHealthBar 脚本
    └── Back                      ← Image（深色底），尺寸决定血条大小
        └── Fill                  ← Image（红色），脚本会拉伸它的锚点
```

### 2.3 手工搭建步骤（Unity 里点哪里）

**先双击打开** `Assets/_Game/Prefabs/Enemy/Monster1.prefab` 进入预制体编辑模式。

**第 1 步：建 Canvas 根**

1. 在 Hierarchy 里右键预制体根 `Monster1` → `Create Empty`，命名 **`HPBar`**。
2. 选中 `HPBar`，Inspector 右上 `Add Component` → 搜 **Canvas** → 添加。
3. 把 `Canvas` 的 **Render Mode 改成 `World Space`**。
4. 选中 `HPBar` 的 `RectTransform`，填：

   | 字段 | 值 | 为什么 |
   |---|---|---|
   | Pos X / Y / Z | `0` / `2.4` / `0` | 小怪模型头顶约 2.23 m（实测），抬到 2.4 留余量 |
   | Width / Height | `90` / `12` | 像素尺寸，配合下面的 Scale |
   | Scale X / Y / Z | `0.01` / `0.01` / `0.01` | **最关键的一步**，见下方说明 |

   > ⭐ **World Space Canvas 的物理大小 = 像素尺寸 × Scale**。
   > `90 × 0.01 = 0.9 米宽`，`12 × 0.01 = 0.12 米高`。小怪肩宽 1.7 m、身高 2.2 m，0.9 m 宽的血条正合适。
   > 如果 Scale 忘了改成 0.01（默认 1），血条会是 **90 米宽**，把整个场景糊住。

**第 2 步：建底图 Back**

1. 右键 Hierarchy 里的 `HPBar` → `UI` → **`Image`**，命名 **`Back`**。（Unity 会自动建一个 Canvas 子物体）
2. 把新建出来的 `Back` 拖到 `HPBar` 下（如果不在的话），**删掉可能自动创建的 `Canvas` 子物体**。
3. 选中 `Back`，`RectTransform` 点左上角那个方形锚点预设，按住 `Alt + Shift` 选**右下角那个「stretch / 全铺满」**，
   这样 Left / Top / Right / Bottom 全变 0，就铺满父物体了。
4. `Image` 组件 → `Color` 设成深色半透明：`R 15, G 13, B 15, A 230`。

**第 3 步：建填充 Fill**

1. 右键 `Back` → `UI` → `Image`，命名 **`Fill`**。
2. 同样用 `Alt + Shift` 选**全铺满**锚点（让 Left/Top/Right/Bottom 全 0）。
3. `Image` → `Color` 设成红色 `R 252, G 18, B 15, A 255`（与玩家血条 `FD110F` 一致）。

**第 4 步：挂脚本并接线**

1. 选回 `HPBar` → `Add Component` → 搜 **`EnemyHealthBar`**。
2. 把字段拖上：

   | 字段 | 拖什么 |
   |---|---|
   | `_health` | Hierarchy 里的 `Monster1` 根（它身上有 `EnemyHealth`） |
   | `_fill` | `HPBar/Back/Fill` |
   | `_camera` | 留空即可（自动用 `Camera.main`） |

3. 其余开关保持默认：`_faceCamera` ✔、`_visibleDistance` = 30、`_hideWhenFull` ✘、`_hideOnDeath` ✔。

**第 5 步：保存**

`Ctrl + S`，或右上角返回箭头选 `Save`。

**第 6 步：复制到 Monster2**

`Monster2.prefab` 重复一遍（`_health` 拖 `Monster2` 自己）。
或者更省事：在 Hierarchy 里选中 `HPBar` → `Ctrl + C` → 打开 `Monster2.prefab` → `Ctrl + V`，
然后**只需要把 `_health` 重新拖一下**（复制过来的引用还指着 Monster1）。

> ⚠️ 场景 `Castle.unity` 里的 `Monster1` / `Monster2` 是预制体实例，**改预制体会自动同步**，
> 不用去场景里再搭一遍。

### 2.4 脚本关键点（`Scripts/UI/EnemyHealthBar.cs`）

**① 订阅时机 —— 为什么 OnEnable 订阅了、Start 还要再同步一次**

```csharp
private void Start()
{
    // ⚠️ Unity 的执行顺序是「父物体的 OnEnable 早于子物体」。
    //    血条是子物体，而 EnemyHealth 挂在**父物体**上 ——
    //    血条 OnEnable 时，EnemyHealth 可能还没跑自己的 OnEnable（还没把 CurHp 重置成满血），
    //    读到的会是 0。Start 在所有 Awake/OnEnable 之后执行，这里再同步一次就稳了。
    _camera = Camera.main;
    SyncNow();
}
```

这是**血条类组件最经典的坑**：数据源在父物体上时，子物体永远读不到「已经初始化好的」值。

**② 距离剔除必须在 LateUpdate 里无条件算**

```csharp
private void LateUpdate()
{
    if (_faceCamera) { /* ... 抄相机旋转 ... */ }

    // 平滑追赶
    // ...

    // ⚠️ 必须**无条件**每帧算，不能写成 if (_visibleDistance > 0f) ApplyVisibility();
    //    玩家走远时血量没有任何变化，没有事件会来通知我们，只能每帧自己看。
    ApplyVisibility();
}
```

> 🐞 这个 bug 是实测踩出来的：最初写成 `if (_visibleDistance > 0f) ApplyVisibility();`，
> 结果一旦把 `_visibleDistance` 改成 0（关掉距离限制），血条就**永远停在「上一次被剔除」的隐藏状态**
> 再也回不来 —— 因为条件不成立，`ApplyVisibility` 根本不会被调用。

**③ 死亡时立刻隐藏，而不是等物体被销毁**

敌人死后 `EnemyHealth` 会延迟 2.4 秒才 `SetActive(false)`（留时间给死亡动画）。
这 2.4 秒里血条不能还挂在头顶显示一格空血，所以：

```csharp
_dead = max > 0 && cur <= 0;     // 在 OnHpChanged 里更新
// ApplyVisibility 里：
if (_hideOnDeath && _dead) visible = false;
```

### 2.5 参数含义

| 参数 | 默认 | 说明 |
|---|---|---|
| `_faceCamera` | ✔ | 是否始终正对相机。**3D 游戏必须开** |
| `_visibleDistance` | 30 | 超过这个距离（米）隐藏血条。`0` = 不限制 |
| `_hideWhenFull` | ✘ | 满血时隐藏 —— 想要「挨打才冒血条」的效果就打开 |
| `_hideOnDeath` | ✔ | 死亡后立刻隐藏 |
| `_fillLerpSpeed` | 6 | 扣血时血条的追赶速度（比例/秒）。`0` = 一格一格硬跳 |

---

## 3. Boss 屏幕固定血条（Screen Space）

### 3.1 原理

Boss 血条就是一块普通的 UGUI 面板，钉在 Canvas 顶部居中。它跟小怪血条的**唯一共同点**
只有「数据来源一样」。它需要额外解决一个问题：**什么时候显示**。

答案：订阅 `BossController` 新加的战斗事件。

### 3.2 让 BossController 会「喊话」

在 `Scripts/Gameplay/Enemy/BossController.cs` 里新增：

```csharp
/// <summary>进入 / 退出战斗的通知（true = 进入战斗）。Boss 屏幕血条订阅它控制显隐。</summary>
public event Action<bool> CombatChanged;

/// <summary>当前是否处于战斗（不在 N_idle，且还没死）。</summary>
public bool InCombat
{
    get { return _fsm != null && !_fsm.Is(EState.NIdle) && !_fsm.Is(EState.Death); }
}

private void RaiseCombat(bool inCombat)
{
    if (CombatChanged != null) CombatChanged(inCombat);
}
```

三个广播点（都是现成的状态入口，不用新建逻辑）：

| 位置 | 广播 | 效果 |
|---|---|---|
| `EnterNIdle()` 末尾 | `RaiseCombat(false)` | 未进战斗 → 血条收起 |
| `EnterCombatIdle()` 末尾 | `RaiseCombat(true)` | 玩家进入战斗范围 → 血条出场 |
| `EnterDeath()` 里 | `RaiseCombat(false)` | 死亡 → 血条准备退场 |

> ⚠️ `EnterCombatIdle` **每次**进入都会广播 true（例如攻击完回到待机也会再发）。
> 订阅方要写成**幂等**的 —— 已经在显示就别再播一次动画。

### 3.3 层级设计：为什么脚本和 Panel 要分两层

```
Canvas
└── BossBar                    ← 空节点，挂 BossHealthBar 脚本，**永远 active**
    └── Panel                  ← 拖给 _panel，真正被 SetActive 显隐的是它
        ├── Name               ← TextMeshProUGUI
        ├── HpBack
        │   └── HpFill         ← 拖给 _hpFill
        └── PoiseBack
            └── PoiseFill      ← 拖给 _poiseFill
```

> ⚠️ **不要把脚本直接挂在 Panel 上、然后 `SetActive(false)` 隐藏它**。
> 那样会把脚本自己也停掉，`OnDisable` 里退订了事件，就**再也收不到**「Boss 进战斗」的通知，
> 血条永远显示不出来 —— 这是一个非常容易踩死循环的坑。

### 3.4 手工搭建步骤

1. 打开 `Assets/Scenes/Castle.unity`，选中 Hierarchy 里的 `Canvas`。
2. 右键 → `Create Empty`，命名 **`BossBar`**。
3. 选中 `BossBar`，`RectTransform` 用 `Alt + Shift` 选**全铺满**锚点（Left/Top/Right/Bottom = 0）。
4. 右键 `BossBar` → `Create Empty`，命名 **`Panel`**，然后设：

   | 字段 | 值 |
   |---|---|
   | Anchor（左上角方形锚点预设） | 选 **top-center** |
   | Pivot X / Y | `0.5` / `1` |
   | Pos X / Y | `0` / `-20` |
   | Width / Height | `900` / `64` |

5. `Panel` 下建三个子物体（右键 → `UI`）：

   **`Name`** —— `UI → Text - TextMeshPro`

   | 字段 | 值 |
   |---|---|
   | Anchor / Pivot | top-center / `0.5, 1` |
   | Pos / Size | `0, 0` / `900 × 30` |
   | Text | `克里克顿 CRICHTON` |
   | Font Asset | `LiberationSans SDF`（跟场景里其他 TMP 一致） |
   | Font Size | `30` |
   | Alignment | 水平 Center、垂直 Middle |

   **`HpBack`** —— `UI → Image`，Anchor/Pivot = top-center / `0.5, 1`，Pos `0, -32`，Size `900 × 22`
   **`PoiseBack`** —— `UI → Image`，Anchor/Pivot = top-center / `0.5, 1`，Pos `0, -58`，Size `900 × 8`

6. 再建两个填充：右键 `HpBack` → `UI → Image` 命名 **`HpFill`**（全铺满锚点）；
   右键 `PoiseBack` → `UI → Image` 命名 **`PoiseFill`**（全铺满锚点）。

7. 颜色：

   | 对象 | RGBA (0-255) |
   |---|---|
   | `HpBack` / `PoiseBack` | 15, 13, 15, 230（深色底） |
   | `HpFill` | 252, 18, 15, 255（红，与玩家血条同色） |
   | `PoiseFill` | 255, 199, 64, 255（金黄） |

8. 选回 `BossBar` → `Add Component` → **`BossHealthBar`**，接线：

   | 字段 | 拖什么 |
   |---|---|
   | `_boss` | 场景里的 `Boss_Crichton`（它身上有 `BossController`） |
   | `_health` | 同上那个对象（`EnemyHealth`） |
   | `_panel` | `BossBar/Panel` |
   | `_hpFill` | `BossBar/Panel/HpBack/HpFill` |
   | `_poiseFill` | `BossBar/Panel/PoiseBack/PoiseFill` |

9. `Ctrl + S` 保存场景。

> 💡 **布局冲突提示**：`Canvas` 下原有一个 `RoomHIntText`（房间名提示），锚点也在顶部居中。
> Boss 血条会跟它重叠，所以本次把 `RoomHIntText` 的 `Pos Y` 从 `-60` 改成了 **`-120`**。

### 3.5 脚本关键点（`Scripts/UI/BossHealthBar.cs`）

**① 开局要先自己拉一次初值**

事件只在**变化时**才发。Boss 血条 `Start` 时才订阅，此时各种初值事件早就发完了，所以要主动同步：

```csharp
OnHpChanged(_health.CurHp, _health.MaxHp);
OnPoiseChanged(_boss.Poise01, 1f);
_hpShown = _hpTarget;        // 初始化时直接就位，避免开局从满血"倒扣"一下
_poiseShown = _poiseTarget;
ApplyFills();

bool show = !_hideWhenNotInCombat || _boss.InCombat;
SetPanel(show);
```

**② 死亡退场用 `unscaledTime`**

```csharp
if (_health != null && _health.IsDead)
{
    _hideAtUnscaled = Time.unscaledTime + _deathHideDelay;
    return;
}
```

本工程有「命中顿帧」（HitStop 会把 `timeScale` 压到 0）。用 `Time.time` 计时的话，
顿帧期间倒计时会被无限拉长，血条该收却收不掉。`unscaledTime` 不受 `timeScale` 影响。

**③ 韧性条的方向**

`BossController.Poise01` = **已累计的削韧量占比**，所以条越长 = 越接近破韧。
想反过来显示「剩余韧性」（条越长越安全），把 `OnPoiseChanged` 里的比例改成 `1f - 那个值` 即可。

### 3.6 参数含义

| 参数 | 默认 | 说明 |
|---|---|---|
| `_hideWhenNotInCombat` | ✔ | 没进战斗时隐藏面板 |
| `_deathHideDelay` | 2 | 死后延迟几秒收起（让玩家看到打空的血条） |
| `_fillLerpSpeed` | 3 | 血条 / 韧性条的追赶速度，`0` = 瞬时 |

---

## 4. 怎么验证

**编不过 / 没反应时，先看 Unity Console**，本功能的常见报错只有两类：
`找不到 EnemyHealth`（引用没拖）和 `场景里没找到 BossController`（Boss 没激活或名字不对）。

### 4.1 小怪血条

1. 进 Play，把角色**走到小怪 30 米以内**（或把 Inspector 里 `_visibleDistance` 临时改成 `0`）。
2. 应该看到小怪头顶有一根红色横条。
3. 打它一下 → 血条按比例缩短。
4. 打死它 → 血条立刻消失（尸体还会留在原地播完死亡动画）。

### 4.2 Boss 血条

1. 进 Play，走到 Boss 所在区域（`Battle` 房间，Boss 在 `x ≈ 80`）。
2. **走进战斗范围（12 米）**那一刻，屏幕顶部中间会冒出 Boss 血条（含名字）。
3. 打 Boss → 血条下降；继续打 → 下面那根金色韧性条上涨，涨满触发破韧硬直。
4. 打死 Boss → 血条停留 2 秒后消失，同时结算面板弹出。

### 4.3 本次实测记录（2026-10-15）

| 检查项 | 实测结果 |
|---|---|
| 血条朝向 | 与相机夹角 **0.0°**（Billboard 正确） |
| 血条高度 | 条中心 Y = 3.02，模型顶 Y = 2.98（在头顶上方） |
| 小怪扣血 | 30 → 20 时 `Fill.anchorMax.x = 0.667` ✔ |
| 小怪死亡 | 0/30 时 `anchorMax.x = 0.000` 且 Canvas 已隐藏 ✔ |
| 距离剔除 | 相机距小怪 55 m 时隐藏、< 30 m 时显示 ✔ |
| Boss 未战斗 | `Panel.activeSelf = False` ✔ |
| Boss 进战斗 | `Panel.activeSelf = True` ✔ |
| Boss 血量 | 600 → 555 时 `HpFill.anchorMax.x = 0.925` ✔ |
| Boss 韧性 | 45 伤害 × 2 = 90 时 `PoiseFill.anchorMax.x = 0.900` ✔ |
| 新增报错 | 血条脚本自身 **0 条**（对照实验：禁用/启用各跑一次 Play 无差异） |

---

## 5. 调参速查

| 想改什么 | 改哪里 |
|---|---|
| 小怪血条太大 / 太小 | `HPBar` 的 `RectTransform.Width/Height`，或整体改 `Scale` |
| 小怪血条高一点 / 低一点 | `HPBar` 的 `Pos Y`（当前 2.4） |
| 血条更红 / 换个色 | `Fill` 的 `Image.Color` |
| 走远就看不见血条 | `EnemyHealthBar._visibleDistance`（当前 30 米） |
| 挨打才显示血条 | `EnemyHealthBar._hideWhenFull` 打勾 |
| Boss 血条位置 / 宽度 | `BossBar/Panel` 的 `Pos Y` / `Width` |
| Boss 名字文字 | `BossBar/Panel/Name` 的 TMP 文本 |
| Boss 血条常驻不隐藏 | `BossHealthBar._hideWhenNotInCombat` 取消打勾 |
| 韧性条改回「剩余韧性」 | `BossHealthBar.OnPoiseChanged` 里用 `1f - cur/max` |

---

## 6. 排错表

| 现象 | 原因 | 怎么办 |
|---|---|---|
| 血条大得离谱糊满屏幕 | World Space Canvas 的 `Scale` 还是 1 | 改成 `0.01` |
| 血条侧着 / 看不见正面 | Billboard 没生效 | 检查 `_camera` 有没有值；`_faceCamera` 是否打勾 |
| 血条一直在头顶不消失 | `_hideOnDeath` 没开 | 勾上；或检查 `_visibleDistance` 是不是 0 |
| 扣血了但血条不动 | `_fill` 没接线，或指向了 `Back` 而不是 `Fill` | 重新拖 `HPBar/Back/Fill` |
| 开局血条是空的 | 引用拖的是敌人身上别的对象 | `_health` 必须指向**挂着 `EnemyHealth` 的那个物体** |
| Boss 血条永远不出现 | 面板被 `SetActive(false)` 连脚本一起关了 | 按 §3.3 的结构，脚本挂父节点、Panel 当子节点 |
| Boss 血条出现后不消失 | 订阅了但没做幂等 / `_hideWhenNotInCombat` 关了 | 检查开关；确认 `_health.IsDead` 判断生效 |
| 找不到 Boss | 场景里 Boss 实例的名字带**尾随空格**（`'Boss_Crichton '`，14 个字符） | 不要用 `GameObject.Find("Boss_Crichton")`；改用 `FindObjectsByType` 按组件类型找，或者顺手把空格删掉 |
| 韧性条方向反了 | `Poise01` 是「已削韧量」 | 见 §5 最后一行 |

---

## 改动清单（2026-10-15）

**新增脚本**

- `Assets/_Game/Scripts/UI/EnemyHealthBar.cs` —— 小怪头顶悬浮血条
- `Assets/_Game/Scripts/UI/BossHealthBar.cs` —— Boss 屏幕固定血条

**修改脚本**

- `Assets/_Game/Scripts/Gameplay/Enemy/EnemyHealth.cs` —— 新增 `HpChanged` 事件
- `Assets/_Game/Scripts/Gameplay/Enemy/BossController.cs` —— 新增 `CombatChanged` 事件与 `InCombat` 属性

**修改资产**

- `Assets/_Game/Prefabs/Enemy/Monster1.prefab` —— 新增 `HPBar` 子树 + 接线
- `Assets/_Game/Prefabs/Enemy/Monster2.prefab` —— 同上
- `Assets/Scenes/Castle.unity` —— Canvas 下新增 `BossBar` 子树 + 接线；`RoomHIntText` 的 `Pos Y` 由 `-60` 改为 `-120`

**备份**：`.workbuddy/backup/hpbar-2026-10-15/`（含上述 prefab / 场景 / 脚本的改动前副本）

**相关文档**：`GDD.md` §6（敌人）、`PHASE1-DESIGN.md` §5.4（Boss 状态机）
