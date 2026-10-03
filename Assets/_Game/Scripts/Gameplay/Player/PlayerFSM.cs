using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Runtime.CompilerServices;
using Game.Core;
using System.Threading;

namespace Game.Gameplay
{
    public class PlayerFSM : MonoBehaviour,IDamageable
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
                _invulnTimer = 0.8f;//0.8秒无敌
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
                if (InputService.Instance.AttackPressedThisFrame)//连段
                    _attack.TryNextCombo();
                if (IsAttackAnimOver())
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
                if (InputService.Instance.DashPressedThisFrame && !_dash.IsDashing)
                    Change(PlayerState.Dash);
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
            if (InputService.Instance.DashPressedThisFrame && !_dash.IsDashing)
            {
                Change(PlayerState.Dash);
                return;
            }
            if(InputService.Instance.AttackPressedThisFrame)
            {
                Change(PlayerState.Attack);
                return;
            }
            if (HasMoveInput() && State != PlayerState.Run)
            {
                Change(PlayerState.Run);
            }
            else if (!HasMoveInput() && State != PlayerState.Idle)
                Change(PlayerState.Idle);
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

        //是否有攻击输入
        private bool IsAttackAnimOver() => !_attack.isAttacking;

        public void TakeDamage(int dmg)
        {
            if (Invulnerable || Dead) return;
            _health.ApplyDamage(dmg);
            Change(_health.IsDead ? PlayerState.Death : PlayerState.Hit);
        }
    }
}