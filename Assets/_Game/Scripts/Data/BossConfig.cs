using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Boss（Crichton）专用数值。**继承 <see cref="EnemyAIConfig"/>**，这样
    /// <see cref="Game.Gameplay.EnemyAIController"/> 里那一整套 getter
    /// （<c>AggroRange</c> / <c>AttackRange</c> / <c>MoveSpeed</c> / <c>Damage</c> / <c>FlashTime</c> …）
    /// 全部直接复用，不必重写一遍。这里只放 **Boss 独有的数值**。
    ///
    /// 复用的继承字段（在 Inspector 的「敌人配置」区块里）：
    /// <list type="bullet">
    ///   <item><c>hp</c> = Boss 血量（GDD §7.1 定 600）</item>
    ///   <item><c>aggroRange</c> = **战斗范围**（进战斗 / 脱战判据）</item>
    ///   <item><c>attackRange</c> = **攻击范围**</item>
    ///   <item><c>moveSpeed</c> = 追击速度（也决定绕圈侧移的线速度）</item>
    ///   <item><c>damage</c> = 每次攻击的伤害</item>
    ///   <item><c>flashTime</c> = 受击闪白时长</item>
    ///   <item><c>canBeInterrupted</c> = **必须 false**（Boss 不会被普通受击打断，只被破韧打断）</item>
    /// </list>
    ///
    /// ⚠️ **口径变更记录**：GDD §6.1 / §7.3 原文写「不引入硬直条 / 破防机制，Stagger 状态已移除」。
    ///    本配置按**新需求**引入了「韧性条（破韧）」，属于对原口径的修改 ——
    ///    ✅ **2026-10-15 已同步更新** <c>Docs/GDD.md</c>（§6.1 / §7.1 / §7.2 / §7.3 / §7.4 / §15.2 / §16）
    ///    与 <c>Docs/PHASE1-DESIGN.md</c>（§5.4.1~§5.4.4 / §5.5 / §6）。
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "Game/BossConfig")]
    public class BossConfig : EnemyAIConfig
    {
        // ====================================================================
        [Header("Boss · 脱战")]
        [Tooltip("玩家水平距离超出「战斗范围」（= 继承的 aggroRange）并持续这么久之后，脱战回 N_idle。\n" +
                 "0 = 永不脱战（会一直贴在玩家附近绕圈）。")]
        public float disengageTime = 4f;

        // ====================================================================
        [Header("Boss · 移动 / 转向")]
        [Tooltip("转身速度（度/秒）。Boss 体量大，建议明显小于小怪的默认 720 —— 否则绕背时像陀螺。")]
        public float turnSpeed = 240f;

        [Tooltip("攻击冷却期间「绕玩家侧移」（walkleft 动画）的半径（米）。\n" +
                 "0 = 不绕圈，CD 期间原地站住面向玩家。")]
        public float strafeRadius = 5f;

        [Tooltip("绕圈方向是否每次随机（true）/ 固定顺时针（false）。随机能避免看起来像在跑圈圈。")]
        public bool randomStrafeDirection = true;

        // ====================================================================
        [Header("Boss · 普通攻击冷却")]
        [Tooltip("普通攻击 CD 的下限（秒）。⭐ 计时**从进入攻击动画的第一帧开始**，不是在动画播完之后 —— " +
                 "这样即使动画很长、或被破韧打断，CD 也不会被漏掉，避免「卡攻击」。")]
        public float attackCdMin = 1.5f;

        [Tooltip("普通攻击 CD 的上限（秒）。每次攻击后取 [attackCdMin, attackCdMax] 的随机值。")]
        public float attackCdMax = 3f;

        [Tooltip("普通攻击的**兜底超时**（秒）。若动画在此时长内没有自然播完" +
                 "（例如动画没接上 / 被误设成循环），强制结束本动作并回到战斗待机，避免永久卡住。\n" +
                 "建议 = 最长的攻击动画时长 + 0.5。")]
        public float attackTimeout = 4f;

        // ====================================================================
        [Header("Boss · 三选一攻击（a / b / c）")]
        [Tooltip("选中「a」的权重。三个权重都是 0 时会退化成等概率。")]
        public float attackWeightA = 1f;
        [Tooltip("选中「b」的权重。b 是二连段的起手（见下方 comboChance）。")]
        public float attackWeightB = 1f;
        [Tooltip("选中「c」的权重。c 既可以被单独选中，也是 b→bc 二连段的第二段。")]
        public float attackWeightC = 1f;

        [Tooltip("判定体开启后持续多少秒自动关闭。需与每段攻击动画的判定窗口匹配。")]
        public float attackHitboxActiveTime = 0.25f;

        // ====================================================================
        [Header("Boss · b → bc 二连段")]
        [Range(0f, 1f)]
        [Tooltip("选中 b 之后，接第二段 c 的概率。1 = 每次都接（bc 二连），0 = b 永远单独出。")]
        public float comboChance = 0.4f;

        [Tooltip("二连段的第二段 c 开判定体后持续多少秒（通常比单出 c 短一点）。")]
        public float comboHitboxActiveTime = 0.25f;

        // ====================================================================
        [Header("Boss · 特殊攻击")]
        [Tooltip("特殊攻击 CD（秒）。⭐ 与普通攻击 CD **独立**计时，同样从进入动画的第一帧开始。\n" +
                 "触发条件 = **这个 CD 和普通攻击 CD 同时为 0**。")]
        public float specialCd = 14f;

        [Tooltip("特殊攻击的兜底超时（秒）。同上，防止动画没接上导致卡死。")]
        public float specialTimeout = 6f;

        [Tooltip("距离判定阈值（水平距离，米）：≤ 此值走**近距离**特殊攻击 sa1（原地不移动）；\n" +
                 "> 此值走**远距离** sa2（先追到玩家身前 specialFarStopDistance 处再打）。")]
        public float specialNearMaxDistance = 6f;

        [Tooltip("sa2 追击到「玩家身前」多远停下（米）。")]
        public float specialFarStopDistance = 3.5f;

        [Tooltip("sa2 **追击段的时长**（秒）。⚠️ 因为 sa2 的「追击 + 攻击」在同一个动画里、" +
                 "而本工程根运动是全局关闭的（位移全部由代码驱动），" +
                 "所以这段位移由代码在这段时间内推进 —— 做动画时这一段**不要放位移**，只做姿态。")]
        public float specialApproachTime = 0.9f;

        [Tooltip("sa2 追击段的移动速度（米/秒）。一般比普通追击快，做出「突进」的感觉。")]
        public float specialApproachSpeed = 7f;

        [Tooltip("sa2 追击段是否**面朝玩家**直线推进（true）；false = 朝行进方向（背对玩家冲过去）。")]
        public bool specialKeepFacingPlayer = true;

        [Tooltip("特殊攻击判定体开启后持续多少秒。")]
        public float specialHitboxActiveTime = 0.35f;

        // ====================================================================
        [Header("Boss · 韧性 / 破韧")]
        [Tooltip("韧性条上限。伤害 × poisePerDamageMultiplier 之后累加到这里，满了就破韧。")]
        public float maxPoise = 100f;

        [Tooltip("⭐ 每点伤害折算成多少点削韧。需求里是「受击时同时受到伤害两倍的破韧值」⇒ 填 2。")]
        public float poisePerDamageMultiplier = 2f;

        [Tooltip("韧性每秒自然恢复量。0 = 不自动恢复（推荐先填 0，手感更可控）。")]
        public float poiseRecoverPerSecond = 0f;

        [Tooltip("最后一次受击之后，隔多少秒才开始自然恢复韧性（避免边打边回）。")]
        public float poiseRecoverDelay = 2f;

        [Tooltip("破韧硬直（hit 动画）的兜底超时（秒）。")]
        public float hitTimeout = 3f;

        [Tooltip("破韧后到能再次被削韧之间的无敌期（秒）。0 = 不设（连续硬直＝被打成木桩，不建议）。")]
        public float poiseBreakImmunityTime = 0.6f;

        // ====================================================================
        [Header("Boss · 调试")]
        [Tooltip("在 Scene 视图画出战斗范围 / 攻击范围 / 特殊攻击距离阈值。")]
        public bool drawGizmos = true;
    }
}
