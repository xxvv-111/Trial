using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// 近战敌人 AI（剑兵）。M3.2 已从「硬编码 switch 状态机」迁移到
    /// <see cref="EnemyStateMachine{TState}"/>（三字典表驱动），行为与迁移前逐条对齐。
    ///
    /// 本类现在只负责三件事：**状态表 / 转移条件 / 动画与判定体的时机**。
    /// 组件、数值、受击反应、移动分层、视野感知都在基类与
    /// <see cref="EnemyPerception"/> / <see cref="EnemyLocomotion"/> 里。
    ///
    /// 状态转移（GDD §6.2）：
    /// <code>
    /// Idle       ──看见玩家（距离 + 扇形 + 视线）──> Alert
    /// Alert      ──停顿 alertTime 秒（这段时间只转身）──> Chase
    /// Chase      ──距离 ≤ attackRange──> Attack ──后摇结束──> Reposition
    /// Reposition ──退开 repositionDistance 米（到位 / 超时 / 玩家又贴脸）──> Chase
    /// 任意        ──受击（小怪）──> Hit ──硬直结束──> Chase
    /// 任意        ──生命 ≤ 0──> Death（锁死状态机）
    /// </code>
    ///
    /// ⚠️ GDD §6.2 的 `Windup` / `Recover` 在本实现里**没有单独成状态** ——
    ///    它们由攻击状态内的计时（`windupTime` / `recoverTime`）表达，迁移前就是这样，保持不动。
    ///
    /// ✅ **M3.4 已完成**：补上 <c>Alert</c>（转朝向 + 短暂停顿）与 <c>Reposition</c>（攻击后拉开距离）。
    /// ⚠️ 仍缺**敌人动画状态机**（M3.6，`EnemyAC` 只有 2 个状态）—— 所以 `Alert` / `Reposition`
    ///    目前**没有对应动画触发**（刻意不调 <c>SetAnimTrigger</c>，免得给控制台添无谓的"参数不存在"警告）。
    /// </summary>
    public class EnemyMeleeAI : EnemyAIController
    {
        public enum EState { Idle, Alert, Chase, Attack, Reposition, Hit, Death }

        [Tooltip("判定体开启后持续多久自动关闭（秒）。需与攻击动画的判定窗口匹配。")]
        [SerializeField] private float _hitboxActiveTime = 0.25f;

        [Tooltip("敌人攻击判定体的名字（挂在敌人预制体下、Layer = EnemyHitbox）。")]
        [SerializeField] private string _attackHitboxName = "Hitbox_Attack";

        //强类型状态机由子类自己持有（基类不能是泛型，见 EnemyAIController 的类注释）
        private EnemyStateMachine<EState> _fsm;

        private float _atkT;   //进入攻击状态后经过的秒数
        private float _stunT;  //受击硬直剩余秒数
        private float _alertT; //Alert 停顿已过秒数
        private float _repoT;  //Reposition 已过秒数（超时兜底用）
        private Vector3 _repoPoint;//Reposition 的目标点（已吸附到导航网）

        // ==================== 状态表 ====================

        protected override void Awake()
        {
            base.Awake();

            _fsm = new EnemyStateMachine<EState>(name);

            _fsm.Register(EState.Idle, update: UpdateIdle);
            _fsm.Register(EState.Alert, enter: EnterAlert, update: UpdateAlert);
            _fsm.Register(EState.Chase, enter: EnterChase, update: UpdateChase, exit: ExitChase);
            _fsm.Register(EState.Attack, enter: EnterAttack, update: UpdateAttack, exit: ExitAttack);
            _fsm.Register(EState.Reposition, enter: EnterReposition, update: UpdateReposition);
            _fsm.Register(EState.Hit, update: UpdateHit);
            _fsm.Register(EState.Death, enter: EnterDeath);

            AttachStateMachine(_fsm);
        }

        protected override void Start()
        {
            base.Start();
            _fsm.Start(EState.Idle);
        }

        // ==================== Idle ====================

        private void UpdateIdle()
        {
            //⚠️ 与迁移前的区别：不再只看距离，改为「距离 + 视野扇形 + 视线遮挡」（GDD §6.1）
            //⭐ M3.4：发现玩家先进 Alert（站住转身、短暂停顿），不再"看见就起跑"
            if (CanSeeTarget) _fsm.Change(EState.Alert);
        }

        // ==================== Alert（M3.4 · GDD §6.2：转朝向 + 短暂停顿） ====================

        private void EnterAlert()
        {
            _alertT = 0f;
            Loco.Stop();//先站住：这段时间只转身、不移动（Agent 停下，避免"边转头边滑步"）
        }

        private void UpdateAlert()
        {
            if (Target == null) { _fsm.Change(EState.Idle); return; }

            Loco.FaceTarget(Target);//只转身（下一段 Chase 的 MoveTo 会把转向权交还给 Agent）

            //⚠️ 刻意不调 SetAnimTrigger("Alert")：敌人动画状态机（M3.6）还没有这个参数，
            //   调了只会往控制台添一条无谓的"参数不存在"警告。M3.6 时再补。
            _alertT += Time.deltaTime;
            if (_alertT >= AlertTime) _fsm.Change(EState.Chase);
        }

        // ==================== Chase ====================

        private void EnterChase()
        {
            Loco.Resume();//上一次攻击 / 硬直时停下的 Agent，这里恢复
        }

        private void UpdateChase()
        {
            if (Target == null) return;

            //进入攻击距离 → 发起攻击（时序与迁移前一致）
            if (SqrDistanceToTarget <= AttackRange * AttackRange)
            {
                _fsm.Change(EState.Attack);
                return;
            }

            //丢失目标太久就放弃（loseTargetTime = 0 时永不放弃，等价于迁移前的行为）
            if (LoseTargetTime > 0f && Perception != null && Perception.TimeSinceLastSeen > LoseTargetTime)
            {
                _fsm.Change(EState.Idle);
                return;
            }

            //Agent 优先；没 Agent / 没烘焙时自动退回直线位移
            Loco.ChaseTarget(Target);
        }

        private void ExitChase()
        {
            Loco.Stop();
        }

        // ==================== Attack ====================

        private void EnterAttack()
        {
            _atkT = 0f;
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();//保险：进入时先清干净
            SetAnimTrigger("Attack");
        }

        private void UpdateAttack()
        {
            Loco.FaceTarget(Target);//攻击期间只转向不动（Agent 已停）

            _atkT += Time.deltaTime;
            //⭐ M3.4：后摇结束先**拉开距离**再回追击（GDD §6.2「不应贴脸」）。
            //   ⚠️ repositionDistance = 0 时直接回 Chase —— 且刻意**不在 enter 回调里调 Change**
            //       （那会在 Change 内部再触发一次 Change，递归语义容易踩坑），条件放在这里判。
            if (_atkT >= RecoverTime)
                _fsm.Change(RepositionDistance > 0f ? EState.Reposition : EState.Chase);
        }

        private void ExitAttack()
        {
            //正常收招 / 被打断都要清，避免「已经不打人了，判定还生效」
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();
        }

        // ==================== Reposition（M3.4 · GDD §6.2：攻击后退开，避免贴脸） ====================

        private void EnterReposition()
        {
            _repoT = 0f;

            //目标点 = 沿「背离玩家」的方向退开 repositionDistance 米
            Vector3 away;
            if (Target != null)
            {
                away = transform.position - Target.position;
                away.y = 0f;
            }
            else
            {
                away = -transform.forward;//目标丢了就沿自己背后退
            }

            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;//与玩家重合时随便挑一个方向，避免除零
            Vector3 want = transform.position + away.normalized * RepositionDistance;

            //⚠️ **必须吸附到导航网**（NAVMESH-GUIDE §9）：把网格外的点直接丢给 Agent，
            //   它会走到"最近的合法点"甚至原地打转。
            //   半径取 0.3 —— 要**大于导航网的 y 偏移**（本工程约 0.066），用 0.05 会全判成"不在网格上"。
            NavMeshHit hit;
            if (NavMesh.SamplePosition(want, out hit, 0.3f, NavMesh.AllAreas))
                want = hit.position;

            _repoPoint = want;
            Loco.MoveToOrStep(want);
        }

        private void UpdateReposition()
        {
            _repoT += Time.deltaTime;

            //玩家又贴上来了 → 不退了，直接回追击（下一帧就会因进入攻击范围而开打）
            if (SqrDistanceToTarget <= AttackRange * AttackRange)
            {
                _fsm.Change(EState.Chase);
                return;
            }

            //到位（水平差 < 0.25 m）或超时 → 回追击
            Vector3 d = _repoPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= 0.0625f || _repoT >= RepositionTimeout)
            {
                _fsm.Change(EState.Chase);
                return;
            }

            Loco.MoveToOrStep(_repoPoint);
            //⚠️ 这里**不** FaceTarget：Agent 走位时朝向由它自己管（朝行进方向）。
            //   强行接管会和 Agent 的自动转向打架（EnemyLocomotion 里 MoveTo 每帧会把转向权交还 Agent），
            //   表现是抖动。所以后退时是"转身走开"，走完由 Chase 再转回来面对玩家。
        }

        /// <summary>
        /// 后退的**超时保护**：按正常速度走过去只要 <c>distance / speed</c> 秒，给 2 倍余量。
        /// 没有它的话，目标点被柱子挡住（永远到不了位）会让敌人**永久卡在 Reposition**。
        /// </summary>
        private float RepositionTimeout
        {
            get { return Mathf.Max(0.3f, RepositionDistance / Mathf.Max(0.1f, MoveSpeed) * 2f); }
        }

        // ==================== Hit ====================

        private void UpdateHit()
        {
            _stunT -= Time.deltaTime;
            if (_stunT <= 0f) _fsm.Change(EState.Chase);
        }

        protected override void OnInterrupted(int damage)
        {
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();//先关判定体：已经被打断了，判定不该再生效

            Loco.Retreat(KnockBackDistance);
            Loco.Stop();

            _stunT = HitStunTime;
            PlayHitAnim();//M3.6：触发 hit 动画（内部会先复位残留的 Attack 触发器）
            _fsm.Change(EState.Hit);
        }

        // ==================== Death ====================

        /// <summary>
        /// 致死一击（来自基类 <c>HandleDamaged</c>）。基类拿不到 <see cref="EState"/>，所以由这里切。
        /// ⚠️ 在此之前**必须**已经跑过 <see cref="EnemyHealth.Damaged"/>，
        ///   因为 <see cref="EnemyHealth"/> 随后就会按延迟把物体隐藏掉。
        /// </summary>
        protected override void OnKilled()
        {
            _fsm.Change(EState.Death);
        }

        private void EnterDeath()
        {
            Loco.Stop();
            if (Hitboxes != null) Hitboxes.DisableAllHitboxes();
            PlayDeathAnim();//M3.6：设 IsDead 参数，驱动动画状态机的 AnyState → death
            _fsm.LockStateMachine();//死后不再接受任何状态切换
        }

        // ==================== 攻击命中（M1.2：走判定体） ====================

        /// <summary>
        /// 动画关键帧事件（挂在 enemy 的攻击动画上）。
        /// M1.2 起改为**开启敌人判定体**，与玩家侧规则对称（GDD 设计支柱 4）。
        /// </summary>
        private void TickAttack()
        {
            if (Hitboxes != null)
            {
                Hitboxes.EnableHitbox(_attackHitboxName, _hitboxActiveTime);
                return;
            }

            //兜底：未配判定体时退回直接结算，避免「完全打不到玩家」
            Debug.LogWarning("[EnemyMeleeAI] 未配置 HitboxController，退回直接结算：" + name, this);

            if (TargetFsm == null || Target == null) return;

            float d = (Target.position - transform.position).sqrMagnitude;
            if (d <= AttackRange * AttackRange * 1.2f && !TargetFsm.Invulnerable)
                TargetFsm.TakeDamage(Damage);
        }

        // ==================== Scene 视图 ====================

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            //⚠️ 追击范围由 EnemyPerception 自己画（扇形 + 距离圈），这里只补攻击范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _config.attackRange);
        }
    }
}
