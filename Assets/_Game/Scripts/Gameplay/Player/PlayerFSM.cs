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
        private PlayerMana _mana;
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
            _mana = GetComponent<PlayerMana>();
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
                //⭐ 两条路线（由 PlayerMotor.attackDisplacement 决定）：
                //   · NativeRootMotion：**必须把组件禁用** —— 只有 OnAnimatorMove 不被调用，位移权才交还给 Animator。
                //     代价：攻击中完全不能转向、前冲不过碰撞体（会穿墙）、动画根曲线的回撤会原样播放。
                //   · 其余模式（代码前冲 / 劫持根运动 / 完全不动）：组件保持 enabled ——
                //     "只转向"与"贴地下压"都写在 Update / OnAnimatorMove 里，禁用会一起停掉。
                //     ⭐ 转向窗口由 PlayerMotor 内部判（默认方案 B：命中帧前可转、出手后锁定），
                //        本次 `StartCombo()` 会把窗口重新打开（每段一个自己的窗口）。
                if (_motor.UsesNativeRootMotion)
                    _motor.enabled = false;
                else
                {
                    _motor.enabled = true;
                    _motor.SetFacingOnly(true);
                }
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
                var input = InputService.Instance;

                //攻击中可直接接特殊攻击（GDD §4.9：Idle / Run / Attack → Special）
                if (input.HasBufferedSpecial && TrySpecial()) { input.ConsumeSpecial(); return; }

                //连段（2026-10-06 起普攻免费，窗口内就能一直接）
                //⭐ 2026-10-07 缓冲：窗口**还没开**时 TryNextCombo 返回 false ⇒ 按键**不消费**、留在缓冲里，
                //   等窗口一开（命中帧那一刻）立刻接上 —— 这就是「提前按也算数」的实现。
                if (input.HasBufferedAttack && _attack.TryNextCombo()) input.ConsumeAttack();

                if (IsAttackAnimOver())
                    Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);

                //攻击中可取消接冲刺（冲刺已免费，随时可接）
                if (input.HasBufferedDash && TryDash()) input.ConsumeDash();
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

            //离开攻击状态时：清掉残留判定体（收招或被受击打断都要清）+ **复位"只转向"模式**
            //⚠️ 复位绝对不能漏 —— 漏了就是"之后只能转身、跑不动"。
            _exit[PlayerState.Attack] = () =>
            {
                _attack.CloseAllHitboxes();
                _motor.SetFacingOnly(false);
                //⚠️ 防御：NativeRootMotion 模式下组件是被禁用的，**离开攻击时必须恢复**。
                //   （Idle / Run / Dash 的 _enter 里本来也会 `enabled = true`，这里再兜一道 ——
                //     避免将来有人在 Attack 与某个"不碰 enabled"的状态之间直连时漏掉，那会是"打一次后再也动不了"。）
                _motor.enabled = true;
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
            //⭐ 2026-10-07 改走**输入缓冲**：后摇 / 硬直期间按下的键会被缓存，能执行时立刻补执行。
            //   统一写法：**先问缓冲 → 执行成功 → 才消费**（失败就把按键留在缓冲里等下一次机会）。
            var input = InputService.Instance;
            if (input.HasBufferedDash && TryDash()) { input.ConsumeDash(); return; }
            if (input.HasBufferedAttack && TryAttack()) { input.ConsumeAttack(); return; }
            if (input.HasBufferedSpecial && TrySpecial()) { input.ConsumeSpecial(); return; }

            if (HasMoveInput() && State != PlayerState.Run)
            {
                Change(PlayerState.Run);
            }
            else if (!HasMoveInput() && State != PlayerState.Idle)
                Change(PlayerState.Idle);
        }

        /// <summary>
        /// 尝试冲刺。⚠️ 2026-10-06 规则变更：**冲刺不再消耗资源**（原体力消耗已删除），
        /// 所以除了"正在冲刺"之外没有别的门槛。
        /// </summary>
        private bool TryDash()
        {
            if (_dash.IsDashing) return false;

            Change(PlayerState.Dash);
            return true;
        }

        /// <summary>尝试起手普攻（第 1 段）。⚠️ 普攻同样**不再消耗资源**。</summary>
        private bool TryAttack()
        {
            Change(PlayerState.Attack);
            return true;
        }

        // ==================== 特殊攻击 · 火球（M2.4） ====================

        /// <summary>
        /// 尝试释放特殊攻击。顺序：**冷却 → 武器是否配了火球 → 魔力**，任一不满足都不切状态。
        /// ⚠️ 魔力走 <see cref="PlayerMana.TrySpendSpecial"/>（统一出口，成本读武器配置）。
        /// ⭐ 这是**唯一**消耗资源的动作（冲刺与普攻已免费）。
        /// </summary>
        private bool TrySpecial()
        {
            if (_specialCdTimer > 0f) return false;

            var weapon = _attack != null ? _attack.Weapon : null;
            if (weapon == null || weapon.specialType != Game.Data.SpecialAttackType.Fireball) return false;
            if (weapon.specialProjectilePrefab == null) return false;

            if (!_mana.TrySpendSpecial()) return false;//付不起 → 不执行（PlayerMana 会报警）

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
