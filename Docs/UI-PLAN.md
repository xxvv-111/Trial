# UI 现状盘点与制作计划（UI-PLAN）

> 生成日期：2026-10-19　｜　依据：`GDD.md` §10、`ROADMAP.md` §7（M5.1/M5.2）、`PHASE1-DESIGN.md` §4/§5
> 本文只回答两件事：**现在有哪些 UI、还缺哪些、每个怎么搭**。血条的详细教程见 `UI-HEALTHBAR-GUIDE.md`，不重复。

---

## 0. 一句话结论

战斗 HUD 的**骨架是齐的**（血条 / 魔力条 / Boss 条 / 暂停 / 结算壳 / 交互提示 / 伤害飘字），
**缺的是"活"的三块**：**技能栏没有逻辑**、**结算没有统计**、**区域进度没有显示**。
RPG 层（酒馆 4 个 NPC 面板 + 魔晶 + 法术槽）**一行代码都没写**，只有占位。

按"答辩能不能演示"排，优先级是：**技能栏 → 结算统计 → 酒馆面板**，其余可砍。

---

## 1. 现状盘点

### 1.1 MainMenu.unity（✅ 2026-10-20：设置按钮已启用 + 键位面板落地）

1. `Canvas` → `Text_Title`（标题）/ `Button_Start` / `Button_Settings` / `Button_Quit`
2. 脚本 `UI/MainMenu.cs`：`StartGame()` 直接 `LoadScene("Tavern")`，`QuitGame()` 退出
3. `Canvas/Panel_Keybinds`（键位说明，**预设 inactive**），脚本 `UI/KeybindPanel.cs` 挂在 `Canvas` 上：
   `Button_Settings.OnClick → Open()`、面板内 `Button_Close.OnClick → Close()`、`ESC` 关闭
4. ⚠️ 目前只有**键位展示**，**不能改键**；改键功能见 §3.6
5. ⚠️ 仍没有加载过程、没有武器/角色选择

### 1.2 Tavern.unity（1 个 Canvas，3 块 UI —— ✅ 2026-10-20 全部重做为 v4 风格）

1. `Canvas_InteractPromot`（⚠️ 拼写如此，别改名，脚本按名字找）
2. **`Panel_InteractPrompt`**（HUD 提示条，**必须保持 active**）：`KeyCap`（1px 方框 + `F`）+ `Text`（TMP）
   —— ⚠️ 原先的旧 `UI.Text` 已换成 `TextMeshProUGUI`
3. **`Panel_QuestPanel`**（弹窗，预设 **inactive**）：`Content` 下一整套分栏 ——
   标题 `任务` / `QUESTS` → 左列 `文本列表`+`Row_Quest_1/2`（序号 + 标题 + 状态 + 选中竖条）/
   中缝 `Divider_Vertical` / 右列 `详情` + `Text_DetailTitle` + `Text_DetailBody` + `Text_DetailGoal` /
   底部 `Line_Foot` + `Button_Close` + `ESC 关闭`
4. **`PausePanel`**（弹窗，预设 **inactive**）：`Content` 下 ——
   标题 `暂停` / `PAUSED` → 左列竖排 `继续游戏 / 重新开始 / 返回主菜单`（左侧 2px 竖线 + 左对齐）/
   中缝 `Divider_Vertical` / 右列 `当前位置` + `酒馆` + `TAVERN · 安全区域` + 操作提示 5 行 /
   底部 `Line_Foot` + `ESC 继续游戏`
5. 脚本：`InteractPromptUI`（订阅 `InteractionDetector.TargetChanged`，**Start 里还会主动同步一次**
   —— 检测器可能在它之前就 Rescan 过，那次事件没订阅者会丢）、`QuestPanel`（**假数据占位**，
   已支持分栏：`_entryLabels[] / _detailTitle / _detailBody / _detailGoal`）、`PauseMenu`
