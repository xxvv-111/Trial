using Game.Core;
using Game.Data;
using System;
using UnityEngine;
using UnityEngine.Windows;
using static UnityEngine.Rendering.DebugUI;

namespace Game.Gameplay
{
    public class PlayerAttack : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        public event Action<Vector3, int> OnHit;
        private float combopWindow;//连段窗口期
        private float attackRange;//判定距离
        private int[] attackDamage;//伤害

        private Animator _anim;
        private PlayerDash _dash;

        private int _comboIndex;//连击段数
        private float _lastAttackTime = -99f;//上次攻击时间
        private bool _cancombo;//是否下一段
        private bool _dead;//是否死亡
        public bool isAttacking;//是否处于攻击
        private int attackCount = 0;

        private void Awake()
        {
            combopWindow=config.comboWindow;
            attackRange = config.attackRange;
            attackDamage = config.attackDamage;
            _anim = GetComponent<Animator>();
            _dash = GetComponent<PlayerDash>();
        }

        private void Update()
        {
            //w6弃用
            //if (_dead) return;//死亡不攻击

            //if (_dash != null && _dash.IsDashing) return;//冲刺不攻击

            //if (InputService.Instance.AttackPressedThisFrame)//按下攻击
            //{
            //    if (InLocomotion())//待机或跑步
            //    {
            //        StartCombo();//开始连段
            //    }
            //    else if (_cancombo && _comboIndex < config.attackDamage.Length - 1 && ComboWindowOpen()) 
            //    {
            //        _comboIndex++;//下一连段
            //        _cancombo = false;//关闭连击等判定帧
            //        _lastAttackTime = Time.time;
            //        _anim.SetTrigger("Attack");
            //    }
            //}
        }
        public void StartCombo()//开始攻击,复用给FSM,私有改公有
        {
            _comboIndex = 0;
            _cancombo = false;

            _lastAttackTime = Time.time;
            //_anim.SetTrigger("Attack");w6
        }
        public void TryNextCombo()   // 原连段分支
        {
            if (_cancombo && _comboIndex < config.attackDamage.Length - 1 && ComboWindowOpen())
            {
                _comboIndex++;
                _cancombo = false;
                _lastAttackTime = Time.time;
                _anim.SetTrigger("Attack");
            }
        }

        private void OnAttackHit()//动画中触发的攻击事件
        {
            if (_dead) return;
            DoMeleeHit(_comboIndex);
            _cancombo = true;
            _lastAttackTime = Time.time;
        }

        private void DoMeleeHit(int combo)//处理受击
        {
            float radius = attackRange * (1 + combo * 0.05f);
            Vector3 center = transform.position + transform.forward * (radius * 0.5f);
            Collider[] hits = Physics.OverlapSphere(center, radius);
            foreach(Collider hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var target) && !hit.CompareTag("Player"))
                {
                    target.TakeDamage(attackDamage[combo]);
                    OnHit?.Invoke(hit.ClosestPoint(center), attackDamage[combo]);
                }
            }
        }

        private bool InLocomotion()//判断是否待机或跑步（w6弃用）
        {
            if (_anim == null) return true;
            return _anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
        }

        //询问连段窗口
        private bool ComboWindowOpen() => Time.time < _lastAttackTime + combopWindow;

        public bool IsAttacking()//外部判断连击
        {
            if (_anim == null) return false;
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            return st.IsName("Attack1") || st.IsName("Attack2") || st.IsName("Attack3") || st.IsName("Attack4");
        }

        public void OnPlayerDied() => _dead = true;//死亡时调用

        //绘制攻击范围
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 center = transform.position + transform.forward * config.attackRange;
            Gizmos.DrawWireSphere(center, config.attackRange);
        }

        //置攻击，动画状态机事件调用
        public void SetAttacking(bool b)
        {
            if (b) attackCount++;
            else attackCount--;

            if (attackCount == 0) isAttacking = false;
            else isAttacking = true;
        }

        //PlayerFSM调用exit
        public void CancelAttack()
        {
            _comboIndex = 0;
            _cancombo = false;
            attackCount = 0;
            isAttacking = false;
        }
    }
}
