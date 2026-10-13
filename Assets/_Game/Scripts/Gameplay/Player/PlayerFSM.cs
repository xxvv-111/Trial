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
        private PlayerJump _jump;
        private PlayerAttack _attack;
        private PlayerHealth _health;
        private PlayerMana _mana;
        private Animator _anim;
        private float _invulnTimer;//受击无敌时间

        // ==================== 跑跳动画的镜像（2026-10-13）====================
        //
        // 跑跳 clip（`P1Kimodo_run_jump`）本身是一个"**右脚起跳**"的动作 ——
        // 实测 0.167 s 起右脚前伸、整个滞空段右脚都在前。而跑步是左右脚交替的，
        // 所以跑步时若左脚在前还播原版，起跳瞬间的脚会"突"地换一只。
        //
        // 解决办法：Animator 的 `P1Kimodo_run_jump` 状态勾了 **Mirror Parameter**（指向 `JumpMirror`），
        // 由本类在起跳那一帧写入 —— **镜像后 = 左脚起跳**，与步态对上。

        /// <summary>
        /// Animator 里驱动 `P1Kimodo_run_jump` 镜像的 Bool 参数名。
        /// ⚠️ 必须与 `Player1_AC.controller` 上那个参数**逐字一致**（状态 → Mirror → Parameter 里选的就是它）。
        /// </summary>
        private const string JumpMirrorParam = "JumpMirror";

        /// <summary>判"哪只脚在前"用的三根骨骼。**在 Awake 里缓存**（Humanoid 骨骼运行时不会变）。</summary>
        private Transform _hipsBone;
        private Transform _leftFootBone;
        private Transform _rightFootBone;

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
            _jump = GetComponent<PlayerJump>();
            _attack = GetComponent<PlayerAttack>();
            _health = GetComponent<PlayerHealth>();
            _mana = GetComponent<PlayerMana>();
            _anim = GetComponent<Animator>();

            //缓存"哪只脚在前"要用的骨骼（跑跳镜像用）。
            //⚠️ 取不到也不影响运行 —— IsLeftFootForward() 会退化成"不镜像"，即退回旧行为。
            if (_anim != null)
            {
                _hipsBone = _anim.GetBoneTransform(HumanBodyBones.Hips);
                _leftFootBone = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFootBone = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            //进场enter
            _enter[PlayerState.Idle] = () => { _motor.enabled = true; };
            _enter[PlayerState.Run] = () => { _motor.enabled = true; };
            _enter[PlayerState.Dash] = () =>
            {
                _motor.enabled = false;//冲刺停止移动
                _dash.BeginDash();
                _anim.SetTrigger("Dash");
            };
            _enter[PlayerState.Jump] = () =>
            {
                //⭐ 结构与 Dash 完全同款：停地面移动，位移交给 PlayerJump（LateUpdate 里走 CharacterController）。
                //   区别只有两点：① 跳跃没有无敌帧（移动技，不是闪避技）；
                //   ② PlayerJump 用的是「不贴地」的 StepMove —— 贴地下压会把人拽回地面。
                //
                // ⭐ 2026-10-13：按**起跳瞬间的档位**二选一播动画，并把当时的移动速度一并交给 PlayerJump：
                //   · 跑步档           → RunJump（跑跳），空中保持跑速前冲；
                //   · 其余（站 / 走路）→ Jump  （站跳），速度跟着走速 —— 静止起跳就是 0，原地上下。
                //   ⚠️ 必须先读 `_motor.IsRunning / CurrentSpeed` **再**禁用组件（顺序写反容易误以为
                //      "禁用后读到的是 0"），位移速度就取自这里。
                bool running = _motor.IsRunning;
                float takeoffSpeed = _motor.CurrentSpeed;

                //⭐⭐ 跑跳再按**起跳瞬间哪只脚在前**决定要不要镜像（镜像后 = 左脚起跳）。
                //   跑跳 clip 是"右脚起跳"，跑步时左脚在前就该播镜像版，否则起跳脚会突变。
                //   ⚠️ 必须在 SetTrigger **之前**写 —— Unity 每帧读状态的 Mirror 参数，
                //      等进了状态再改会让动画在半空中翻个个儿。
                //   ⚠️ 这里刻意用 `if (running)` 包住整个写入（而不是"只在要镜像时才写 true"）：
                //      每次跑跳都得把该参数**写死成当次的判断结果** —— 否则上一次跑跳留下的 true 会一直粘着，
                //      结果就是"该镜像时不镜像、不该镜像时瞎镜像"。
                //   ℹ️ 站跳（Kimodo_jump）没勾 Mirror 参数，本参数对它的观感没有影响。
                if (running) _anim.SetBool(JumpMirrorParam, IsLeftFootForward());

                _motor.enabled = false;//起跳停止地面移动（含贴地下压）
                _jump.BeginJump(takeoffSpeed);
                _anim.SetTrigger(running ? "RunJump" : "Jump");
            };
            _enter[PlayerState.Attack] = () =>
            {
                //⭐ 组件**必须保持 enabled**："只转向"与"贴地下压"都写在 `PlayerMotor.Update` 里，
                //   禁用会一起停掉（那会让攻击既不能转向、也失去贴地下压）。
                //   本段的前冲由**武器的三轴曲线**驱动（`PlayerMotor.ComputeCodeLunge`），与动画根运动无关。
                //   ⭐ 转向窗口由 PlayerMotor 内部判（默认方案 B：命中帧前可转、出手后锁定），
                //      本次 `StartCombo()` 会把窗口重新打开（每段一个自己的窗口）。
                _motor.enabled = true;
                _motor.SetFacingOnly(true);
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
                if (_jump != null) _jump.enabled = false;
                //ℹ️ 2026-10-12：这里原来有一句 `_anim.applyRootMotion = true`，本意是"让死亡动画自己位移"。
                //   实测（Play 模式逐帧采样）证明它**从来没生效过** —— 只要 `PlayerMotor` 里存在 `OnAnimatorMove`，
                //   Unity 就永不自动应用根运动。而死亡动画的**倒地完全由骨骼姿势完成**
                //   （Hips 世界 Y 下沉 1.004 m，与根运动无关），所以删掉它不会有任何视觉变化。
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
            _update[PlayerState.Jump] = () =>
            {
                //⭐ 判据用 **Busy**（滞空 **或** 落地后摇），不是 IsJumping。
                //   跳跃现在是两段：滞空段 + 落地后摇段。要两段都走完才放玩家回 Idle/Run ——
                //   否则落地瞬间就能跑，落地缓冲/起身的动画会被直接混合掉（就是"落地没有重量感"的来源）。
                if (!_jump.Busy) Change(HasMoveInput() ? PlayerState.Run : PlayerState.Idle);
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

                //攻击中可取消接跳跃（与冲刺同一策略：动作取消技，起跳即打断收招）
                if (input.HasBufferedJump && TryJump()) input.ConsumeJump();
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
            _exit[PlayerState.Jump] = () =>
            {
                //正常落地 / 被受击打断都走这里：清计时器（残留的话角色会"落地后还剩半截跳跃"）
                _jump.EndJump();
            };

            //离开攻击状态时：清掉残留判定体（收招或被受击打断都要清）+ **复位"只转向"模式**
            //⚠️ 复位绝对不能漏 —— 漏了就是"之后只能转身、跑不动"。
            _exit[PlayerState.Attack] = () =>
            {
                _attack.CloseAllHitboxes();
                _motor.SetFacingOnly(false);
                //⚠️ 防御：**离开攻击时必须恢复 enabled** —— 否则再也不会位移。
                //   （Idle / Run / Dash 的 _enter 里本来也会 `enabled = true`，这里再兜一道：
                //     避免将来有人在 Attack 与某个"不碰 enabled"的状态之间直连时漏掉，那会是"打一次后再也不能动"。）
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
            if (input.HasBufferedJump && TryJump()) { input.ConsumeJump(); return; }
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

        /// <summary>
        /// 尝试起跳。与 <see cref="TryDash"/> 同一套规则：无资源消耗，
        /// 唯一门槛是"当前这一整套跳跃还没做完"。（空中不能再跳 —— 没有二段跳。）
        ///
        /// ⚠️ 2026-10-13：判据由 <c>IsJumping</c> 换成 <see cref="PlayerJump.Busy"/> ——
        ///    **落地后摇期间同样不允许起跳**。否则玩家可以在后摇里连点跳跃键，
        ///    用"后摇中起跳"把定身硬直整个绕过，后摇就形同虚设了。
        /// </summary>
        private bool TryJump()
        {
            if (_jump == null || _jump.Busy) return false;

            Change(PlayerState.Jump);
            return true;
        }

        /// <summary>
        /// 当前**左脚是否比右脚更靠前**（沿角色正面量）—— 用于决定跑跳动画要不要镜像。
        ///
        /// 做法：取两脚相对 **Hips** 的位移，投影到 <c>transform.forward</c> 上比大小。
        /// <list type="bullet">
        ///   <item>用 <b>Hips</b> 而不是 <c>transform.position</c> 做基准：躯干才是两脚的共同参照，
        ///         拿角色原点比会被"身体前后倾"干扰。</item>
        ///   <item>用<b>骨骼位置</b>而不是动画的 <c>normalizedTime</c>：Locomotion 是混合树，
        ///         Idle / Walk / Run 的循环长度差 3.7 倍（2.467 s vs 0.667 s），相位会被混在一起，
        ///         读数不可靠；骨骼位置则是"物理真相"。</item>
        /// </list>
        ///
        /// ⚠️ 两脚交叉的那几帧（实测投影差接近 0）判断必然是不确定的 —— 但那只占极短一瞬，
        ///    且镜像与否只影响观感、不影响任何判定，所以**不加滞回**，保持简单。
        /// ⚠️ 骨骼没缓存到（非 Humanoid / 缺 Avatar）时**一律返回 false**，即退回"永远原版"。
        /// </summary>
        private bool IsLeftFootForward()
        {
            if (_hipsBone == null || _leftFootBone == null || _rightFootBone == null) return false;

            Vector3 fwd = transform.forward;
            float left = Vector3.Dot(_leftFootBone.position - _hipsBone.position, fwd);
            float right = Vector3.Dot(_rightFootBone.position - _hipsBone.position, fwd);
            return left > right;
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