6. 生成器：`Tools/UI/生成酒馆 UI（全部）` + 三个分项 → `Assets/_Game/Editor/TavernUIBuilder.cs`，
   公共 helper 在 `Assets/_Game/Editor/UIBuilderKit.cs`
   > ⚠️ **2026-10-24 已删除这两个脚本**：它们生成的是**旧版**任务面板（固定两行 `Row_Quest_1/2`），
   > 与第 5 步改成 `List_Rows` + 动态行的现行结构冲突，重跑会覆盖掉现有面板。
   > 需要复查 UI 结构时以场景里的实际对象为准（或 `git show <commit>:路径` 取回脚本）。
7. ✅ 顺手修掉两个**旧 bug**：
   - 暂停面板三个按钮原先全接 `GameManager`，而 **Tavern 没有 GameManager** ⇒ `Missing`、点了没反应。
     现在改接 `PauseMenu` 自己（新增 `RestartScene()`）
   - `Interactable.prompt` 是英文（`Check` / `Go`），配合中文格式串会变成"按 F Check" ⇒ 已本地化

### 1.3 Castle.unity（1 个 Canvas，10 个 HUD/面板）

HUD（常驻）：

1. `HPBarBack` → `HPFill`　脚本 `HPBar`（订阅 `PlayerHealth.OnHpChanged`，锚点拉伸填充）
2. `ManaBarBack` → `ManaFill`　脚本 `ManaBar`（订阅 `PlayerMana.OnManaChanged`，不足时 `Flash()`）
3. `BossBar` → `Panel` → `Name` / `HpBack`→`HpFill` / `PoiseBack`→`PoiseFill`　脚本 `BossHealthBar`
4. `RoomHIntText`（⚠️ 名字大小写如此）　脚本 `RoomHintText`（3 秒后自动清空）
5. `SkillCD1` ~ `SkillCD4`：**Image + Text(TMP)，四个都是空壳**（详见 §2）

弹窗（预设 inactive）：

6. `PausePanel`：标题 / 继续 / 重开 / 回主菜单
7. `GameOverPanel`：`'DeathMsgText '`（⚠️ 名字带尾随空格）/ Restart / MainMenu
8. `VictoryOverPanel`：`'VictoryMsgText '`（同样带空格）/ Restart / MainMenu
9. `ResultText`：**inactive**，由 `GameManager` 写 `VICTORY! TIME:xx.xxs` 后随胜利面板一起显示
10. ⚠️ `BossBar/Panel` 的 `m_IsActive` 是 **1**，靠 `BossHealthBar` 在运行时隐藏 → 进场景瞬间会闪一下，建议改成预设 0

### 1.4 脚本层（`Assets/_Game/Scripts/UI/` 共 11 个）

1. `HPBar` / `ManaBar`：`[SerializeField]` 引用 + **锚点拉伸**（不是 `fillAmount`），`OnEnable/OnDisable` 成对订阅
2. `BossHealthBar`：Screen Space 固定条，订阅 `EnemyHealth.HpChanged` + `BossController.CombatChanged`
3. `EnemyHealthBar`：小怪头顶 World Space 血条（挂在敌人预制体下，scale 0.01 + billboard + 距离剔除）
4. `InteractPromptUI`：**事件驱动**，只在目标变化那帧改文字（每帧刷会持续 GC）
5. `UIWindow`：弹窗基类，**双闸**（`Time.timeScale=0` + `InputService.SetGameplayInputEnabled(false)`）
6. `PauseMenu`：ESC 开关，同样双闸，并检查 `GameManager.HasEnded` 避免结算后还能开
7. `QuestPanel`：继承 `UIWindow`，内部 `BuildPlaceholder()` 写死假任务
8. `RoomHintText`：显示 3 秒后清空，重复调用覆盖上一条
9. `MainMenu`：切场景
10. 非 UI 目录但归 UI 管：`GameManager`（订阅 `GameEvents.PlayerDied/BossDied` → 面板 + 暂停）、`HitFxSystem` + `DamagePopup`（伤害飘字，对象池）

---

## 2. 缺口清单（按优先级）

