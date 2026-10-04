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
        private PlayerEnergy _energy;
        private HitboxController _hitboxes;

        private int _comboIndex;//连击段数
        private float _lastAttackTime = -99f;//上次攻击时间
        private bool _cancombo;//是否可接下一段
        private bool _dead;//是否死亡（⚠️ 当前无调用方，死亡链路待统一，见 T8）

        public bool isAttacking;//是否处于攻击状态（由 AttackStateBehaviour 驱动）

        /// <summary>当前武器（可能为 null = 未装配）。</summary>
        public WeaponConfig Weapon { get { return _weapon; } }

        private int attackCount;//驱动 isAttacking 的引用计数

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _energy = GetComponent<PlayerEnergy>();
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
        /// ⚠️ 第 1 段的体力消耗由 **PlayerFSM 在切换状态前**扣除（见 PlayerFSM.TryAttack），
        ///    本方法只管重置连段计数。
        /// </summary>
        public void StartCombo()
        {
            _comboIndex = 0;
            _cancombo = false;
            _lastAttackTime = Time.time;
        }

        /// <summary>
        /// 尝试接下一段。连段窗口内且未超段数才生效。
        /// ⚠️ 体力不足则该段**不接续**（前一段正常播完）—— GDD §4.8 连段细则。
        /// </summary>
        public void TryNextCombo()
        {
            if (!_cancombo || _comboIndex >= comboLength - 1 || !ComboWindowOpen())
                return;

            int next = _comboIndex + 1;

            //体力不足 → 放弃接续（PlayerEnergy 会触发 OnSpendFailed 供 HUD 报警）
            if (_energy != null && !_energy.TrySpendAttack(next)) return;

            _comboIndex = next;
            _cancombo = false;
            _lastAttackTime = Time.time;
            _anim.SetTrigger("Attack");
        }

        /// <summary>
        /// 动画关键帧事件（挂在 combo_01_1~4 上）。
        /// 这是"判定开启帧"——GDD §8 要求判定体在**关键动作帧**期间存在。
        /// </summary>
        private void OnAttackHit()
        {
            if (_dead) return;

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
