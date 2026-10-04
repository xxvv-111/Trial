using System.Collections.Generic;
using UnityEngine;
using System;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>玩家状态机（三字典驱动）。加状态只需加表项。</summary>
    public class PlayerFSM : MonoBehaviour, IDamageable
    {
        //当前状态
        public PlayerState State { get; private set; } = PlayerState.Idle;

        //三个字典
        private readonly Dictionary<PlayerState, Action> _enter = new();//进入状态
        private readonly Dictionary<PlayerState, Action> _update = new();//状态中要做的事
        private readonly Dictionary<PlayerState, Action> _exit = new();//退出状态

        //其余组件
        private PlayerMotor _motor;
        private PlayerDash _dash;
        private PlayerAttack _attack;
        private PlayerHealth _health;
        private PlayerEnergy _energy;
        private Animator _anim;
        private float _invulnTimer;//受击无敌时间

        //对外，是否无敌
        public bool Invulnerable => _invulnTimer > 0f || (_dash != null && _dash.IsInvulnerable);
        public bool Dead => State == PlayerState.Death;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _dash = GetComponent<PlayerDash>();
            _attack = GetComponent<PlayerAttack>();
            _health = GetComponent<PlayerHealth>();
            _energy = GetComponent<PlayerEnergy>();
            _anim = GetComponent<Animator>();

            //进场enter
            _enter[PlayerState.Idle] = () => { _motor.enabled = true; };
            _enter[PlayerState.Run] = () => { _motor.enabled = true; };
            _enter[PlayerState.Dash] = () =>
            {
                _motor.enabled = false;//冲刺停止移动
                _dash.BeginDash();
                _anim.SetTrigger("Dash");
            };
            _enter[PlayerState.Attack] = () =>
            {
                _motor.enabled = false;//攻击停止移动
                _attack.StartCombo();
                _anim.SetTrigger("Attack");
            };
            _enter[PlayerState.Hit] = () =>
            {
                _motor.enabled = false;
                _invulnTimer = _health.HitInvulnTime;//受击无敌时长（走配置，见 PlayerConfig.hitInvulnTime）
                _anim.SetTrigger("Hit");
            };
            _enter[PlayerState.Death] = () =>
            {
                _motor.enabled = false;
                _dash.enabled = false;
                _anim.applyRootMotion = true;
                _anim.SetBool("IsDead", true);

                GameEvents.RaisePlayerDied();
            };

            //每个状态的update
            _update[PlayerState.Idle] = UpdateNeutral;
            _update[PlayerState.Run] = UpdateNeutral;
            _update[PlayerState.Dash] = () =>
            {
                if (!_dash.IsDashing) Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
            };
            _update[PlayerState.Attack] = () =>
            {
                //连段（体力检查在 PlayerAttack 内部，不足则该段不接续）
                if (InputService.Instance.AttackPressedThisFrame)
                    _attack.TryNextCombo();

                if (IsAttackAnimOver())
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);

                //攻击中可取消接冲刺；体力不足则保持攻击（TryDash 会拒绝）
                if (InputService.Instance.DashPressedThisFrame)
                    TryDash();
            };
            _update[PlayerState.Hit] = () =>
            {
                if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;
                if (_invulnTimer <= 0f) Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
            };

            _exit[PlayerState.Dash] = () =>
            {
                _dash.EndDash();
            };

            //离开攻击状态时清掉残留判定体（收招或被受击打断都要清）
            _exit[PlayerState.Attack] = () =>
            {
                _attack.CloseAllHitboxes();
            };
        }

        private void Start()
        {
            Change(PlayerState.Idle);
        }

        private void Update()
        {
            _update.GetValueOrDefault(State)?.Invoke();
        }

        //待机跑步状态
        private void UpdateNeutral()
        {
            //冲刺 / 攻击都是"收费动作"：付得起体力才切状态
            if (InputService.Instance.DashPressedThisFrame && TryDash()) return;
            if (InputService.Instance.AttackPressedThisFrame && TryAttack()) return;

            if (HasMoveInput() && State != PlayerState.Run)
            {
                Change(PlayerState.Run);
            }
            else if (!HasMoveInput() && State != PlayerState.Idle)
                Change(PlayerState.Idle);
        }

        /// <summary>
        /// 尝试冲刺：**先付体力，付得起才切状态**（GDD §4.8：消耗 > 当前体力则不执行）。
        /// 体力不足时 PlayerEnergy 会触发 OnSpendFailed 供 HUD 报警。
        /// </summary>
        private bool TryDash()
        {
            if (_dash.IsDashing) return false;
            if (!_energy.TrySpendDash()) return false;

            Change(PlayerState.Dash);
            return true;
        }

        /// <summary>尝试起手普攻（第 1 段）。同样先付体力。</summary>
        private bool TryAttack()
        {
            if (!_energy.TrySpendAttack(0)) return false;

            Change(PlayerState.Attack);
            return true;
        }

        //换状态
        public void Change(PlayerState next)
        {
            if (State == PlayerState.Death) return;   //死后不接受任何状态切换
            if (next == State) return;
            _exit.GetValueOrDefault(State)?.Invoke();                   //旧状态停
            State = next;
            _enter.GetValueOrDefault(State)?.Invoke();
        }

        //是否有移动输入
        private bool HasMoveInput() => InputService.Instance.Move.sqrMagnitude > 0.01f;

        //攻击动画是否播完
        private bool IsAttackAnimOver() => !_attack.isAttacking;

        public void TakeDamage(int dmg)
        {
            if (Invulnerable || Dead) return;
            _health.ApplyDamage(dmg);
            Change(_health.IsDead ? PlayerState.Death : PlayerState.Hit);
        }
    }
}