### P0 · 演示必需（建议本周做完）

1. **技能栏没有逻辑**
   - 现状：4 个 `Image` 的 `m_Sprite` 全是空，`Material` 里只有一个被误挂了 URP/Lit（详见 §5 坑 6），没有任何脚本
   - 缺：图标资源、冷却遮罩、按键提示、魔力不足反馈、冷却数据源
   - ⚠️ **没有技能系统代码**（`grep Skill` 在 `.cs` 里 0 命中）—— 火球走的是 `PlayerMana.TrySpendSpecial` + `PlayerAttack`，**没有冷却字段，也没有冷去事件**，UI 现在无处可取数

2. **结算面板没有统计**
   - 现状：只有 `ResultText` 里一句 `VICTORY! TIME:xx.xxs`
   - 缺：击杀数、剩余生命、到达区域、存活时间（`GDD` §10.3 / `ROADMAP` M5.2 都点名了）
   - 数据源：`EnemyHealth` 的死亡、`PlayerHealth` 当前值、`GameManager._startTime`

3. **区域/清怪进度没有显示**
   - ⚠️ `GDD` §10.2 写的是 "Level 1/3"，但**多关卡拆分（M3.7）已于 10-07 作废**，现在是单场景 Castle
   - 结论：**别做 Level 1/3**。改成"当前区域 x/y"或"剩余敌人 n"，数据从 `RoomController` 取

### P1 · 答辩加分

4. **酒馆 4 个 NPC 面板**（装备升级 / 法术学习 / 锻炼 / 任务）
   - `PHASE1-DESIGN` §4 已给架构：`NpcPanelBase` 基类 + 4 个子面板，复用 `UIWindow` 的双闸
   - 现状：只有 `QuestPanel` 一个假数据占位
   - 建议：**先做「装备升级」一个走通全链路**（列表 + 选中 + 消耗魔晶 + 数值变化 + 存档），另外 3 个先出空面板
5. **魔晶计数 + 拾取提示**：HUD 右上角计数 + 拾取时 "+1" 飘字（可复用 `DamagePopup`）
6. **操作说明面板**：静态图文（WASD / Shift / 空格 / 鼠标左键 / 右键火球 / F 交互 / ESC），主菜单和暂停菜单都挂一个入口
7. **设置面板**：主音量 / BGM / SFX 三个 Slider —— ⚠️ 但 `AudioManager` **完全不存在**（`Assets/_Game/Audio/` 只有 `.gitkeep`），先做 UI 存 `PlayerPrefs`，等 `AudioManager` 落地再接

### P2 · 时间紧可砍

8. 关卡选择（GDD 自己标了"工期紧可降级"，且多关卡已作废 → **直接砍**）
9. 最好成绩 / 存档（`ROADMAP` M5.4，属于 M5 收口，不是 UI 重点）
10. 武器 / 角色选择界面（`Weapon_Sword.asset` / `Weapon_Spear.asset` 的 `icon` 都是 `{fileID: 0}`，且 V1 只有剑 → 砍）

---

## 3. 逐项制作建议

### 3.1 技能栏（P0，最重要）

**层级**（每个槽位）：

```
Canvas
└── SkillBar                       ← Horizontal Layout Group，间距 8，靠右下
    ├── Slot_1                     ← Image(图标) + Button(可选)
    │   ├── CooldownMask           ← Image，Type=Filled，Method=Radial360，Fill Origin=Top
    │   ├── KeyLabel               ← TMP "1"
    │   └── CostLabel              ← TMP "30"（魔力消耗，可选）
    ├── Slot_2 / Slot_3 / Slot_4
```

**要点**：

1. 图标 = `Slot` 自身的 `Image.Source Image`（**贴图必须是 Sprite 类型、Sprite Mode = Single**，见 §5 坑 6）
2. 冷却 = **子物体 `CooldownMask` 的 `fillAmount`**，不要用锚点拉伸（锚点拉伸是血条的做法）
3. 冷却数字 = `CooldownMask` 上再叠一个 TMP，**只在整数秒变化时**改 `text`（每帧改会持续分配字符串）
4. 不足魔力 = 复用 `ManaBar.Flash()` 的思路，`OnManaSpendFailed` 时把槽位 `Image.color` 闪红 0.15s

