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
        /// <summary>
        /// Boss 状态。⭐ 2026-10-21 新增 <see cref="Return"/>：脱战后**走回出生点**，而不是瞬移 / 原地待机。
        /// <para>它属于「非战斗」状态（<see cref="InCombat"/> 里被排除），所以血条在这一段是收起的，
        /// 但 Boss 会沿 NavMesh 走回去，途中玩家进入战斗范围就<b>就地开打</b>。</para>
        /// </summary>
        public enum EState { NIdle, CombatIdle, Chase, Attack, Special, Hit, Return, Death }

        // ==================== 动画参数名（与 Animator 控制器里的参数逐字一致） ====================
        // ⚠️ 基类的 PlayHitAnim / PlayDeathAnim 里引用了名为 "Attack" 的参数，本 Boss 用的是
        //    AttackA/B/C 三个独立触发器，所以那两方法**不要用**（用了只会刷一条"参数不存在"警告）。
        private const string P_Speed = "Speed";             // Float  实际水平速度，驱动 待机↔walkfront
        private const string P_Strafe = "Strafe";           // Bool   true → 用 walkleft
        private const string P_WalkDir = "WalkDir";         // Float  侧移方向：+1 = 正向，-1 = 倒放（walk_left 状态的 Speed 由它驱动）
        private const string P_InCombat = "InCombat";       // Bool   N_idle ↔ 战斗状态
        private const string P_AttackA = "AttackA";         // Trigger
        private const string P_AttackB = "AttackB";         // Trigger
        private const string P_AttackC = "AttackC";         // Trigger（单出 c / 二连段的第二段，共用）
        private const string P_SpecialNear = "SpecialNear"; // Trigger  sa1（原地）
        private const string P_SpecialFar = "SpecialFar";   // Trigger  sa2（先追击再打）
        private const string P_Hit = "Hit";                 // Trigger  破韧硬直
        private const string P_IsDead = "IsDead";           // Bool

        /// <summary>Animator 里的**状态名**（注意不是参数名）—— 读动画进度时要拿它比对。Boss 的三段普攻状态叫 a1 / a2 / a3。</summary>
        private const string S_AttackB = "a2";

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

        // ==================== 脱战复位 / 回血 ====================
        /// <summary>出生点（<see cref="Awake"/> 记下当时的 transform.position）。走回原位就是走回这里。</summary>
        private Vector3 _homePos;
        /// <summary>
        /// 出生时的**朝向**。到点后要转回这个朝向 —— <c>MoveToOrStep</c> 会把转向交给 NavMeshAgent
        /// （朝行进方向转），不纠正的话 Boss 回家后会**面朝它走回来的方向**（从 +X 回来就是 **−90°**）。
        /// </summary>
        private Quaternion _homeRot = Quaternion.identity;
        /// <summary>本次「走回原位」已用时（秒），只用于兜底超时。</summary>
        private float _returnT;
        /// <summary>是否已走到出生点（进入"转回朝向"阶段）。</summary>
        private bool _arrivedHome;
        /// <summary>回血的整数余量。HP 是 int 而每帧回量 &lt; 1，不攒就会一直被取整成 0。</summary>
        private float _hpRecoverAccum;

        /// <summary>走回原位时离出生点多近算「到点」（米）。</summary>
        private const float ReturnArriveRadius = 0.8f;
        /// <summary>走回原位时临时把 Agent 停靠距离压到这个值（米）——否则它会在 3.6 m 外就停下，根本没回到位。</summary>
        private const float ReturnStoppingDistance = 0.2f;
        /// <summary>走回原位的兜底超时（秒）。出生点被杂物挡住 / 不在 NavMesh 上时，不让它永远卡在 Return。</summary>
        private const float ReturnTimeout = 20f;
        /// <summary>进入 Return 前的 Agent 停靠距离，离开时还原（Chase 靠它不怼进玩家身体里）。</summary>
        private float _savedStoppingDistance;

        /// <summary>
        /// 本次特殊攻击是不是**远距的 sa2**（<c>false</c> = 近距的 sa1，原地出招）。
        /// 只用来决定"sa2 是否全程允许转向"，见 <see cref="CanTurn"/>。在 <see cref="EnterSpecial"/> 里赋值。
        /// </summary>
        private bool _specialIsFar;

        // ==================== 特殊攻击的追击段 ====================
        private float _approachT;      //sa2 追击段剩余秒数（0 = 不追了）

        /// <summary>韧性变化通知（当前值, 上限）。UI 的血条旁边的韧性条订阅它。</summary>
        public event Action<float, float> PoiseChanged;

        /// <summary>当前韧性占比（0..1），UI 也可以直接读。</summary>
        public float Poise01 { get { return PoiseMax <= 0f ? 0f : Mathf.Clamp01(_poise / PoiseMax); } }

        /// <summary>
        /// 进入 / 退出**战斗**的通知（true = 进入战斗）。
        ///
        /// <para>✅ **2026-10-15 新增**：Boss 屏幕血条（<c>Game.UI.BossHealthBar</c>）订阅它，
        /// 用来决定「Boss 血条什么时候出现在屏幕上」—— 平时（N_idle）不显示，
        /// 玩家走进战斗范围、Boss 进入 CombatIdle 的那一刻才显示。</para>
        ///
        /// <para>⚠️ 每次进入战斗待机都会广播一次 true（例如攻击完回到待机也会再发），
        /// 订阅方要自己做成**幂等**的（已经在显示就别再播一次动画）。</para>
        /// </summary>
        public event Action<bool> CombatChanged;

        /// <summary>当前是否处于战斗（不在 N_idle / Return / Death）。UI 订阅事件后可以先读它同步初值。</summary>
        public bool InCombat
        {
            get
            {
                //⚠️ Return（走回原位）**不算战斗** —— 它是脱战演出，血条得是收起状态。
                //   破韧硬直（Hit）算战斗中，所以那一段不回血。
                return _fsm != null
                    && !_fsm.Is(EState.NIdle)
                    && !_fsm.Is(EState.Return)
                    && !_fsm.Is(EState.Death);
            }
        }

        /// <summary>
        /// 与 <see cref="InCombat"/> 同义，供 <see cref="CombatRegistry"/> 统一查询。
        /// <c>Return</c>（脱战走回原位）**不算**交战 —— 它是一段脱战演出。
        /// </summary>
        public override bool IsEngaged { get { return InCombat; } }

        // ==================== 配置取值（配置没挂时给安全默认，避免 NRE） ====================

        /// <summary>Boss 专用配置。<c>_config</c> 不是 BossConfig 时为 null（会在 Awake 里报错）。</summary>
        protected BossConfig BossCfg { get { return _config as BossConfig; } }

        private float CfgTurnSpeed { get { return BossCfg != null ? BossCfg.turnSpeed : 240f; } }
        //注：踱步改成"纯切向"后不再需要固定半径，原先的 CfgStrafeRadius 已删除
        //（BossConfig.strafeRadius 字段保留未用，将来若要"保持固定距离"可以再拿回来）。
        private float CfgAttackCdMin { get { return BossCfg != null ? BossCfg.attackCdMin : 1.5f; } }
        private float CfgAttackCdMax { get { return BossCfg != null ? BossCfg.attackCdMax : 3f; } }

        /// <summary>连段窗口（0~1，b 动作的进度比例）。见 <see cref="BossConfig.comboWindowStart"/>。</summary>
        private float CfgComboWindow { get { return BossCfg != null ? Mathf.Clamp01(BossCfg.comboWindowStart) : 0.55f; } }
        private float CfgAttackTimeout { get { return BossCfg != null ? BossCfg.attackTimeout : 4f; } }
        private float CfgSpecialCd { get { return BossCfg != null ? BossCfg.specialCd : 14f; } }
        private float CfgSpecialTimeout { get { return BossCfg != null ? BossCfg.specialTimeout : 6f; } }
        private float CfgHitTimeout { get { return BossCfg != null ? BossCfg.hitTimeout : 3f; } }
        private float CfgMaxPoise { get { return BossCfg != null ? BossCfg.maxPoise : 100f; } }
        private float CfgDisengageTime { get { return BossCfg != null ? BossCfg.disengageTime : 4f; } }

        /// <summary>脱战回血速率（每秒回血上限的百分比）。配置没挂时默认 5%。</summary>
        private float CfgHpRecoverPct
        {
            get { return BossCfg != null ? Mathf.Max(0f, BossCfg.hpRecoverPercentPerSecond) : 0f; }
        }

        /// <summary>非战斗状态的免伤比例（0.9 = 只吃 10%）。配置没挂时默认 0.9。</summary>
        private float CfgOutOfCombatReduction
        {
            get { return BossCfg != null ? Mathf.Clamp01(BossCfg.outOfCombatDamageReduction) : 0.9f; }
        }

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
            _fsm.Register(EState.Return, enter: EnterReturn, update: UpdateReturn, exit: ExitReturn);
            _fsm.Register(EState.Death, enter: EnterDeath);

            AttachStateMachine(_fsm);

            //⭐ 出生点 = 场景里摆的这个位置。之后脱战就走回这里（不是瞬移）。
            //   放在 Awake 而不是 Start：Start 之前没有任何状态会移动它。
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

        protected override void Start()
        {
            base.Start();
            _fsm.Start(EState.NIdle);

            //开局就在非战斗态 —— 先把免伤挂上，别等第一帧 OnAIUpdate
            UpdateOutOfCombatProtection();
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

            RecoverHpWhileOutOfCombat(dt);//脱战回血（同样与状态无关，只在非战斗时生效）
            UpdateOutOfCombatProtection();//脱战免伤（同上）
        }

        /// <summary>
        /// **非战斗免伤**：不在战斗时把 <see cref="EnemyHealth.DamageMultiplier"/> 压到
        /// <c>1 - outOfCombatDamageReduction</c>（默认 0.1），重新进战立刻恢复 1。
        ///
        /// <para>需求来源（2026-10-21）：玩家可以站在 <c>aggroRange</c>(12 m) 外一路偷血，
        /// 把正在走回原位 / 待机的 Boss 打死 —— 加 90% 免伤把这条路堵住，
        /// 同时**不希望**受击就直接重新进战（Boss 该继续走回去）。</para>
        ///
        /// <para>⚠️ 判据与回血共用 <see cref="InCombat"/>：Hit（破韧硬直）算战斗中 ⇒ 全额伤害。</para>
        /// </summary>
        private void UpdateOutOfCombatProtection()
        {
            if (Health == null) return;
            Health.DamageMultiplier = InCombat ? 1f : 1f - CfgOutOfCombatReduction;
        }

        /// <summary>
        /// **脱战回血**：每秒按血量上限的百分比回（<see cref="BossConfig.hpRecoverPercentPerSecond"/>，默认 5%/s）。
        ///
        /// <para>⭐ 只依赖 <see cref="InCombat"/> 这一个判据，所以「走回原位的路上」和「回到原位待机」都会回血，
        ///    而破韧硬直（Hit 算战斗中）、死亡（FSM 已锁）都不会回。</para>
        ///
        /// <para>⚠️ HP 是 int，而每帧回量远小于 1（600 × 5% ÷ 60fps ≈ 0.5/帧）——
        ///    必须用 <c>_hpRecoverAccum</c> 攒够整点再交出去，否则 <c>(int)</c> 取整后永远回不动。</para>
        /// </summary>
        private void RecoverHpWhileOutOfCombat(float dt)
        {
            if (InCombat) return;
            if (Health == null || Health.IsDead) return;
            if (CfgHpRecoverPct <= 0f) return;

            if (Health.CurHp >= Health.MaxHp) { _hpRecoverAccum = 0f; return; }

            _hpRecoverAccum += CfgHpRecoverPct * Health.MaxHp * dt;

            int whole = (int)_hpRecoverAccum;
            if (whole <= 0) return;

            _hpRecoverAccum -= whole;
            Health.Restore(whole);
        }

        // ==================== N_idle（未进战斗） ====================

        private void EnterNIdle()
        {
            Loco.Stop();
            SetAnimBool(P_InCombat, false);
            SetAnimBool(P_Strafe, false);
            SetAnimFloat(P_Speed, 0f);
            SetAnimFloat(P_WalkDir, 1f);//侧移方向复位，避免下次以"倒放"起步
            _outOfRangeT = 0f;

            RaiseCombat(false);//UI：Boss 血条收起
        }

        private void UpdateNIdle()
        {
            if (Target == null) return;
            if (HorizontalDistanceToTarget <= AggroRange) _fsm.Change(EState.CombatIdle);
        }

        // ==================== Return（脱战 → 走回出生点） ====================

        /// <summary>
        /// 脱战后的「回家」状态。⭐ 2026-10-21 新增。
        ///
        /// <para><b>为什么单独做一个状态</b>：需求是「退出战斗后走回原位」，而
        /// <see cref="NIdle"/> 是原地待机（<c>Loco.Stop()</c>）。要让 Boss 真的走过去，
        /// 就得有一个持续驱动 <c>Loco.MoveToOrStep</c> 的状态。</para>
        ///
        /// <para><b>三条规则</b>（对应用户 2026-10-21 的决定）：</para>
        /// <list type="number">
        ///   <item>回程中<b>玩家进入战斗范围就原地开打</b>，不要求先走到家（<see cref="UpdateReturn"/> 第一段）；</item>
        ///   <item>回程中<b>持续回血</b>（走 <see cref="RecoverHpWhileOutOfCombat"/>，与 NIdle 同一套）；</item>
        ///   <item>不带「复位后免进战」的保护 —— 复位后玩家在旁边就是正常重新进战。</item>
        /// </list>
        /// </summary>
        private void EnterReturn()
        {
            //⚠️ 三条脱战路径（CombatIdle / Chase / Hit 退出）都汇到这里，
            //   所以「清战斗残留」统一放这 —— 以前只有 CombatIdle 那条清了 CD 与韧性，
            //   Chase 脱战会把 CD / 韧性带着走。
            _atkCd = 0f;
            _spCd = 0f;
            _outOfRangeT = 0f;
            _returnT = 0f;
            _arrivedHome = false;

            SetPoise(0f);//格挡（韧性）条回满
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();

            //⭐ 动画：离开侧移、方向复位，但**不要动 `InCombat` 动画参数**。
            //   ⚠️ Animator 里 `N_idle` 是**单条待机 clip**，真正会走路的是 `Locomotion` 子状态机
            //     （`idle` / `walk_front` / `walk_left`），而进入它的条件就是 `InCombat == true`。
            //     所以这里若把 InCombat 设成 false，Boss 会切到 N_idle **滑着走回原位**（2026-10-21 实测动画图确认）。
            //   逻辑上的"不在战斗"由 `InCombat` **属性**（按 FSM 状态算）负责，跟这个动画参数是两回事。
            SetAnimBool(P_Strafe, false);
            SetAnimFloat(P_WalkDir, 1f);

            RaiseCombat(false);//UI：Boss 血条收起

            //⭐ 追击用的停靠距离是 3.6 m（免得怼进玩家身体）—— 但"回家"要精确到点，
            //   所以这一段临时压到 0.2 m，离开 Return 时在 ExitReturn 里还原。
            _savedStoppingDistance = Loco.StoppingDistance;
            Loco.StoppingDistance = ReturnStoppingDistance;

            Loco.Resume();
        }

        /// <summary>离开 Return 必走这里 —— 还原 Agent 停靠距离，否则 Chase 会贴在玩家身上。</summary>
        private void ExitReturn()
        {
            Loco.StoppingDistance = _savedStoppingDistance;
        }

        private void UpdateReturn()
        {
            _returnT += Time.deltaTime;

            //⭐ 规则①：回程中玩家又进战斗范围 → **原地**进战斗（不要求走回原位）
            if (Target != null && HorizontalDistanceToTarget <= AggroRange)
            {
                _outOfRangeT = 0f;
                _fsm.Change(EState.CombatIdle);
                return;
            }

            //---- 阶段一：还没到点，继续走 ----
            if (!_arrivedHome)
            {
                float dist = HorizontalDistance(_homePos);

                if (dist > ReturnArriveRadius && _returnT < ReturnTimeout)
                {
                    //沿用 Chase 那套：Agent 优先，没 Agent 时退回逐帧直线位移。
                    //⚠️ 它会把转向权交还 Agent（朝行进方向转）—— 所以到点后必须自己把朝向转回来。
                    Loco.MoveToOrStep(_homePos);
                    return;
                }

                _arrivedHome = true;
                Loco.Stop();//站定：停掉 Agent，把转向权收回来，否则它会继续朝行进方向转

                //⚠️ 兜底超时也会走到这里（其实没到出生点）—— 下面照样纠正朝向，见阶段二的注释。
            }

            //---- 阶段二：转回**出生时的朝向** ----
            //⭐ 不纠的话会保留"走回来那一路 Agent 给的朝向"：从 +X 方向走回来就停在 **−90°**，
            //   看起来像 Boss 回家后面朝墙。按 turnSpeed 转过去（240°/s ⇒ 转 90° 约 0.38 s），不瞬切。
            //⚠️ 这里**不区分"真的走到"还是"超时收场"** —— 两种情况都必须把朝向纠正，
            //   否则超时那条路会留在行走朝向（2026-10-21 用户报的 −90° 就出在这条路上）。
            if (Quaternion.Angle(transform.rotation, _homeRot) <= 1f)
            {
                transform.rotation = _homeRot;
                _fsm.Change(EState.NIdle);
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, _homeRot, CfgTurnSpeed * Time.deltaTime);
        }

        // ==================== 战斗待机（也是「攻击 CD 期间」的状态） ====================
        // 需求：处于攻击 CD 时「始终面向玩家，用 walkleft 动画」⇒ 本状态就是它。
        //   · CD 未就绪 → 绕玩家侧移（Strafe = true ⇒ 动画走 walkleft）
        //   · CD 就绪   → 在攻击范围就打，不在就 Chase

        private void EnterCombatIdle()
        {
            SetAnimBool(P_InCombat, true);

            //⚠️ 2026-10-21：这里**不要**再 `_outOfRangeT = 0f`。
            //   `_outOfRangeT` 的语义是「连续离开战斗范围的秒数」，而"进入战斗待机"这件事
            //   跟距离无关 —— Boss 每次出完招都会重新进 CombatIdle，以前在这里清零，
            //   等于每次出招都把脱战计时**重启**，结果玩家跑远后 Boss 反而永远不脱战
            //   （实测：Special CD 每 14 s 就会重置一次计时，Return 永远触发不了）。
            //   现在的清零只发生在 UpdateCombatIdle / UpdateChase 的「玩家确实在范围内」分支，
            //   以及脱战成功时（EnterReturn / EnterNIdle）。

            //每次进战斗待机随机一次绕圈方向，避免看起来像在跑固定圈
            if (BossCfg == null || BossCfg.randomStrafeDirection)
                _strafeDir = (UnityEngine.Random.value < 0.5f) ? -1 : 1;
            else
                _strafeDir = 1;

            RaiseCombat(true);//UI：Boss 血条出场
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
                    //CD / 韧性 / 血量上限的清理统一在 EnterReturn 里（三条脱战路径共用）
                    _fsm.Change(EState.Return);
                    return;
                }
            }
            else
            {
                _outOfRangeT = 0f;
            }

            TurnTowardTarget();//始终面向玩家（CombatIdle 属 Locomotion 三状态 ⇒ 允许转向）

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
        /// 攻击 CD 期间的「踱步」：绕玩家侧移，全程面朝玩家、播 <c>walk_left</c>。
        ///
        /// <para><b>方向取法</b>（2026-10-16 按用户给的算法简化）：把「玩家 → Boss」的水平单位向量
        /// 绕 Y 轴转 90°（<c>_strafeDir</c> ±1 决定顺/逆时针），得到的就是圆周切线方向。
        /// 这与旧版「把半径向量转 Δθ 再重新投回固定半径」在数学上等价，只是不再强制保持固定半径
        /// —— 反正只有玩家在攻击范围内时才踱步，距离自然落在 3.6~4 m 附近。
        /// （旧版半径投影的唯一作用是把"沿切线走"带来的外飘按回圆上，实测外飘仅约 1.5 cm/s，
        ///  一次 CD（1.5~3 s）也就 2~5 cm，可以忽略；因此 <c>strafeRadius</c> 现已不再被使用。）</para>
        ///
        /// <para><b>左右方向的动画映射</b>（2026-10-16 新增）：动画里侧移只有 <c>walk_left</c> 一条 clip，
        /// 所以向**自身右侧**侧移时要把这条 clip <b>倒放</b>（状态速度取负）。方向由 <see cref="P_WalkDir"/>
        /// 参数交给 Animator（见方法内注释），否则两个方向都播正向 ⇒ 必然有一半时间在"倒着滑步"。</para>
        ///
        /// <para>⚠️ 两个坑，踩住任何一个都会表现成「站桩不动」：
        /// ① <b>不能走 <c>SetDestination</c></b>：每帧步长只有 ~5 cm，会落进
        ///    <c>agent.stoppingDistance</c>（= attackRange 4 × 0.9 = 3.6 m）之内，
        ///    Agent 直接判定「已到达」⇒ 一步不走；
        /// ② <b>动画 <c>walk_left</c> 的转移条件是 <c>Speed &gt; 0.1</c></b>，而 Speed 取自实际速度，
        ///    所以速度必须真的被上报（见 <c>EnemyLocomotion.CurrentSpeed</c> 里的 _manualMove 分支）。
        /// </para>
        /// </summary>
        private void StrafeAroundTarget()
        {
            Vector3 toBoss = transform.position - Target.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.0001f) return;//与玩家重合时跳过这一帧，避免除零

            //玩家→Boss 的水平单位向量，绕 Y 轴转 ±90° ⇒ 圆周切线（绕玩家转圈的方向）
            Vector3 tangent = Quaternion.AngleAxis(90f * _strafeDir, Vector3.up) * toBoss.normalized;
            Vector3 step = tangent * (MoveSpeed * Time.deltaTime);

            SetAnimBool(P_Strafe, true);

            //⭐ 侧移左右方向（2026-10-16）：动画里只有一条 walk_left，向右侧移必须把它**倒放**。
            //   换算依据：tangent = AngleAxis(90° × _strafeDir, up) × (boss − player)，而 Boss 永远面朝玩家,
            //   「boss − player」正是它的**背后**方向；Unity 的 AngleAxis(90°, up) 把 forward 转到 right
            //   （等价 Euler(0,90,0) 朝 +X，即俯视顺时针）⇒
            //       _strafeDir = +1 → 走向自身**左**侧 → walk_left 正向（WalkDir = +1）
            //       _strafeDir = -1 → 走向自身**右**侧 → walk_left 倒放（WalkDir = -1）
            //   ⚠️ 若实测左右反了，把下面这行的判断翻过来即可（一行）。
            //   ⚠️ 前提：Animator 里要有 Float 参数 WalkDir，且 walk_left 状态的 Speed 勾了
            //      "Parameter" = WalkDir；否则本行只会触发一条"参数不存在"的警告，不会报错。
            SetAnimFloat(P_WalkDir, _strafeDir < 0 ? -1f : 1f);

            Loco.OrbitTo(transform.position + step, Target, CfgTurnSpeed);

            StrafeProbe();//⚠️ 临时诊断（验完删）
        }

        // ==================== 临时诊断（验完删） ====================
        // 目的：把"到底有没有进入踱步 / 走了多远"变成 Console 里能看的数字，不必靠肉眼判断。
        // 用法：进 Play、站到 Boss 攻击范围内，看 Console 里 [Boss] 开头的行。

        private Vector3 _probePrev;
        private float _probeAccum;
        private float _probeLastFrame = -1f;
        private float _probeNextLog = -1f;

        private void StrafeProbe()
        {
            if (_probeLastFrame < 0f)
            {
                Debug.Log(string.Format(
                    "[Boss] 踱步开始：dist={0:F2} 普攻CD={1:F2} strafeDir={2} onNavMesh={3} Speed={4:F2}",
                    HorizontalDistanceToTarget, _atkCd, _strafeDir, Loco.UsesNavMesh, Loco.CurrentSpeed), this);
                _probeAccum = 0f;
                _probeNextLog = Time.time + 1f;
            }
            else
            {
                //中间断过（跑去 Chase/Attack 了）就不把那段位移算进来
                if (Time.time - _probeLastFrame > 0.2f) { _probeAccum = 0f; _probeNextLog = Time.time + 1f; }
                else _probeAccum += Vector3.Distance(_probePrev, transform.position);
            }

            _probePrev = transform.position;
            _probeLastFrame = Time.time;

            if (Time.time >= _probeNextLog)
            {
                Debug.Log(string.Format(
                    "[Boss] 踱步实测：约 1 秒走了 {0:F2} m（期望 ≈ {1:F1} m）dist={2:F2} Speed={3:F2}",
                    _probeAccum, MoveSpeed, HorizontalDistanceToTarget, Loco.CurrentSpeed), this);
                _probeAccum = 0f;
                _probeNextLog = Time.time + 1f;
            }
        }

        // ==================== Chase（walkfront 靠近玩家） ====================

        private void EnterChase()
        {
            SetAnimBool(P_Strafe, false);
            SetAnimFloat(P_WalkDir, 1f);//离开侧移，方向复位
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
                    _fsm.Change(EState.Return);//走回出生点（不是原地待机）
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
            Loco.Stop();//⭐ 停下的同时**朝向就锁在这里**（Attack 不在 Locomotion 三状态里 ⇒ 全程不转向）

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
            //⚠️ 攻击期间**不转向**（2026-10-16 需求）：朝向在 EnterAttack 那一刻就锁死，a1/a2/a3 全程
            //   用"起手时的朝向"出招，玩家绕到背后就打空。原来这里有一行
            //   `Loco.FaceTarget(Target, CfgTurnSpeed)`，现按需求删除 —— 要转向必须先回到
            //   Locomotion 三状态（idle / walk_left / walk_front，见 CanTurn）。

            //⭐⭐ 连段窗口（2026-10-16 新增）：**b 还在播的时候就发 c**，不要等 ActionFinished()。
            //   为什么要提前：b 的出转移（a2 → Locomotion）ExitTime = 0.79，播到 79% 动画就回 Locomotion 了
            //   （Speed≈0 ⇒ 选 idle）；而 ActionFinished() 读的是"当前状态"的 normalizedTime，
            //   那时当前状态已经是**循环的 idle** ⇒ 要等它播满一整圈（1.8 s）或撞 4 s 兜底超时。
            //   结果就是观感「b 收招 → 站 idle 约 1.8 s → 才出 c」。
            //   现在播到 comboWindowStart 就把 AttackC 发出去 ⇒ 命中控制器里现成的 `a2 → a3` 专线
            //   （条件 AttackC、HasExitTime = 0），0.25 s 平滑接上，中间完全不回 Locomotion。
            if (_comboPending && !_comboFired && InComboWindow())
            {
                FireComboC(fromWindow: true);
                return;
            }

            //⚠️ ActionFinished() 内部会累加 _actT，**一帧只能调一次**（调两次会双倍计时）
            bool finished = ActionFinished();

            //兜底：万一连段窗口没抓到（比如 a2 被别的东西提前切走），仍按老逻辑在动作结束时接上 c，
            //      只是会有一次"回 idle 再起手"的顿挫 —— 总比整段连击丢掉好。
            if (finished && _comboPending && !_comboFired)
            {
                FireComboC(fromWindow: false);
                return;
            }

            if (finished) _fsm.Change(EState.CombatIdle);
        }

        /// <summary>
        /// b 是否已经播到「可以接 c」的进度。
        /// ⚠️ 三条都必须满足：① 不在过渡中（过渡时读到的 normalizedTime 是别的状态的）；
        /// ② 当前状态**确实是 a2**（否则读到的是 idle 等循环 clip 的进度，会误判）；
        /// ③ 进度 ≥ <see cref="CfgComboWindow"/>。
        /// </summary>
        private bool InComboWindow()
        {
            if (Anim == null) return false;
            if (Anim.IsInTransition(0)) return false;

            AnimatorStateInfo si = Anim.GetCurrentAnimatorStateInfo(0);
            if (si.shortNameHash != Animator.StringToHash(S_AttackB)) return false;

            return si.normalizedTime >= CfgComboWindow;
        }

        /// <summary>接上二连段的第二段 c：换判定体、复位 b 的触发器、发 AttackC、重新计时。</summary>
        /// <param name="fromWindow">true = 由连段窗口提前触发；false = 动作结束后的兜底触发。</param>
        private void FireComboC(bool fromWindow)
        {
            //⚠️ 临时诊断（验完删）：确认这次二连段是「窗口」触发（无顿挫）还是「兜底」触发（会先回 idle）
            if (Anim != null && !Anim.IsInTransition(0))
            {
                AnimatorStateInfo dbg = Anim.GetCurrentAnimatorStateInfo(0);
                Debug.Log(string.Format(
                    "[Boss] 二连段触发：{0}｜当前状态 hash={1}，进度={2:F2}，窗口阈值={3:F2}",
                    fromWindow ? "窗口（平滑接招）" : "兜底（会先回 idle）",
                    dbg.shortNameHash, dbg.normalizedTime, CfgComboWindow), this);
            }

            _comboFired = true;
            _curHitbox = _hitboxC;
            _curHitboxTime = BossCfg != null ? BossCfg.comboHitboxActiveTime : 0.25f;

            //⚠️ 先把 b 的残留触发器复位再触发 c，避免复用到下个动作
            ResetAnimTrigger(P_AttackB);
            SetAnimTrigger(P_AttackC);
            BeginAction(CfgAttackTimeout);
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
            Loco.Stop();//⭐ 朝向锁在这里（例外：sa2 全程放行，在 CanTurn 里，见 UpdateSpecial 开头注释）

            //⭐ 同样在进入动画的第一时间重置两个 CD
            _spCd = CfgSpecialCd;
            _atkCd = UnityEngine.Random.Range(Mathf.Min(CfgAttackCdMin, CfgAttackCdMax),
                                              Mathf.Max(CfgAttackCdMin, CfgAttackCdMax));

            _curHitbox = _hitboxSpecial;
            _curHitboxTime = BossCfg != null ? BossCfg.specialHitboxActiveTime : 0.35f;

            float nearMax = BossCfg != null ? BossCfg.specialNearMaxDistance : 6f;
            float dist = HorizontalDistanceToTarget;

            //⭐ 记下"这次是 sa2 还是 sa1"：sa2 **全程**允许转向（追击段 + 出招段），sa1 全程不转。
            //   ⚠️ 判据必须与下面挑动画的判据完全一致，否则会出现"走了 sa2 的动画却按 sa1 的规则锁朝向"。
            _specialIsFar = dist > nearMax;

            if (dist <= nearMax)
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
            //⚠️ 特殊攻击的转向规则（2026-10-16 用户口径）：**sa2 全程允许转向**，sa1 全程不转。
            //   · sa2 = 远距"先追后打"：追击段（_approachT > 0，位移由代码逐帧朝玩家推进）+ 出招段都跟转，
            //     否则玩家横向绕圈时会看到"斜着平移"、出招也会打偏。
            //   · sa1 = 近距原地出招：朝向保持起手方向，玩家绕背就该打空（出招有承诺）。
            //   判据写在 CanTurn 里（`_specialIsFar`），这里只管每帧调一次唯一的转向入口。
            TurnTowardTarget();

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
                }
            }

            if (ActionFinished()) _fsm.Change(EState.CombatIdle);
        }

        private void ExitSpecial()
        {
            _approachT = 0f;
            _specialIsFar = false;//离开特殊攻击 ⇒ 收回 sa2 的转向例外（回到"只允许 Locomotion 三状态转向"）
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
            Loco.Stop();//破韧硬直期间不转向（hit 不在 Locomotion 三状态里）
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
            if (!ActionFinished()) return;

            //⚠️ 2026-10-21：这里原来直接判 `dist <= AggroRange ? CombatIdle : NIdle`，
            //   **不脱战计时** —— 也就是「Boss 被打破韧、玩家刚好跑出 12 m」会一步跳到 NIdle。
            //   加了「脱战回血」之后那等于白送满血，所以改成走 Return：
            //   玩家还在范围内就近身开打，否则走回出生点。
            bool playerInRange = Target != null && HorizontalDistanceToTarget <= AggroRange;
            _fsm.Change(playerInRange ? EState.CombatIdle : EState.Return);
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
            SetAnimFloat(P_WalkDir, 1f);//侧移方向复位
            SetAnimBool(P_IsDead, true);

            _fsm.LockStateMachine();//死后不再接受任何状态切换

            RaiseCombat(false);//UI：Boss 血条该退场了（BossHealthBar 会留一小段延迟让玩家看到打空的血条）

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

        /// <summary>广播战斗状态变化（见 <see cref="CombatChanged"/>）。</summary>
        private void RaiseCombat(bool inCombat)
        {
            if (CombatChanged != null) CombatChanged(inCombat);
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

        // ==================== 转向许可（2026-10-16 需求） ====================

        /// <summary>
        /// 本状态是否允许**转向**。需求：Boss 只在 Animator 的 <c>Locomotion</c> 子状态机的**三个状态**里
        /// 能调整朝向 —— 也就是下面这三个行为状态（与动画状态是 1:1 对应的）：
        /// <list type="bullet">
        ///   <item><see cref="EState.NIdle"/> ↔ 动画 <c>idle</c>（未进战斗）</item>
        ///   <item><see cref="EState.CombatIdle"/> ↔ 动画 <c>idle</c> / <c>walk_left</c>（战斗待机 / 踱步）</item>
        ///   <item><see cref="EState.Chase"/> ↔ 动画 <c>walk_front</c>（追击）</item>
        /// </list>
        ///
        /// <para>其余状态一律**锁死朝向**：<see cref="EState.Attack"/>（a1/a2/a3）、<see cref="EState.Special"/>
        /// （仅 sa1）、<see cref="EState.Hit"/>（hit）、<see cref="EState.Death"/>（dead）—— 动作一起手就不再
        /// 跟转，玩家绕到背后会**打空**，这是「出招有承诺」的动作游戏手感（也是打击感的前提）。</para>
        ///
        /// <para>⭐ <b>唯一例外（2026-10-16 用户口径）</b>：<b>sa2「先追后打」全程允许转向</b> ——
        /// 追击段（位移是逐帧朝玩家推进的，不跟转就会"斜着平移"）和出招段一样跟转，保证打得准。
        /// 判据是 <c>_specialIsFar</c>（在 <see cref="EnterSpecial"/> 里用与挑动画**同一个**距离阈值判定）；
        /// <b>sa1（近距原地）不在例外里</b>，仍全程锁死朝向。开关沿用
        /// <see cref="Game.Data.BossConfig.specialKeepFacingPlayer"/>（默认 true = 跟转；设 false 则 sa2 也不转，
        /// 追击段表现成"背对玩家冲过去"）。</para>
        ///
        /// <para>⚠️ 为什么用**行为状态**判断、而不是去问 Animator 当前在哪个状态：0.25 s 的过渡融合期间
        /// <c>GetCurrentAnimatorStateInfo</c> 读到的是哪个状态并不确定（连段窗口那段代码就吃过这个亏，
        /// 见 <see cref="InComboWindow"/> 的注释）。而这里行为状态与实际动画是 1:1 的，判起来既确定又便宜。</para>
        ///
        /// <para>⚠️ 用的是**白名单**：将来新加的状态默认**不能**转向，要转向就得先加进这里。</para>
        /// </summary>
        private bool CanTurn
        {
            get
            {
                if (_fsm == null) return false;

                //Locomotion 三状态：动画 idle / walk_left / walk_front
                if (_fsm.Is(EState.NIdle) || _fsm.Is(EState.CombatIdle) || _fsm.Is(EState.Chase)) return true;

                //⭐ sa2 例外（见上方注释）：远距特殊攻击**全程**允许转向；sa1 不放行
                if (_fsm.Is(EState.Special))
                {
                    bool keepFacing = BossCfg == null || BossCfg.specialKeepFacingPlayer;
                    return keepFacing && _specialIsFar;
                }

                return false;
            }
        }

        /// <summary>
        /// **唯一的转向入口**：允许时把朝向拧向玩家（<see cref="CanTurn"/> 为准），否则什么都不做。
        ///
        /// <para>⚠️ 以后凡是要"让它转过来"的地方，一律调本方法，**不要**直接调 <c>Loco.FaceTarget</c>
        /// —— 否则会绕开上面的规则（攻击/特殊/硬直会莫名其妙又能转向）。</para>
        ///
        /// <para>⚠️ 注意 <c>Loco.ChaseTarget / MoveToOrStep</c> 会把转向权交还给 NavMeshAgent（由它朝
        /// 行进方向转），这条路径只出现在 <see cref="EState.Chase"/> 里，本来就属于允许转向的状态。</para>
        /// </summary>
        private void TurnTowardTarget()
        {
            if (Target == null) return;
            if (!CanTurn) return;

            Loco.FaceTarget(Target, CfgTurnSpeed);
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
                return HorizontalDistance(Target.position);
            }
        }

        /// <summary>到任意世界坐标的**水平距离**（同样忽略 Y）。脱战回程到点判定用。</summary>
        private float HorizontalDistance(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            d.y = 0f;
            return d.magnitude;
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

            //出生点（脱战走回的目标）。⚠️ _homePos 在 Awake 才赋值，编辑模式下还没值 ⇒ 用当前位置顶上。
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Application.isPlaying ? _homePos : transform.position, ReturnArriveRadius);
        }
    }
}
