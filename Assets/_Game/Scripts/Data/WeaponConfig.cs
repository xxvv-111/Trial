using UnityEngine;

namespace Game.Data
{
    /// <summary>特殊攻击类型。决定特殊攻击走哪条分支（GDD §5.3）。</summary>
    public enum SpecialAttackType
    {
        /// <summary>未配置特殊攻击。</summary>
        None = 0,

        /// <summary>剑·插地：以自身为中心的圆形范围爆发。⚠️ 2026-10-05 已作废（改为 <see cref="Fireball"/>）。</summary>
        SwordSlam = 1,

        /// <summary>长枪·投掷与召回：固定距离直线投出 → 落地 → 空手 → 召回（返程伤害）。</summary>
        SpearThrow = 2,

        /// <summary>
        /// 剑·火球（2026-10-05 改定）：向面朝方向**发射火球、远距离攻击**，命中后**小范围爆炸**。
        /// 取代原「剑·插地」——游戏背景允许使用魔法。
        /// </summary>
        Fireball = 3,
    }

    /// <summary>
    /// 武器配置（GDD §5.2）：**每把武器一份**，玩家普攻与特殊攻击的全部数值都在这里。
    ///
    /// 设计意图：把"属于武器的数值"从 <see cref="PlayerConfig"/> 里分出来。
    /// PlayerConfig 只保留**角色自身**属性（移动、生命、魔力上限与再生）；
    /// 伤害 / 判定盒 / 连段窗口 / 特殊攻击消耗 —— 全部随武器走。
    ///
    /// ⚠️ 2026-10-06 起**普攻不再消耗资源**（原每段体力消耗 <c>energyCost</c> 已删除），
    /// 只有特殊攻击消耗魔力。所以现在武器只提供"特殊攻击的魔力消耗"。
    ///
    /// ⚠️ 使用方（都持有同一个武器引用，由 M2.2 的 WeaponManager 统一装配）：
    ///   · <c>PlayerAttack</c> —— 伤害、判定盒尺寸、连段窗口
    ///   · <c>PlayerMana</c>   —— 特殊攻击的魔力消耗
    ///     （⚠️ 成本表仍集中在 PlayerMana，符合"统一出口"原则）
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Game/WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("标识")]
        public string displayName = "未命名武器";

        [Tooltip("选择界面用的图标（M2.2 开局二选一）。可为空。")]
        public Sprite icon;

        [Header("普通攻击 · 连段")]
        [Tooltip("连段窗口（秒）：上一段结束后多久内可接下一段。")]
        public float comboWindow = 1f;

        [Tooltip("4 段伤害。")]
        public int[] damage = { 12, 15, 10, 20 };

        [Header("普通攻击 · 三轴位移（代码驱动）")]
        [Tooltip("4 段攻击各自的前冲**距离**（米）—— **仅在 lungeCurve 留空时**当兜底幅度用。\n" +
                 "画了 Z 轴曲线就以曲线的峰值为准，这个数值只作为策划案上的读数参考。\n" +
                 "实测值（= 各段动画根运动只取正向增量的结果）= 1.25 / 0.88 / 0.95 / 2.25 m。")]
        public float[] lungeDistance = { 1.25f, 0.88f, 0.95f, 2.25f };

        [Tooltip("4 段各自的位移**时长**（秒）—— 三条曲线共用的时间轴长度，默认 = 各段动画时长（实测）。\n" +
                 "代码在「本段已过秒数」超过本值后就**冻结**位移（不再评估曲线），避免曲线外推。\n" +
                 "⭐ 你画曲线时横轴写到几秒，这里就该填几秒。")]
        public float[] lungeDuration = { 1.000f, 1.200f, 1.133f, 1.633f };

        [Tooltip("4 段各自的**前向（Z）位移曲线**：横轴 = **秒**，纵轴 = **米**。\n" +
                 "⭐⭐ 口径：**直接把曲线的值当作位移量，不再乘任何系数**；峰值是多少米，这一刀就冲多少米。\n" +
                 "   （2026-10-07 改口径：旧版是「归一化时间 0~1 × 归一化进度 0~1」，\n" +
                 "     与照抄逐帧表画出来的曲线对不上，故改为与表格一致的字面量口径。）\n" +
                 "⭐ 曲线**下降段 = 向后退**（由 PlayerMotor 的 allowLungeBackstep 控制，默认允许）；\n" +
                 "   曲线终点值 = 最后停在离起点多少米处（0 = 退回起点，与动画 Attack2 的根曲线一致）。\n" +
                 "⛔ 留空 = 回落到「lungeDistance × 默认前快后缓曲线（归一化时间）」。\n" +
                 "⚠️ 逐帧实测值见 `.workbuddy/attack-z-per-frame.md`（第一列是秒，第三列是米，直接照抄）。")]
        public AnimationCurve[] lungeCurve;

        [Header("普通攻击 · 横向位移（X 轴，可留空）")]
        [Tooltip("4 段攻击各自的**横向幅度**（米，正 = 角色自身右侧）—— **仅在 lungeLateralCurve 留空时**当兜底用。\n" +
                 "实测峰值（RootT.x）= 0.077 / 0.129 / 0.156 / 0.088 m —— 幅度很小，\n" +
                 "是挥砍时身体重心的小幅横摆；画大了会像「平移」而不是「挥砍」。")]
        public float[] lungeLateral = { 0f, 0f, 0f, 0f };

        [Tooltip("4 段各自的**横向（X）位移曲线**：横轴 = **秒**，纵轴 = **米**（正 = 角色右侧，负 = 左侧）。\n" +
                 "⭐ 与 Z 轴同口径：曲线值就是位移量。**允许负值**（左右摇摆本来就要两边走）。\n" +
                 "⚠️ 逐帧实测值见 `.workbuddy/attack-x-per-frame.md`。")]
        public AnimationCurve[] lungeLateralCurve;

        [Header("普通攻击 · 垂直位移（Y 轴，可留空）")]
        [Tooltip("4 段攻击各自的**垂直幅度**（米，正 = 向上）—— **仅在 lungeVerticalCurve 留空时**当兜底用。\n" +
                 "实测（RootT.y）= -0.20 / -0.18 / -0.20 / -0.29 m：**全是向下沉**，也就是攻击时的「下蹲/压身」。\n" +
                 "⚠️ 2026-10-07 实测确认：这个下沉**只存在于根曲线**（骨骼姿势只额外贡献 0.02~0.05 m），\n" +
                 "   而「劫持根运动」会把 Y 分量丢掉 ⇒ 之前角色攻击时**少了这 0.2 m 的下沉**（腿已经把脚收回去了，\n" +
                 "   所以看起来像踮脚）。补上这一段即可还原。\n" +
                 "✅ 脚不会穿地：脚踝静止高度 0.148 m，动画里脚最低只到 -0.145 m ⇒ 仍在地面之上。")]
        public float[] lungeVertical = { 0f, 0f, 0f, 0f };

        [Tooltip("4 段各自的**垂直（Y）位移曲线**：横轴 = **秒**，纵轴 = **米**（正 = 向上）。\n" +
                 "⭐ 该通道由 `PlayerMotor` **直接偏移骨骼根（Armature）的 localPosition.y**，\n" +
                 "   是**纯视觉**位移：不经过 CharacterController，所以不会被地面挡住、也不影响碰撞与寻路。\n" +
                 "⚠️ 逐帧实测值见 `.workbuddy/attack-y-per-frame.md`。")]
        public AnimationCurve[] lungeVerticalCurve;

        [Header("普通攻击 · 判定盒（与 §8 的 hitbox 联动）")]
        [Tooltip("4 段判定盒长度（米，沿角色前向）。")]
        public float[] hitboxLength = { 2.5f, 2.7f, 2.9f, 3.1f };

        [Tooltip("4 段判定盒宽度（米，左右）。")]
        public float[] hitboxWidth = { 1.5f, 1.62f, 1.74f, 1.86f };

        [Tooltip("判定盒高度（米）。各段相同。")]
        public float hitboxHeight = 1.6f;

        [Tooltip("判定盒中心高度（米，相对脚底）。1.0 约在腰部。")]
        public float hitboxCenterY = 1.0f;

        [Header("特殊攻击（GDD §5.3，两把武器都是点按触发）")]
        public SpecialAttackType specialType = SpecialAttackType.None;

        public int specialDamage = 25;

        [Tooltip("特殊攻击的魔力消耗（2026-10-06 起本字段语义由「体力」改为「魔力」）。")]
        public float specialManaCost = 30f;

        [Tooltip("特殊攻击冷却（秒）。")]
        public float specialCooldown = 3f;

        [Tooltip("剑·插地 = 范围半径；长枪 = 投掷飞行距离；**火球 = 最大飞行距离（射程）**。")]
        public float specialRange = 6f;

        [Header("特殊攻击 · 火球专用（specialType = Fireball）")]
        [Tooltip("火球飞行速度（米/秒）。")]
        public float specialProjectileSpeed = 12f;

        [Tooltip("火球命中后的**爆炸半径**（米）—— 范围内所有敌人都会受伤。")]
        public float specialExplosionRadius = 2f;

        [Tooltip("施法动画开始后多久**出手**（秒）。用于对齐 animation 里的出手帧。\n" +
                 "atk_energy01 时长 1.067 s，默认 0.55 s 约在动作过半时。")]
        public float specialCastDelay = 0.55f;

        [Tooltip("火球的出手高度（米，相对脚底）。1.2 约在胸口，避免打地。")]
        public float specialSpawnHeight = 1.2f;

        [Tooltip("火球预制体（需挂 Fireball 脚本 + Trigger 碰撞体 + Rigidbody）。")]
        public GameObject specialProjectilePrefab;

        [Header("外观")]
        [Tooltip("武器模型（挂到右手骨骼挂点上）。可为空 —— 先用无武器网格试动作。")]
        public GameObject modelPrefab;

        [Tooltip("模型相对挂点的本地偏移/旋转（用于校正握持角度）。")]
        public Vector3 modelLocalPosition = Vector3.zero;
        public Vector3 modelLocalEuler = Vector3.zero;

        [Tooltip("握持挂点骨骼名。Roskva 模型自带 B_Weapon_L / B_Weapon_R。")]
        public string gripBoneName = "B_Weapon_R";

        // ==================== 取值辅助（带越界保护，避免配置写歪就崩） ====================

        /// <summary>取第 <paramref name="comboIndex"/> 段（0 基）伤害。越界时回落到最后一段。</summary>
        public int GetDamage(int comboIndex)
        {
            if (damage == null || damage.Length == 0) return 0;
            return damage[Mathf.Clamp(comboIndex, 0, damage.Length - 1)];
        }

        /// <summary>普攻段数。</summary>
        public int ComboLength
        {
            get { return damage == null ? 0 : damage.Length; }
        }

        /// <summary>取第 <paramref name="comboIndex"/> 段（0 基）判定盒尺寸（宽, 高, 长）。</summary>
        public Vector3 GetHitboxSize(int comboIndex)
        {
            float len = 2.5f, wid = 1.5f;
            if (hitboxLength != null && hitboxLength.Length > 0)
                len = hitboxLength[Mathf.Clamp(comboIndex, 0, hitboxLength.Length - 1)];
            if (hitboxWidth != null && hitboxWidth.Length > 0)
                wid = hitboxWidth[Mathf.Clamp(comboIndex, 0, hitboxWidth.Length - 1)];
            return new Vector3(wid, hitboxHeight, len);
        }

        /// <summary>
        /// 取第 <paramref name="comboIndex"/> 段判定盒的局部位置。
        /// 中心放在身前 <c>长度/2</c> 处 → 判定盒覆盖「身前 0 ~ 长度」这一段（与 M1.2 的摆放一致）。
        /// </summary>
        public Vector3 GetHitboxLocalPosition(int comboIndex)
        {
            return new Vector3(0f, hitboxCenterY, GetHitboxSize(comboIndex).z * 0.5f);
        }

        /// <summary>第 <paramref name="comboIndex"/> 段（0 基）的前冲**总距离**（米）。越界回落到最后一段。</summary>
        public float GetLungeDistance(int comboIndex)
        {
            if (lungeDistance == null || lungeDistance.Length == 0) return 0f;
            return lungeDistance[Mathf.Clamp(comboIndex, 0, lungeDistance.Length - 1)];
        }

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段的前冲**时长**（秒）。
        /// ⚠️ 永不返回 ≤ 0 —— 否则调用方算归一化时间时会**除零**。
        /// </summary>
        public float GetLungeDuration(int comboIndex)
        {
            const float Fallback = 0.5f;
            if (lungeDuration == null || lungeDuration.Length == 0) return Fallback;
            float d = lungeDuration[Mathf.Clamp(comboIndex, 0, lungeDuration.Length - 1)];
            return d > 0.01f ? d : Fallback;
        }

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段的**前向（Z）位移曲线**。
        /// ⭐ **横轴 = 秒，纵轴 = 米**（曲线的绝对值就是位移量，不是 0~1 的进度）。
        /// 没配 / 曲线为空时返回 <c>null</c> —— 由调用方回落到
        /// 「<see cref="GetLungeDistance"/> × <see cref="DefaultLungeCurve"/>（归一化时间）」。
        /// </summary>
        public AnimationCurve GetLungeCurve(int comboIndex)
        {
            return PickCurve(lungeCurve, comboIndex);
        }

        // ==================== 横向（X 轴）位移：取值辅助 ====================

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段（0 基）的**横向幅度**（米，正 = 角色右侧）。
        /// ⚠️ 只在**没画曲线**时当兜底用；画了曲线就以曲线为准。
        /// </summary>
        public float GetLungeLateral(int comboIndex)
        {
            if (lungeLateral == null || lungeLateral.Length == 0) return 0f;
            return lungeLateral[Mathf.Clamp(comboIndex, 0, lungeLateral.Length - 1)];
        }

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段的**横向（X）位移曲线**。
        /// ⭐ **横轴 = 秒，纵轴 = 米**，正 = 角色右侧。没配时返回 <c>null</c>（由调用方兜底）。
        /// </summary>
        public AnimationCurve GetLungeLateralCurve(int comboIndex)
        {
            return PickCurve(lungeLateralCurve, comboIndex);
        }

        // ==================== 垂直（Y 轴）位移：取值辅助 ====================

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段（0 基）的**垂直幅度**（米，正 = 向上）。
        /// ⚠️ 只在**没画曲线**时当兜底用；画了曲线就以曲线为准。
        /// </summary>
        public float GetLungeVertical(int comboIndex)
        {
            if (lungeVertical == null || lungeVertical.Length == 0) return 0f;
            return lungeVertical[Mathf.Clamp(comboIndex, 0, lungeVertical.Length - 1)];
        }

        /// <summary>
        /// 第 <paramref name="comboIndex"/> 段的**垂直（Y）位移曲线**。
        /// ⭐ **横轴 = 秒，纵轴 = 米**，正 = 向上。没配时返回 <c>null</c>（由调用方兜底）。
        /// </summary>
        public AnimationCurve GetLungeVerticalCurve(int comboIndex)
        {
            return PickCurve(lungeVerticalCurve, comboIndex);
        }

        /// <summary>按段号从曲线数组里取一条；越界 / 为空 / 空曲线都返回 null。</summary>
        private static AnimationCurve PickCurve(AnimationCurve[] arr, int comboIndex)
        {
            if (arr == null || arr.Length == 0) return null;
            AnimationCurve c = arr[Mathf.Clamp(comboIndex, 0, arr.Length - 1)];
            return (c != null && c.length > 0) ? c : null;
        }

        private static AnimationCurve _defaultLungeCurve;

        /// <summary>
        /// 兜底的「前快后缓」进度曲线：前半程走约 75%、后半程 25%，**全程单调**。
        ///
        /// ⚠️ **起点切线必须 ≤ 2**：Unity 的 <see cref="AnimationCurve"/> 是 Hermite 插值，
        ///    在「h(0)=0 / h(1)=1 / 终点切线 0」的条件下，起点切线 <c>m &gt; 2</c> 会让曲线
        ///    **过冲**（冲到 100% 以上再回落）。实测 <c>m = 5</c> 时 t=0.5 就冲到 **112.5%** ——
        ///    表现是"冲过头再被拉回"，总位移也会超出配置值。取 <c>m = 2</c> 时曲线退化为
        ///    抛物线 <c>h(t) = -t² + 2t</c>，数学上**严格单调**。
        ///
        /// ⚠️ **静态缓存** —— 调用方（PlayerMotor）每帧都会取曲线，每次 new 一条会产生 GC 压力。
        /// </summary>
        public static AnimationCurve DefaultLungeCurve
        {
            get
            {
                if (_defaultLungeCurve == null)
                {
                    _defaultLungeCurve = new AnimationCurve();
                    _defaultLungeCurve.AddKey(new Keyframe(0f, 0f, 2f, 2f));
                    _defaultLungeCurve.AddKey(new Keyframe(1f, 1f, 0f, 0f));
                }
                return _defaultLungeCurve;
            }
        }

        private void OnValidate()
        {
            //配置写歪时给出提示，而不是等运行期崩
            if (damage == null || damage.Length == 0)
                Debug.LogWarning("[WeaponConfig] " + name + " 未配置 damage，普攻将没有伤害。", this);
        }
    }
}
