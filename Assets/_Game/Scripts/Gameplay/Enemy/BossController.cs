using System;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Boss（Crichton）行为状态机。**复用** <see cref="EnemyAIController"/>（组件 / 数值 / 闪白 / 感知）
    /// 与 <see cref="EnemyLocomotion"/>（移动 / 转向），自己只负责「状态表 + 转移条件 + 动画与判定体时机」，
    /// 与 <see cref="EnemyMeleeAI"/> 完全同构（PHASE1-DESIGN §5.4.2 的口径：**不要新写一套**）。
    ///
    /// <para>状态转移：</para>
    /// <code>
    /// NIdle      ──玩家水平距离 ≤ 战斗范围(aggroRange)────────────→ CombatIdle
    /// CombatIdle ──特殊攻击双 CD 归零──────────────────────────→ Special
    ///            ──普通攻击 CD 就绪 &amp; 玩家在攻击范围──────────────→ Attack
    ///            ──普通攻击 CD 就绪 &amp; 玩家不在攻击范围────────────→ Chase
    ///            ──普通攻击 CD 未就绪────────────────────────────→ （原地）绕玩家侧移 walkleft
    ///            ──离开战斗范围超过 disengageTime──────────────→ NIdle
    /// Chase      ──进入攻击范围──────────────────────────────→ Attack（特殊攻击就绪则先 Special）
    /// Attack     ──动画播完─────────────────────────────────→ CombatIdle（b 命中 comboChance 则先接 c 再走）
    /// Special    ──动画播完─────────────────────────────────→ CombatIdle
    /// 任意        ──韧性条被打满──────────────────────────────→ Hit ──动画播完──→ CombatIdle
    /// 任意        ──生命 ≤ 0────────────────────────────────→ Death（锁死状态机）
    /// </code>
    ///
    /// <para>⭐ 两条「防卡死」设计（需求里明确要求的）：</para>
    /// <list type="number">
    ///   <item>两个 CD 都在**进入动画的第一帧**就重置 —— 不是在动画播完之后。这样即使动画很长、
    ///         或被破韧打断，CD 也不会被漏掉。</item>
    ///   <item>每个动作都有**兜底超时**（<c>attackTimeout</c> / <c>specialTimeout</c> / <c>hitTimeout</c>）：
    ///         动画没接上、或误设成循环时，状态机也会自己走出来，不会永久停在动作状态里。</item>
    /// </list>
    ///
    /// <para>⚠️ 移动全部由**代码**驱动（根运动在本工程是全局关闭的，见 REF-COMBAT）。
    /// 所以 sa2「追击 + 攻击」虽然是一个动画，**追击段的位移由代码在 specialApproachTime 内推进**，
    /// 做那条动画时这一段只做姿态、不要放位移。</para>
    /// </summary>
    public class BossController : EnemyAIController
    {
        public enum EState { NIdle, CombatIdle, Chase, Attack, Special, Hit, Death }

        // ==================== 动画参数名（与 Animator 控制器里的参数逐字一致） ====================
        // ⚠️ 基类的 PlayHitAnim / PlayDeathAnim 里引用了名为 "Attack" 的参数，本 Boss 用的是
        //    AttackA/B/C 三个独立触发器，所以那两方法**不要用**（用了只会刷一条"参数不存在"警告）。
        private const string P_Speed = "Speed";             // Float  实际水平速度，驱动 待机↔walkfront
        private const string P_Strafe = "Strafe";           // Bool   true → 用 walkleft
        private const string P_InCombat = "InCombat";       // Bool   N_idle ↔ 战斗状态
        private const string P_AttackA = "AttackA";         // Trigger
        private const string P_AttackB = "AttackB";         // Trigger
        private const string P_AttackC = "AttackC";         // Trigger（单出 c / 二连段的第二段，共用）
        private const string P_SpecialNear = "SpecialNear"; // Trigger  sa1（原地）
        private const string P_SpecialFar = "SpecialFar";   // Trigger  sa2（先追击再打）
        private const string P_Hit = "Hit";                 // Trigger  破韧硬直
        private const string P_IsDead = "IsDead";           // Bool

        [Header("Boss 判定体名（对应预制体下的子物体名）")]
        [Tooltip("三选一攻击各自的判定体。用动画事件的 TickAttack() 按当前招式自动开对应的那个。")]
        [SerializeField] private string _hitboxA = "Hitbox_AttackA";
        [SerializeField] private string _hitboxB = "Hitbox_AttackB";
        [SerializeField] private string _hitboxC = "Hitbox_AttackC";
        [SerializeField] private string _hitboxSpecial = "Hitbox_Special";

        // ==================== 状态机 ====================
        private EnemyStateMachine<EState> _fsm;

        // ==================== 冷却（与状态无关，在 OnAIUpdate 里连续走） ====================
        private float _atkCd;//剩余普通攻击 CD 秒数
        private float _spCd; //剩余特殊攻击 CD 秒数

        // ==================== 当前动作 ====================
        private float _actT;           //本动作已过秒数
        private float _actTimeout;     //本动作的兜底超时
        private string _curHitbox;     //本次动作该开哪个判定体
        private float _curHitboxTime;  //本次动作判定体开启时长
        private bool _forceActionEnd;  //由 OnActionEnd() 动画事件置位（提前结束动作）

        // ==================== 连段 ====================
        private bool _comboPending;    //本次攻击是 b 且抽中接 c
        private bool _comboFired;      //第二段 c 是否已经放出去

        // ==================== 韧性 ====================
        private float _poise;          //当前累计削韧值
        private float _poiseHoldT;     //距上次受击的秒数（过了 poiseRecoverDelay 才开始自然恢复）
        private float _poiseImmuneT;   //破韧后的免疫剩余秒数

        // ==================== 脱战 / 绕圈 ====================
        private float _outOfRangeT;    //连续离开战斗范围的秒数
        private int _strafeDir = 1;    //+1 / -1，绕圈方向

        // ==================== 特殊攻击的追击段 ====================
        private float _approachT;      //sa2 追击段剩余秒数（0 = 不追了）

        /// <summary>韧性变化通知（当前值, 上限）。UI 的血条旁边的韧性条订阅它。</summary>
        public event Action<float, float> PoiseChanged;

        /// <summary>当前韧性占比（0..1），UI 也可以直接读。</summary>
        public float Poise01 { get { return PoiseMax <= 0f ? 0f : Mathf.Clamp01(_poise / PoiseMax); } }

        // ==================== 配置取值（配置没挂时给安全默认，避免 NRE） ====================

        /// <summary>Boss 专用配置。<c>_config</c> 不是 BossConfig 时为 null（会在 Awake 里报错）。</summary>
        protected BossConfig BossCfg { get { return _config as BossConfig; } }

        private float CfgTurnSpeed { get { return BossCfg != null ? BossCfg.turnSpeed : 240f; } }
        private float CfgStrafeRadius { get { return BossCfg != null ? BossCfg.strafeRadius : 5f; } }
        private float CfgAttackCdMin { get { return BossCfg != null ? BossCfg.attackCdMin : 1.5f; } }
        private float CfgAttackCdMax { get { return BossCfg != null ? BossCfg.attackCdMax : 3f; } }
        private float CfgAttackTimeout { get { return BossCfg != null ? BossCfg.attackTimeout : 4f; } }
        private float CfgSpecialCd { get { return BossCfg != null ? BossCfg.specialCd : 14f; } }
        private float CfgSpecialTimeout { get { return BossCfg != null ? BossCfg.specialTimeout : 6f; } }
        private float CfgHitTimeout { get { return BossCfg != null ? BossCfg.hitTimeout : 3f; } }
        private float CfgMaxPoise { get { return BossCfg != null ? BossCfg.maxPoise : 100f; } }
        private float CfgDisengageTime { get { return BossCfg != null ? BossCfg.disengageTime : 4f; } }

        private float PoiseMax { get { return Mathf.Max(0.01f, CfgMaxPoise); } }

        // ==================== 状态表 ====================

        protected override void Awake()
        {
            base.Awake();

            if (_config != null && BossCfg == null)
                Debug.LogError("[BossController] Config 字段指的不是 BossConfig 资产 —— " +
                               "Boss 的招式 / 韧性数值会全部走安全默认值。请在预制体上改指到 BossConfig.asset。", this);

            _fsm = new EnemyStateMachine<EState>(name);

            _fsm.Register(EState.NIdle, enter: EnterNIdle, update: UpdateNIdle);
            _fsm.Register(EState.CombatIdle, enter: EnterCombatIdle, update: UpdateCombatIdle);
            _fsm.Register(EState.Chase, enter: EnterChase, update: UpdateChase, exit: ExitChase);
            _fsm.Register(EState.Attack, enter: EnterAttack, update: UpdateAttack, exit: ExitAttack);
            _fsm.Register(EState.Special, enter: EnterSpecial, update: UpdateSpecial, exit: ExitSpecial);
            _fsm.Register(EState.Hit, enter: EnterHit, update: UpdateHit);
            _fsm.Register(EState.Death, enter: EnterDeath);

            AttachStateMachine(_fsm);
        }

        protected override void Start()
        {
            base.Start();
            _fsm.Start(EState.NIdle);
        }

        protected override void OnEnable()
        {
            base.OnEnable();//基类在这里订阅 HandleDamaged（Boss 只闪白，不打断）
            if (Health != null) Health.Damaged += OnBossDamaged;//Boss 自己再挂一个：累计削韧
        }

        protected override void OnDisable()
        {
            if (Health != null) Health.Damaged -= OnBossDamaged;
            base.OnDisable();
        }

        // ==================== 与状态无关的计时（基类专门留的钩子） ====================

        /// <summary>
        /// 两个 CD / 韧性恢复都在这里走 —— **与当前状态无关**。
        /// 这就是「防卡 CD」的关键：CD 不是由某个状态负责推进的，
        /// 所以被破韧打断、或在状态之间跳来跳去都不会把 CD 停住。
        /// </summary>
        protected override void OnAIUpdate()
        {
            if (IsStateLocked) return;

            float dt = Time.deltaTime;

            if (_atkCd > 0f) _atkCd -= dt;
            if (_spCd > 0f) _spCd -= dt;
            if (_poiseImmuneT > 0f) _poiseImmuneT -= dt;

            _poiseHoldT += dt;//距上次受击的时间（削韧时会被清零）

            //韧性自然恢复（默认 0 = 不恢复）
            if (BossCfg != null && BossCfg.poiseRecoverPerSecond > 0f && _poise > 0f
                && _poiseHoldT >= BossCfg.poiseRecoverDelay)
            {
                SetPoise(_poise - BossCfg.poiseRecoverPerSecond * dt);
            }
        }

        // ==================== N_idle（未进战斗） ====================

        private void EnterNIdle()
        {
            Loco.Stop();
            SetAnimBool(P_InCombat, false);
            SetAnimBool(P_Strafe, false);
            SetAnimFloat(P_Speed, 0f);
            _outOfRangeT = 0f;
        }

        private void UpdateNIdle()
        {
            if (Target == null) return;
            if (HorizontalDistanceToTarget <= AggroRange) _fsm.Change(EState.CombatIdle);
        }

        // ==================== 战斗待机（也是「攻击 CD 期间」的状态） ====================
        // 需求：处于攻击 CD 时「始终面向玩家，用 walkleft 动画」⇒ 本状态就是它。
        //   · CD 未就绪 → 绕玩家侧移（Strafe = true ⇒ 动画走 walkleft）
        //   · CD 就绪   → 在攻击范围就打，不在就 Chase

        private void EnterCombatIdle()
        {
            SetAnimBool(P_InCombat, true);
            _outOfRangeT = 0f;

            //每次进战斗待机随机一次绕圈方向，避免看起来像在跑固定圈
            if (BossCfg == null || BossCfg.randomStrafeDirection)
                _strafeDir = (UnityEngine.Random.value < 0.5f) ? -1 : 1;
            else
                _strafeDir = 1;
        }

        private void UpdateCombatIdle()
        {
            if (Target == null) { _fsm.Change(EState.NIdle); return; }

            float dist = HorizontalDistanceToTarget;

            //⭐ 水平距离判脱战（不吃 Y 差 —— Boss 4.47 m 高，用 3D 距离会永远"够不着"）
            if (dist > AggroRange)
            {
                _outOfRangeT += Time.deltaTime;
                if (CfgDisengageTime > 0f && _outOfRangeT >= CfgDisengageTime)
                {
                    _atkCd = 0f;
                    _spCd = 0f;
                    SetPoise(0f);
                    _fsm.Change(EState.NIdle);
                    return;
                }
            }
            else
            {
                _outOfRangeT = 0f;
            }

            Loco.FaceTarget(Target, CfgTurnSpeed);//始终面向玩家

            //⭐ 判定顺序：特殊攻击优先 → 普通攻击 → 绕圈等 CD。
            //   否则普通攻击会在 CD 刚归零的那一帧把窗口抢走，特殊攻击几乎永远轮不到。
            if (IsSpecialReady) { _fsm.Change(EState.Special); return; }

            if (_atkCd <= 0f)
            {
                if (dist <= AttackRange) { _fsm.Change(EState.Attack); return; }
                _fsm.Change(EState.Chase);
                return;
            }

            StrafeAroundTarget();//CD 还没好 → 面朝玩家绕圈（walkleft）
        }

        /// <summary>
        /// 保持 <c>strafeRadius</c> 半径、面朝玩家绕圈。
        /// 用 <see cref="EnemyLocomotion.BackstepTo"/> 而不是 MoveToOrStep：
        /// 后者会把转向权交还 NavMeshAgent（朝行进方向转身）⇒ 变成背对玩家走开。
        /// </summary>
        private void StrafeAroundTarget()
        {
            float radius = CfgStrafeRadius;
            if (radius <= 0.1f) { SetAnimBool(P_Strafe, false); Loco.Stop(); return; }

            Vector3 rel = transform.position - Target.position;
            rel.y = 0f;

            //线速度换算成角速度：ω = v / r
            float angDeg = MoveSpeed / Mathf.Max(0.5f, radius) * Mathf.Rad2Deg * Time.deltaTime * _strafeDir;

            Vector3 nextRel = Quaternion.AngleAxis(angDeg, Vector3.up) * rel;
            if (nextRel.sqrMagnitude < 0.0001f) return;//与玩家重合时跳过一帧，避免除零

            Vector3 nextPos = Target.position + nextRel.normalized * radius;

            SetAnimBool(P_Strafe, true);
            Loco.BackstepTo(nextPos, Target, CfgTurnSpeed);
        }

        // ==================== Chase（walkfront 靠近玩家） ====================

        private void EnterChase()
        {
            SetAnimBool(P_Strafe, false);
            Loco.Resume();
        }

        private void UpdateChase()
        {
            if (Target == null) { _fsm.Change(EState.NIdle); return; }

            float dist = HorizontalDistanceToTarget;

            if (dist > AggroRange)
            {
                _outOfRangeT += Time.deltaTime;
                if (CfgDisengageTime > 0f && _outOfRangeT >= CfgDisengageTime)
                {
                    _fsm.Change(EState.NIdle);
                    return;
                }
            }
            else
            {
                _outOfRangeT = 0f;
            }

            //追击途中也可能触发特殊攻击（远距离的 sa2 本身就负责追）
            if (IsSpecialReady) { _fsm.Change(EState.Special); return; }

            if (dist <= AttackRange)
            {
                //CD 没好的话不硬打，回战斗待机绕圈等
                _fsm.Change(_atkCd <= 0f ? EState.Attack : EState.CombatIdle);
                return;
            }

            Loco.ChaseTarget(Target);//walkfront
        }

        private void ExitChase()
        {
            Loco.Stop();
        }

        // ==================== Attack（三选一 + b→bc 二连段） ====================

        private void EnterAttack()
        {
            Loco.Stop();

            //⭐ 需求：CD 在「进入动画的第一时间」开始算 —— 就在这里，不是等动画播完
            _atkCd = UnityEngine.Random.Range(Mathf.Min(CfgAttackCdMin, CfgAttackCdMax),
                                              Mathf.Max(CfgAttackCdMin, CfgAttackCdMax));

            int pick = PickAttackIndex();//0=a 1=b 2=c
            _comboPending = false;
            _comboFired = false;

            if (pick == 0)
            {
                _curHitbox = _hitboxA;
                _curHitboxTime = BossCfg != null ? BossCfg.attackHitboxActiveTime : 0.25f;
                SetAnimTrigger(P_AttackA);
            }
            else if (pick == 1)
            {
                _curHitbox = _hitboxB;
                _curHitboxTime = BossCfg != null ? BossCfg.attackHitboxActiveTime : 0.25f;
                //b 出手时就把「要不要接 c」定下来（不在动画中途再抽，方便复现与调试）
                _comboPending = UnityEngine.Random.value < (BossCfg != null ? BossCfg.comboChance : 0.4f);
                SetAnimTrigger(P_AttackB);
            }
            else
            {
                _curHitbox = _hitboxC;
                _curHitboxTime = BossCfg != null ? BossCfg.attackHitboxActiveTime : 0.25f;
                SetAnimTrigger(P_AttackC);
            }

            BeginAction(CfgAttackTimeout);
        }

        private void UpdateAttack()
        {
            Loco.FaceTarget(Target, CfgTurnSpeed);//攻击期间只转身、不位移

            //⚠️ ActionFinished() 内部会累加 _actT，**一帧只能调一次**（调两次会双倍计时）
            bool finished = ActionFinished();

            //b 播完 → 抽中二连段则接第二段 c，否则正常收招
            if (finished && _comboPending && !_comboFired)
            {
                _comboFired = true;
                _curHitbox = _hitboxC;
                _curHitboxTime = BossCfg != null ? BossCfg.comboHitboxActiveTime : 0.25f;
                //⚠️ 先把 B 的残留触发器复位再触发 C，避免复用到下个动作
                ResetAnimTrigger(P_AttackB);
                SetAnimTrigger(P_AttackC);
                BeginAction(CfgAttackTimeout);
                return;
            }

            if (finished) _fsm.Change(EState.CombatIdle);
        }

        private void ExitAttack()
        {
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();
            ResetAnimTrigger(P_AttackA);
            ResetAnimTrigger(P_AttackB);
            ResetAnimTrigger(P_AttackC);
        }

        /// <summary>按权重抽 0=a / 1=b / 2=c。三个权重都为 0 时退化成等概率。</summary>
        private int PickAttackIndex()
        {
            float wa = BossCfg != null ? Mathf.Max(0f, BossCfg.attackWeightA) : 1f;
            float wb = BossCfg != null ? Mathf.Max(0f, BossCfg.attackWeightB) : 1f;
            float wc = BossCfg != null ? Mathf.Max(0f, BossCfg.attackWeightC) : 1f;

            float total = wa + wb + wc;
            if (total <= 0f) return UnityEngine.Random.Range(0, 3);

            float r = UnityEngine.Random.value * total;
            if (r < wa) return 0;
            if (r < wa + wb) return 1;
            return 2;
        }

        // ==================== Special（sa1 原地 / sa2 先追后打） ====================

        private void EnterSpecial()
        {
            Loco.Stop();

            //⭐ 同样在进入动画的第一时间重置两个 CD
            _spCd = CfgSpecialCd;
            _atkCd = UnityEngine.Random.Range(Mathf.Min(CfgAttackCdMin, CfgAttackCdMax),
                                              Mathf.Max(CfgAttackCdMin, CfgAttackCdMax));

            _curHitbox = _hitboxSpecial;
            _curHitboxTime = BossCfg != null ? BossCfg.specialHitboxActiveTime : 0.35f;

            float nearMax = BossCfg != null ? BossCfg.specialNearMaxDistance : 6f;
            if (HorizontalDistanceToTarget <= nearMax)
            {
                _approachT = 0f;
                SetAnimTrigger(P_SpecialNear);//sa1：原地
            }
            else
            {
                _approachT = BossCfg != null ? BossCfg.specialApproachTime : 0.9f;
                SetAnimTrigger(P_SpecialFar);//sa2：先追到玩家身前再打（追击段位移由代码推）
            }

            BeginAction(CfgSpecialTimeout);
        }

        private void UpdateSpecial()
        {
            //---- sa2 追击段：位移由代码推进（根运动全局关闭，动画里那段不带位移）----
            if (_approachT > 0f && Target != null)
            {
                float stopDist = BossCfg != null ? BossCfg.specialFarStopDistance : 3.5f;
                bool closeEnough = HorizontalDistanceToTarget <= stopDist;

                if (closeEnough)
                {
                    _approachT = 0f;//到玩家身前了，提前收住
                    Loco.Stop();
                }
                else
                {
                    _approachT -= Time.deltaTime;

                    Vector3 toTarget = Target.position - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        float speed = BossCfg != null ? BossCfg.specialApproachSpeed : 7f;
                        //只推进到「距玩家 stopDist」处，别一头撞进玩家身体里
                        float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(0f, toTarget.magnitude - stopDist));
                        transform.position += toTarget.normalized * step;
                    }

                    bool keepFacing = BossCfg == null || BossCfg.specialKeepFacingPlayer;
                    if (keepFacing) Loco.FaceTarget(Target, CfgTurnSpeed);
                }
            }
            else
            {
                Loco.FaceTarget(Target, CfgTurnSpeed);
            }

            if (ActionFinished()) _fsm.Change(EState.CombatIdle);
        }

        private void ExitSpecial()
        {
            _approachT = 0f;
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();
            ResetAnimTrigger(P_SpecialNear);
            ResetAnimTrigger(P_SpecialFar);
        }

        private bool IsSpecialReady
        {
            //需求：特殊攻击 CD 与普通攻击 CD **都为 0** 才触发
            get { return _spCd <= 0f && _atkCd <= 0f && Target != null; }
        }

        // ==================== Hit（破韧硬直，打断一切） ====================

        private void EnterHit()
        {
            Loco.Stop();
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();

            _comboPending = false;
            _comboFired = false;
            _approachT = 0f;

            //⚠️ 必须把所有动作触发器复位：攻击被打断时触发器可能还没被状态机消费掉，
            //   留着它会让 hit 动画一播完就"诈尸"接着挥一刀（EnemyAIController 里也强调过这点）。
            ResetAnimTrigger(P_AttackA);
            ResetAnimTrigger(P_AttackB);
            ResetAnimTrigger(P_AttackC);
            ResetAnimTrigger(P_SpecialNear);
            ResetAnimTrigger(P_SpecialFar);

            SetAnimTrigger(P_Hit);

            SetPoise(0f);
            _poiseImmuneT = BossCfg != null ? BossCfg.poiseBreakImmunityTime : 0.6f;

            BeginAction(CfgHitTimeout);
        }

        private void UpdateHit()
        {
            if (ActionFinished())
                _fsm.Change(Target != null && HorizontalDistanceToTarget <= AggroRange
                            ? EState.CombatIdle : EState.NIdle);
        }

        // ==================== Death ====================

        protected override void OnKilled()
        {
            _fsm.Change(EState.Death);
        }

        private void EnterDeath()
        {
            Loco.Stop();
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();

            ResetAnimTrigger(P_AttackA);
            ResetAnimTrigger(P_AttackB);
            ResetAnimTrigger(P_AttackC);
            ResetAnimTrigger(P_SpecialNear);
            ResetAnimTrigger(P_SpecialFar);
            ResetAnimTrigger(P_Hit);

            SetAnimBool(P_Strafe, false);
            SetAnimFloat(P_Speed, 0f);
            SetAnimBool(P_IsDead, true);

            _fsm.LockStateMachine();//死后不再接受任何状态切换

            //GameManager 在等这个事件弹结算面板（见 GameEvents / GameManager）
            GameEvents.RaiseBossDied();
        }

        // ==================== 韧性（唯一新增的战斗机制） ====================

        /// <summary>
        /// 受击 → 累计削韧。需求：削韧值 = 伤害 × <c>poisePerDamageMultiplier</c>（默认 2 倍）。
        /// ⚠️ 基类的 <c>HandleDamaged</c> 对 <c>canBeInterrupted = false</c> 的 Boss 会提前 return
        /// （只闪白），所以这里**另订阅一份** <see cref="EnemyHealth.Damaged"/>，互不干扰。
        /// </summary>
        private void OnBossDamaged(EnemyHealth h, int dmg)
        {
            if (IsStateLocked) return;
            if (_fsm == null) return;
            if (_fsm.Is(EState.Hit)) return;   //破韧硬直期间不再累积（否则会连续硬直成木桩）
            if (_poiseImmuneT > 0f) return;

            float mult = BossCfg != null ? BossCfg.poisePerDamageMultiplier : 2f;
            _poiseHoldT = 0f;
            SetPoise(_poise + dmg * mult);

            if (_poise >= PoiseMax)
                _fsm.Change(EState.Hit);//打断一切
        }

        private void SetPoise(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, PoiseMax);
            if (Mathf.Approximately(clamped, _poise)) return;

            _poise = clamped;
            if (PoiseChanged != null) PoiseChanged(_poise, PoiseMax);
        }

        // ==================== 动画事件入口 ====================

        /// <summary>
        /// **判定体开关**。挂到每段攻击 / 特殊攻击动画的命中帧上（Animation Event，函数名 <c>TickAttack</c>）。
        /// 会按**当前正在放的那一招**自动开对应判定体。
        ///
        /// ⚠️ 漏挂是**静默**的（打不出伤害但不会有任何报错）——这是本工程既有的坑，
        ///    参考 EnemyMeleeAI 的 TickAttack 与 GDD §8。
        /// </summary>
        private void TickAttack()
        {
            if (Hitboxes == null)
            {
                Debug.LogWarning("[BossController] 没找到 HitboxController —— 攻击不会造成伤害。", this);
                return;
            }
            if (string.IsNullOrEmpty(_curHitbox)) return;

            Hitboxes.EnableHitbox(_curHitbox, _curHitboxTime);
        }

        /// <summary>
        /// **可选**的动画事件：提前结束当前动作。
        /// 用途：想让 bc 二连段在 b 的**后摇中途**就接上 c（而不是等 b 完全播完），
        /// 在 b 动画的后摇起点挂一个 <c>OnActionEnd</c> 事件即可。
        /// 不挂也完全没问题（那时就按动画自然播完来接）。
        /// </summary>
        private void OnActionEnd()
        {
            _forceActionEnd = true;
        }

        // ==================== 动作计时 / 结束判定 ====================

        /// <summary>开始一个新动作：清计时、记超时。</summary>
        private void BeginAction(float timeout)
        {
            _actT = 0f;
            _actTimeout = Mathf.Max(0.05f, timeout);
            _forceActionEnd = false;
        }

        /// <summary>
        /// 当前动作是否结束。三条判据，任一成立即结束：
        /// <list type="number">
        ///   <item>挂了 <c>OnActionEnd</c> 动画事件且已经触发</item>
        ///   <item>动画自然播完（<c>normalizedTime &gt;= 1</c>，且不在过渡中）</item>
        ///   <item>⭐ 兜底超时 —— 动画没接上 / 被误设成循环时，靠它自己走出来</item>
        /// </list>
        /// </summary>
        private bool ActionFinished()
        {
            _actT += Time.deltaTime;

            if (_forceActionEnd) return true;
            if (_actT >= _actTimeout) return true;
            if (Anim == null) return _actT >= 0.5f;
            if (Anim.IsInTransition(0)) return false;//过渡中读到的 normalizedTime 是别的状态的，不判

            return Anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        }

        // ==================== 距离（水平） ====================

        /// <summary>
        /// **水平距离**。⭐ Boss 4.47 m 高、玩家 1.8 m 左右，用 3D 距离会让"攻击范围"永远判不中，
        /// 所以战斗 / 攻击 / 特殊攻击的距离全部用水平距离。
        /// </summary>
        private float HorizontalDistanceToTarget
        {
            get
            {
                if (Target == null) return float.MaxValue;
                Vector3 d = Target.position - transform.position;
                d.y = 0f;
                return d.magnitude;
            }
        }

        // ==================== Scene 视图 ====================

        private void OnDrawGizmosSelected()
        {
            if (BossCfg != null && !BossCfg.drawGizmos) return;

            Gizmos.color = Color.cyan;//战斗范围
            Gizmos.DrawWireSphere(transform.position, AggroRange);

            Gizmos.color = Color.red;//攻击范围
            Gizmos.DrawWireSphere(transform.position, AttackRange);

            if (BossCfg != null)
            {
                Gizmos.color = Color.yellow;//特殊攻击距离阈值
                Gizmos.DrawWireSphere(transform.position, BossCfg.specialNearMaxDistance);

                Gizmos.color = new Color(1f, 0.5f, 0f);//sa2 停靠距离
                Gizmos.DrawWireSphere(transform.position, BossCfg.specialFarStopDistance);
            }
        }
    }
}
