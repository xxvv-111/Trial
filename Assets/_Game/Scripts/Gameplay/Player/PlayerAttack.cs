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
        [SerializeField] private PlayerConfig config;

        [Tooltip("判定体开启后持续多久自动关闭（秒）。\n" +
                 "⚠️ 动画上目前只有「命中帧」一个事件，所以用时长收敛判定窗口。\n" +
                 "若要精确控制，可在动画末尾再挂一个事件调用 HitboxController.DisableHitbox。")]
        [SerializeField] private float _hitboxActiveTime = 0.25f;

        private float combopWindow;//连段窗口期
        private int[] attackDamage;//伤害

        private Animator _anim;
        private PlayerEnergy _energy;
        private HitboxController _hitboxes;

        private int _comboIndex;//连击段数
        private float _lastAttackTime = -99f;//上次攻击时间
        private bool _cancombo;//是否可接下一段
        private bool _dead;//是否死亡（⚠️ 当前无调用方，死亡链路待统一，见 T8）

        public bool isAttacking;//是否处于攻击状态（由 AttackStateBehaviour 驱动）

        private int attackCount;//驱动 isAttacking 的引用计数

        private void Awake()
        {
            combopWindow = config.comboWindow;
            attackDamage = config.attackDamage;
            _anim = GetComponent<Animator>();
            _energy = GetComponent<PlayerEnergy>();
            _hitboxes = GetComponent<HitboxController>();
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
            if (!_cancombo || _comboIndex >= config.attackDamage.Length - 1 || !ComboWindowOpen())
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

            if (_hitboxes != null)
                _hitboxes.EnableHitbox(CurrentHitboxName(), _hitboxActiveTime);

            _cancombo = true;
            _lastAttackTime = Time.time;
        }

        /// <summary>本段对应的判定体名（判定体挂在 Player 预制体下，名为 Hitbox_Attack1~4）。</summary>
        private string CurrentHitboxName()
        {
            int i = Mathf.Clamp(_comboIndex, 0, 3) + 1;
            return "Hitbox_Attack" + i;
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
