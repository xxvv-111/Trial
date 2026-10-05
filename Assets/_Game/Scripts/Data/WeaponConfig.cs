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
    /// PlayerConfig 只保留**角色自身**属性（移动、生命、体力上限与再生）；
    /// 伤害 / 判定盒 / 连段窗口 / 每段体力消耗 / 特殊攻击 —— 全部随武器走。
    ///
    /// ⚠️ 使用方（都持有同一个武器引用，由 M2.2 的 WeaponManager 统一装配）：
    ///   · <c>PlayerAttack</c>   —— 伤害、判定盒尺寸、连段窗口
    ///   · <c>PlayerEnergy</c>   —— 每段普攻与特殊攻击的体力消耗
    ///     （⚠️ 成本表仍集中在 PlayerEnergy，符合"统一出口"原则）
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

        [Tooltip("4 段体力消耗（GDD §4.8：消耗 > 当前体力则该段不接续）。")]
        public int[] energyCost = { 8, 10, 12, 15 };

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

        [Tooltip("特殊攻击体力消耗。")]
        public float specialEnergyCost = 30f;

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

        /// <summary>取第 <paramref name="comboIndex"/> 段（0 基）体力消耗。</summary>
        public int GetEnergyCost(int comboIndex)
        {
            if (energyCost == null || energyCost.Length == 0) return 0;
            return energyCost[Mathf.Clamp(comboIndex, 0, energyCost.Length - 1)];
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

        private void OnValidate()
        {
            //配置写歪时给出提示，而不是等运行期崩
            if (damage == null || damage.Length == 0)
                Debug.LogWarning("[WeaponConfig] " + name + " 未配置 damage，普攻将没有伤害。", this);
            if (energyCost != null && damage != null && energyCost.Length != damage.Length)
                Debug.LogWarning("[WeaponConfig] " + name + " 的 energyCost 与 damage 段数不一致。", this);
        }
    }
}
