using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 普攻与连段（4 段）。
    ///
    /// **判定方式（GDD §8）**：动画关键帧事件 <c>OnAttackHit</c> 挂在本类上，
    /// 事件只负责**开启对应段落的判定体**；真正的命中结算由
    /// <see cref="HitboxController"/> 统一收口（过滤 → 扣血 → 播特效）。
    ///
    /// ⚠️ 与旧实现的区别：旧版是"事件里瞬时 `OverlapSphere` 采样一次"（**漏帧即丢判定**）；
    ///    现在是"判定体在一段时间窗内**真实存在**"。
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [Tooltip("角色配置：仅在未装配武器时作为兜底。伤害/连段窗口等**已迁到 WeaponConfig**。")]
        [SerializeField] private PlayerConfig config;

        [Tooltip("当前武器（M2.1）。由 M2.2 的 WeaponManager 装配；为空时回退到 PlayerConfig 的旧字段。")]
        [SerializeField] private WeaponConfig _weapon;

        [Tooltip("判定体开启后持续多久自动关闭（秒）。\n" +
                 "⚠️ 动画上目前只有「命中帧」一个事件，所以用时长收敛判定窗口。\n" +
                 "若要精确控制，可在动画末尾再挂一个事件调用 HitboxController.DisableHitbox。")]
        [SerializeField] private float _hitboxActiveTime = 0.25f;

        /// <summary>判定体命名前缀：第 i 段（0 基）对应 <c>Hitbox_Attack{i+1}</c>。</summary>
        private const string HitboxNamePrefix = "Hitbox_Attack";

        private float combopWindow;//连段窗口期
        private int comboLength;//本武器普攻段数

        private Animator _anim;
        private HitboxController _hitboxes;

        private int _comboIndex;//连击段数
        private float _lastAttackTime = -99f;//上次攻击时间

        /// <summary>段序号自增计数（每次开新的一段 +1）。见 <see cref="SegmentId"/>。</summary>
        private int _segmentId;

        /// <summary>本段（这一刀）开始的时刻 —— 前冲位移的时间轴起点。见 <see cref="SegmentStartTime"/>。</summary>
        private float _segmentStartTime;
        private bool _cancombo;//是否可接下一段
        private bool _dead;//是否死亡（⚠️ 当前无调用方，死亡链路待统一，见 T8）

        public bool isAttacking;//是否处于攻击状态（由 AttackStateBehaviour 驱动）

        /// <summary>
        /// ⭐ **本段攻击是否已经出手**（= 命中帧 <see cref="OnAttackHit"/> 是否已过）。
        ///
        /// 用途：方案 B 的「转向窗口」——**出手前可以调整朝向，出手后朝向锁定**
        /// （由 <see cref="PlayerMotor"/> 在"只转向"模式下读取 <see cref="CanTurn"/>）。
        ///
        /// ⚠️ 每段独立计：<see cref="StartCombo"/> 与 <see cref="TryNextCombo"/> 都会清零，
        ///    所以在一次 4 段连段里，**每一段都有自己的窗口**。
        ///
        /// ⚠️ **失败时是"放行"而不是"锁死"**：若某段动画忘了挂 `OnAttackHit` 事件，
        ///    本标志会一直为 false → 该段全程可转向（宁可松，也不要让玩家突然转不动）。
        /// </summary>
        private bool _hitThisSegment;

        /// <summary>
        /// 当前是否**允许**靠输入调整朝向（方案 B 的窗口判据）。
        /// ⭐ 刻意**不**再叠加 <see cref="isAttacking"/> —— 那个由 `AttackStateBehaviour` 在动画状态
        /// enter/exit 时驱动，而 Attack 的过渡本身有 0.05~0.25 s 混合期，会让窗口起点不可控。
        /// 外层是否处于攻击由 <see cref="PlayerFSM"/> 的 Attack 状态保证。
        /// </summary>
        public bool CanTurn { get { return !_hitThisSegment; } }

        /// <summary>
        /// ⭐ 本段（这一刀）的**自增编号** —— 每开新的一段都 +1。
        ///
        /// 用途：<see cref="PlayerMotor"/> 靠它判断"换段了没有"，从而把前冲进度**归零重算**
        /// （每段各自从头开始算位移，而不是连着上一段继续累加）。
        ///
        /// ⚠️ 刻意用自增计数、**而不是**时间戳：`_lastAttackTime` 在**命中帧**
        ///    （见 <see cref="OnAttackHit"/>）也会被刷新，拿它当"段边界"会让前冲进度在中途被重置。
        /// </summary>
        public int SegmentId { get { return _segmentId; } }

        /// <summary>本段（这一刀）开始的时刻 —— 前冲位移的时间轴起点。</summary>
        public float SegmentStartTime { get { return _segmentStartTime; } }

        /// <summary>当前连段段号（0 基）。武器配置里"每段的数值"都按它取。</summary>
        public int ComboIndex { get { return _comboIndex; } }

        /// <summary>当前武器（可能为 null = 未装配）。</summary>
        public WeaponConfig Weapon { get { return _weapon; } }

        private int attackCount;//驱动 isAttacking 的引用计数

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _hitboxes = GetComponent<HitboxController>();

            ResolveWeaponData();
        }

        /// <summary>
        /// 解析武器数值。**武器优先，PlayerConfig 兜底** ——
        /// 这样 M2.1 不会破坏"尚未装配武器"时的可玩性，M2.2 装上武器后自动切换。
        /// </summary>
        private void ResolveWeaponData()
        {
            if (_weapon != null)
            {
                combopWindow = _weapon.comboWindow;
                comboLength = Mathf.Max(1, _weapon.ComboLength);

                //把武器配置的判定盒尺寸应用到对应判定体（按名字匹配）
                if (_hitboxes != null) _hitboxes.ApplyWeaponShapes(_weapon, HitboxNamePrefix);
                return;
            }

            //兜底：沿用 PlayerConfig（旧行为），段数按判定体数量推断
            combopWindow = config != null ? config.comboWindow : 1f;
            comboLength = (config != null && config.attackDamage != null && config.attackDamage.Length > 0)
                ? config.attackDamage.Length : 4;
        }

        /// <summary>换武器（M2.2 用）。重新解析数值并刷新判定盒尺寸。</summary>
        public void SetWeapon(WeaponConfig weapon)
        {
            _weapon = weapon;
            ResolveWeaponData();
        }

        /// <summary>某一段（0 基）的伤害。武器优先、PlayerConfig 兜底。</summary>
        private int DamageAt(int comboIndex)
        {
            if (_weapon != null) return _weapon.GetDamage(comboIndex);
            if (config == null || config.attackDamage == null || config.attackDamage.Length == 0) return 0;
            return config.attackDamage[Mathf.Clamp(comboIndex, 0, config.attackDamage.Length - 1)];
        }

        /// <summary>
        /// 开始连段第 1 段。由 PlayerFSM 在进入 Attack 状态时调用。
        /// ⚠️ 2026-10-06 起**普攻不消耗任何资源**（原体力扣除已删除），本方法只管重置连段计数。
        /// </summary>
        public void StartCombo()
        {
            _comboIndex = 0;
            _cancombo = false;
            _hitThisSegment = false;//★ 新的一段开始 → 转向窗口重新打开（方案 B）
            _segmentId++;//★ 段序号 +1 → PlayerMotor 据此把前冲进度归零
            _segmentStartTime = Time.time;
            _lastAttackTime = Time.time;
        }

        /// <summary>
        /// 尝试接下一段。连段窗口内且未超段数才生效。
        /// ⚠️ 2026-10-06 规则变更：**普攻不再消耗资源**，原来"体力不足则该段不接续"的判断已删除，
        ///    现在只要在连段窗口内就能一直接下去。
        ///
        /// ⭐ 2026-10-07：**返回是否真的接上了** —— 供输入缓冲判断"要不要消费这次按键"。
        ///    返回 false（窗口没开 / 已到末段）时，按键会**留在缓冲里**等下一次机会，
        ///    这样玩家在后摇里提前按下也能接上。
        /// </summary>
        public bool TryNextCombo()
        {
            if (!_cancombo || _comboIndex >= comboLength - 1 || !ComboWindowOpen())
                return false;

            _comboIndex++;
            _cancombo = false;
            _hitThisSegment = false;//★ 进入下一段 → 该段自己的转向窗口重新打开（方案 B）
            _segmentId++;//★ 换段 → 前冲进度归零、从头重算
            _segmentStartTime = Time.time;
            _lastAttackTime = Time.time;
            _anim.SetTrigger("Attack");
            return true;
        }

        /// <summary>
        /// 动画关键帧事件（挂在 combo_01_1~4 上）。
        /// 这是"判定开启帧"——GDD §8 要求判定体在**关键动作帧**期间存在。
        /// ⭐ 同时也是**转向窗口的关闭点**（方案 B）：出手之后朝向锁定，判定体与视觉因此不会错位。
        /// </summary>
        private void OnAttackHit()
        {
            if (_dead) return;

            _hitThisSegment = true;//★ 已出手 → 本段不能再转向（方案 B）

            //命中判定体由 HitboxController 负责；伤害值在 M2.1 已改由武器配置提供。
            //⚠️ 判定体的 _damage 是预制体上的序列化值 —— 换武器后需要同步（见 SyncHitboxDamage）。
            SyncHitboxDamage();

            if (_hitboxes != null)
                _hitboxes.EnableHitbox(CurrentHitboxName(), _hitboxActiveTime);

            _cancombo = true;
            _lastAttackTime = Time.time;
        }

        /// <summary>
        /// 把当前武器的伤害同步到判定体上（M2.1）。
        ///
        /// 为什么需要：判定体的伤害存在它自己的序列化字段里（M1.2 的设计），
        /// 而换武器会改变伤害 —— 两者必须保持一致，否则"换了武器伤害没变"。
        /// </summary>
        private void SyncHitboxDamage()
        {
            if (_weapon == null || _hitboxes == null) return;

            Hitbox hb = _hitboxes.GetByName(CurrentHitboxName());
            if (hb != null) hb.SetDamage(_weapon.GetDamage(_comboIndex));
        }

        /// <summary>本段对应的判定体名（判定体挂在 Player 预制体下，名为 Hitbox_Attack1~4）。</summary>
        private string CurrentHitboxName()
        {
            int i = Mathf.Clamp(_comboIndex, 0, Mathf.Max(0, comboLength - 1)) + 1;
            return HitboxNamePrefix + i;
        }

        /// <summary>连段窗口是否还开着。</summary>
        private bool ComboWindowOpen() { return Time.time < _lastAttackTime + combopWindow; }

        /// <summary>⚠️ 当前无调用方（原调用方 PlayerHealth.Die 已被注释），待统一死亡链路（T8）。</summary>
        public void OnPlayerDied() { _dead = true; }

        /// <summary>攻击动画状态 enter / exit 时由 AttackStateBehaviour 调用（引用计数）。</summary>
        public void SetAttacking(bool b)
        {
            attackCount += b ? 1 : -1;
            isAttacking = attackCount > 0;
        }

        /// <summary>供 PlayerFSM 在离开 Attack 状态时调用：收招/被中断时清掉残留判定。</summary>
        public void CloseAllHitboxes()
        {
            if (_hitboxes != null) _hitboxes.DisableAllHitboxes();
        }
    }
}