**必须先补的数据源**（现在没有，这是卡点）：

```csharp
// PlayerAttack 里加，UI 只订阅，不自己计时
public event Action<int, float, float> SpecialCooldownChanged; // slot, remain, total
```

1. 冷却时间本身放进 `WeaponConfig`（`specialCooldown` 字段），别写死在 UI
2. ⚠️ 用 `WaitForSecondsRealtime` 或直接读 `Time.time`，**别用 `Time.timeScale` 依赖的计时**（顿帧会把 `timeScale` 压到 0）

**脚本**：新建 `UI/SkillSlotUI.cs`，字段 `Image icon` / `Image cooldownMask` / `TMP keyLabel` / `PlayerMana playerMana`；
`OnEnable/OnDisable` 成对订阅，**不轮询**。

### 3.2 区域进度（P0，半天）

1. 结构：`Canvas/ZoneProgress` → TMP `"区域 2/4"` 或 `"剩余 7"`
2. 数据源：`RoomController` 清怪数，或 `EnemyHealth` 死亡累加
3. ⚠️ 别做"Level 1/3"（多关卡已作废），否则和实际场景对不上

### 3.3 结算统计（P0，半天）

1. `GameManager` 加 3 个字段：`_killCount`、`_playerHpAtEnd`、`_zoneReached`，在 `OnVictory/OnPlayerDied` 里填
2. 面板结构从"一句话"改成 4 行：`结果` / `用时` / `击杀` / `剩余生命`
3. ⚠️ `DeathMsgText `/`VictoryMsgText ` 名字**带尾随空格**，`GameObject.Find` 找不到，必须走 `[SerializeField]` 引用

### 3.4 酒馆 NPC 面板（P1，每个 0.5~1 天）

1. 基类 `NpcPanelBase : UIWindow`，只加"标题 + 内容区 + 关闭"
2. 4 个子面板共用布局：左侧条目列表（`ScrollRect` + `VerticalLayoutGroup` + 条目 prefab），右侧详情 + 确认按钮
3. ⚠️ 条目 prefab **不要在运行时 `Instantiate` 到 Canvas 根**，一定挂到 `Content` 下，否则 Layout 全乱
4. ⚠️ 打开面板必须走 `UIWindow.Open()`（双闸），别只 `SetActive(true)` —— 否则鼠标还能转视角

### 3.5 魔晶计数 / 拾取提示（P1）

1. HUD 右上角 `Image(魔晶图标) + TMP(数量)`，订阅 `GameEvents.LootPicked`
2. 拾取飘字直接复用 `DamagePopup`（对象池现成）

### 3.6 设置 / 操作说明（P1）

1. **操作说明（键位表）：✅ 2026-10-20 已完成** —— `MainMenu/Canvas/Panel_Keybinds` + `KeybindPanel`，
   生成器 `Tools/UI/生成键位面板 (MainMenu)`，视觉规范见 `UI-STYLE.md` §5.5。
   键位数值全部从 `Assets/InputSystem_Actions.inputactions` 的**实际绑定**抄的，改键位后记得同步这张表
2. **改键功能（未做）**：行节点已按 `Content/Row_<Id>/Key` 命名，`KeybindPanel.FindKeyLabel(id)` 是现成入口；
   真正改键必须走 Input System 的 `PerformInteractiveRebinding()` —— ⚠️ 它要临时禁用 `Player` action map，
   否则按下的键会同时触发角色动作
3. 设置：3 个 `Slider` + 1 个 `Dropdown`（分辨率，可选），值存 `PlayerPrefs`，等 `AudioManager` 落地再接线
4. 操作说明建议**同时**放进 `PausePanel`（从暂停界面也能看），复用同一个面板预设

---

## 4. 统一规范（**开工前先定，避免返工**）

