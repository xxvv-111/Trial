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

        //特殊攻击 · 火球（M2.4）
        private float _specialCdTimer;//冷却剩余（秒）
        private float _castTimer;//本次施法已过时间
        private bool _fireballSpawned;//本次施法是否已出手（保证一次施法只出一颗）
        private bool _casting;//施法动画进行中 —— 由 SpecialStateBehaviour 驱动
        private bool _specialAnimStarted;//施法动画是否真的开始过（防"还没进动画就被判结束"）

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
            _enter[PlayerState.Special] = () =>
            {
                _motor.enabled = false;//施法停止移动
                _castTimer = 0f;
                _fireballSpawned = false;
                _casting = false;
                _specialAnimStarted = false;
                _anim.SetTrigger("Special");
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
                //攻击中可直接接特殊攻击（GDD §4.9：Idle / Run / Attack → Special）
                if (InputService.Instance.SpecialPressedThisFrame && TrySpecial()) return;

                //连段（体力检查在 PlayerAttack 内部，不足则该段不接续）
                if (InputService.Instance.AttackPressedThisFrame)
                    _attack.TryNextCombo();

                if (IsAttackAnimOver())
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);

                //攻击中可取消接冲刺；体力不足则保持攻击（TryDash 会拒绝）
                if (InputService.Instance.DashPressedThisFrame)
                    TryDash();
            };
            _update[PlayerState.Special] = () =>
            {
                if (_casting) _specialAnimStarted = true;

                //出手：施法到点 → 生成火球（一次施法只出一颗）
                if (!_fireballSpawned)
                {
                    float delay = SpecialCastDelay();
                    if (_castTimer >= delay)
                    {
                        SpawnFireball();
                        _fireballSpawned = true;
                    }
                }
                _castTimer += Time.deltaTime;

                //动画播完 → 回待机/跑；⚠️ 必须等动画真的开始过，否则 SetTrigger 当帧就会误判"已结束"
                if (_specialAnimStarted && !_casting)
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
                else if (_castTimer > 3f)//兜底：动画没接上时不至于永久卡在施法状态
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
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
            if (_specialCdTimer > 0f) _specialCdTimer -= Time.deltaTime;//特殊攻击冷却
            _update.GetValueOrDefault(State)?.Invoke();
        }

        //待机跑步状态
        private void UpdateNeutral()
        {
            //冲刺 / 攻击 / 特殊攻击都是"收费动作"：付得起体力才切状态
            if (InputService.Instance.DashPressedThisFrame && TryDash()) return;
            if (InputService.Instance.AttackPressedThisFrame && TryAttack()) return;
            if (InputService.Instance.SpecialPressedThisFrame && TrySpecial()) return;

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

        // ==================== 特殊攻击 · 火球（M2.4） ====================

        /// <summary>
        /// 尝试释放特殊攻击。顺序：**冷却 → 武器是否配了火球 → 体力**，任一不满足都不切状态。
        /// ⚠️ 体力走 <see cref="PlayerEnergy.TrySpendSpecial"/>（统一出口，成本读武器配置）。
        /// </summary>
        private bool TrySpecial()
        {
            if (_specialCdTimer > 0f) return false;

            var weapon = _attack != null ? _attack.Weapon : null;
            if (weapon == null || weapon.specialType != Game.Data.SpecialAttackType.Fireball) return false;
            if (weapon.specialProjectilePrefab == null) return false;

            if (!_energy.TrySpendSpecial()) return false;//付不起 → 不执行（PlayerEnergy 会报警）

            _specialCdTimer = weapon.specialCooldown;
            Change(PlayerState.Special);
            return true;
        }

        /// <summary>在角色身前生成一颗火球，并按武器配置发射。</summary>
        private void SpawnFireball()
        {
            var weapon = _attack != null ? _attack.Weapon : null;
            if (weapon == null || weapon.specialProjectilePrefab == null) return;

            Vector3 fwd = transform.forward;
            Vector3 pos = transform.position + Vector3.up * weapon.specialSpawnHeight + fwd * 0.4f;

            var go = Instantiate(weapon.specialProjectilePrefab, pos, Quaternion.LookRotation(fwd));
            var fb = go.GetComponent<Fireball>();
            if (fb == null)
            {
                Debug.LogWarning("[PlayerFSM] 火球预制体上没有 Fireball 组件：" + weapon.specialProjectilePrefab.name, go);
                return;
            }

            fb.Launch(fwd, weapon.specialProjectileSpeed, weapon.specialRange,
                      weapon.specialDamage, weapon.specialExplosionRadius, transform);
        }

        private float SpecialCastDelay()
        {
            var weapon = _attack != null ? _attack.Weapon : null;
            return weapon != null ? Mathf.Max(0f, weapon.specialCastDelay) : 0.55f;
        }

        /// <summary>由 <see cref="SpecialStateBehaviour"/> 在 `Special_Attack` 进入/退出时调用。</summary>
        public void SetCasting(bool value) { _casting = value; }

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
