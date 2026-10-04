using Game.Core;
using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 普攻与连段（4 段）。伤害由动画关键帧事件 <c>OnAttackHit</c> 触发。
    /// ⚠️ M1.2 待改造：把 <see cref="DoMeleeHit"/> 的瞬时 OverlapSphere 采样
    /// 换成帧驱动的 Hitbox 系统（GDD §8）。
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        /// <summary>(命中点, 伤害)。AttackFxBridge 订阅后转给 HitFxSystem。</summary>
        public event Action<Vector3, int> OnHit;

        private float combopWindow;//连段窗口期
        private float attackRange;//判定距离
        private int[] attackDamage;//伤害

        private Animator _anim;
        private PlayerEnergy _energy;

        private int _comboIndex;//连击段数
        private float _lastAttackTime = -99f;//上次攻击时间
        private bool _cancombo;//是否可接下一段
        private bool _dead;//是否死亡（⚠️ 当前无调用方，死亡链路待 M1.2 统一，见 T8）

        public bool isAttacking;//是否处于攻击状态（由 AttackStateBehaviour 驱动）

        private int attackCount;//驱动 isAttacking 的引用计数

        private void Awake()
        {
            combopWindow = config.comboWindow;
            attackRange = config.attackRange;
            attackDamage = config.attackDamage;
            _anim = GetComponent<Animator>();
            _energy = GetComponent<PlayerEnergy>();
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

        /// <summary>动画关键帧事件（挂在 combo_01_1~4 上）。</summary>
        private void OnAttackHit()
        {
            if (_dead) return;
            DoMeleeHit(_comboIndex);
            _cancombo = true;
            _lastAttackTime = Time.time;
        }

        /// <summary>⚠️ M1.2 待改造：瞬时采样 → 帧驱动的常驻判定体（GDD §8）。</summary>
        private void DoMeleeHit(int combo)
        {
            float radius = attackRange * (1 + combo * 0.05f);
            Vector3 center = transform.position + transform.forward * (radius * 0.5f);
            Collider[] hits = Physics.OverlapSphere(center, radius);

            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var target) && !hit.CompareTag("Player"))
                {
                    target.TakeDamage(attackDamage[combo]);
                    OnHit?.Invoke(hit.ClosestPoint(center), attackDamage[combo]);
                }
            }
        }

        /// <summary>连段窗口是否还开着。</summary>
        private bool ComboWindowOpen() => Time.time < _lastAttackTime + combopWindow;

        /// <summary>⚠️ 当前无调用方（原调用方 PlayerHealth.Die 已被注释），待 M1.2 统一死亡链路。</summary>
        public void OnPlayerDied() => _dead = true;

        /// <summary>攻击动画状态 enter / exit 时由 AttackStateBehaviour 调用（引用计数）。</summary>
        public void SetAttacking(bool b)
        {
            attackCount += b ? 1 : -1;
            isAttacking = attackCount > 0;
        }

        //在编辑器里可视化攻击范围
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 center = transform.position + transform.forward * config.attackRange;
            Gizmos.DrawWireSphere(center, config.attackRange);
        }
    }
}