1. **分辨率**：`CanvasScaler` = `Scale With Screen Size`，参考 **1920×1080**，`Match = 0.5`
2. **Sorting Order 分层**：HUD = 0 / 弹窗 = 100 / 提示与飘字 = 200 / 过场遮罩 = 300
3. **预设 inactive**：所有弹窗根节点在场景里**预先设为 inactive**（`UIWindow` 注释里写了，否则运行时会闪一下）
4. **字号阶梯**：标题 32 / 小标题 24 / 正文 20 / 辅助 16（避免每个面板各写一套）
5. **色彩**：主色暖橙（`#E8873A` 一档）、危险红、成功绿；⚠️ 血量用红、魔力用蓝，别和"中国股市红涨绿跌"混用
6. **中文字体**：全工程只有 `LiberationSans SDF` 一处引用，已挂 Sarasa 中文 fallback；**新 TMP 直接用这个默认字体**，别再导新字体
7. **9-slice**：所有面板背景图先在 Sprite Editor 里设 Border，再 `Image.Type = Sliced`，否则放大后边缘拉伸
8. **图集**：UI 图标统一走一张 Sprite Atlas（Sprite Mode = Multiple + Sprite Editor 切图），减少 Draw Call

---

## 5. 通用坑（都是本项目踩过的）

1. **弹窗双闸**：`Time.timeScale = 0` **拦不住输入**，必须同时 `InputService.SetGameplayInputEnabled(false)`（`UIWindow` / `PauseMenu` / `GameManager` 都这么写）
2. **事件成对订阅**：`OnEnable` 订阅、`OnDisable` 退订，跨场景不清理会重复触发
3. **数据源在父物体**：子物体 `OnEnable` 读到的是未初始化值 → 除订阅外还要在 `Start` 主动同步一次（血条踩过）
4. **别每帧 `SetText`**：字符串分配会持续 GC，只在值变化时写
5. **血条 / 冷却的填充方式不同**：血条 = 锚点拉伸（`anchorMax`）；冷却 = `Image.fillAmount`
6. **UI 不能用 3D 材质**：`SkillFireBall.mat` 是 **URP/Lit**（不透明、ZWrite On），挂在 `Image` 上不显示；UI 只能用 `UI/Default` 这类 UI shader
7. **贴图导入**：走 UI 的图必须 `Texture Type = Sprite (2D and UI)` + `Sprite Mode = Single`；**Multiple 且没切图 = 一个 sprite 子资产都没有，拖不进 `Source Image`**（2026-10-19 实际踩过）
8. **URP 下第三方 `.mat`**：shader guid 是 `0000000000000000f000000000000000`（Built-in）⇒ 品红，需要 Convert
9. **TMP 新版字段**：`m_alignment` 已拆成 `m_HorizontalAlignment` / `m_VerticalAlignment`
10. **场景里带空格的名字**：`'Boss_Crichton '`、`'DeathMsgText '`、`'VictoryMsgText '` —— 不要用 `GameObject.Find`

---

## 6. 建议排期（按 0.5~1 天粒度）

1. **D1**：技能栏数据源（`WeaponConfig.specialCooldown` + `PlayerAttack.SpecialCooldownChanged`）+ `SkillSlotUI`
2. **D2**：技能图标资源（4 张 128×128 带 alpha）+ 冷却遮罩 + 魔力不足闪烁
3. **D3**：区域进度 + 结算统计（`GameManager` 扩字段 + 面板改 4 行）
4. **D4**：`BossBar/Panel` 预设 inactive、HUD 视觉统一（字号/间距/颜色）
5. **D5~D6**：酒馆「装备升级」面板走通全链路（列表 + 详情 + 消耗 + 数值变化）
6. **D7**：另外 3 个 NPC 面板出骨架 + 魔晶计数
7. **D8**：操作说明 + 设置面板（先存 `PlayerPrefs`）

> P2 的关卡选择 / 存档 / 武器选择在时间不足时直接不做，不影响演示效果。
