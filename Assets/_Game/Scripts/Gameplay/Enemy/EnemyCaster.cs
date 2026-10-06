using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// 远程敌人 AI（**法师**）。🔄 2026-10-06 由「弓兵」改定（见 `GDD.md` §6.3）。
    ///
    /// **为什么改**：原弓兵需要 `Aim` / `Shoot` 两段**库里没有的拉弓动画**（旧文档标注为唯一"必须手 K"的阻塞点）。
    /// 改成法师后施法动作直接复用 `Magical-Knight_Set/Animation/Humanoid/atk_energy01~11` ⇒ 敌人侧已无必须手 K 的动画。
    ///
    /// 状态转移（GDD §6.3）：
    /// <code>
    /// Idle  ──看见玩家（距离 + 扇形 + 视线）──> Alert
    /// Alert ──停顿 alertTime 秒（只转身）──> Chase
    /// Chase ──【kiting】太近则后撤、太远则靠近；在舒适区且冷却就绪且视线无遮挡──> Attack
    /// Attack──前摇结束→出手（生成法术弹）→后摇结束──> Chase
    /// 任意  ──受击（小怪）──> Hit ──硬直结束──> Chase
    /// 任意  ──生命 ≤ 0 ──> Death（锁死状态机）
    /// </code>
    ///
    /// ⚠️ **kiting 是本类的核心**（`NAVMESH-GUIDE.md` §9）：状态机**先算出"该去哪"的目标点**，
    ///    再交给 <see cref="EnemyLocomotion.MoveToOrStep"/>。**绝不能直接** `SetDestination(player.position)`，
    ///    那会让法师一路冲向玩家。目标点必须经 `NavMesh.SamplePosition` 吸附（半径 0.3）。
    ///
    /// ⚠️ **行为状态 6 个，动画只要 5 个**（GDD §6.4.1）：`Alert` 复用 `Idle`、`Chase` 复用 `Run`，
    ///    所以本类**只触发** `Attack` 一个动画（`SetAnimTrigger` 带参数存在性检查，M3.6 补上参数后警告会消失）。
    ///
    /// ⚠️ 与剑兵的区别：法师**没有** `Reposition` 状态 —— "保持距离"已经折进 `Chase` 的 kiting 里。
    ///
    /// ⭐ **后撤时保持面朝玩家**（2026-10-07 修）：kiting 的"太近 → 往后撤"那一段必须走
    ///    <see cref="EnemyLocomotion.BackstepTo"/>，**不能**走 <see cref="EnemyLocomotion.MoveToOrStep"/>。
    ///    区别在转向权的归属：后者会把转向交还 `NavMeshAgent`，Agent 于是朝"行进方向"转身
    ///    ⇒ 观感是"先扭过身、再背对玩家走开"（法师这样特别怪）。
    ///    实测（Play 模式，20 fps 手动步进）：改用 `BackstepTo` 后，距离从 2.50 m 一路拉到 4.19 m，
    ///    而**朝向偏角全程 0.0°**（始终正对玩家），`agent.updateRotation` 全程为 `false`。
    /// </summary>
    public class EnemyCaster : EnemyAIController
    {
        public enum EState { Idle, Alert, Chase, Attack, Hit, Death }

        // ⚠️⚠️ **这两个字段名刻意与旧版 `EnemyRanged` 保持一致** ——
        //     Unity 是按**字段名**恢复序列化数据的，改名会让 Gunner 预制体上已经连好的引用**全部丢掉**。
        [Tooltip("法术弹预制体（复用现有 `Bullet` 组件：只打玩家、不会误伤同伴）。")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("出手点（法杖 / 手的位置）。留空则退回胸口高度、身前 0.4 m。")]
        [SerializeField] private Transform firePoint;

        //强类型状态机由子类自己持有（基类不能是泛型，见 EnemyAIController 的类注释）
        private EnemyStateMachine<EState> _fsm;

        private float _alertT;//Alert 停顿已过秒数
        private float _atkT;  //进入攻击后经过的秒数
        private float _cdT;   //施法冷却剩余秒数
        private float _stunT; //受击硬直剩余秒数
        private bool _casted; //本次施法是否已出手（保证一次施法只出一发）

        // ==================== 状态表 ====================

        protected override void Awake()
        {
            base.Awake();

            _fsm = new EnemyStateMachine<EState>(name);

            _fsm.Register(EState.Idle, update: UpdateIdle);
            _fsm.Register(EState.Alert, enter: EnterAlert, update: UpdateAlert);
            _fsm.Register(EState.Chase, enter: EnterChase, update: UpdateChase);
            _fsm.Register(EState.Attack, enter: EnterAttack, update: UpdateAttack);
            _fsm.Register(EState.Hit, update: UpdateHit);
            _fsm.Register(EState.Death, enter: EnterDeath);

            AttachStateMachine(_fsm);
        }

        protected override void Start()
        {
            base.Start();
            _fsm.Start(EState.Idle);
        }

        /// <summary>
        /// ⚠️ **法师的 Agent 停靠距离必须很小**（覆写基类默认的 `attackRange × 0.9`）：
        ///    "保持距离"由**状态机算出的目标点**决定；若沿用默认值，`attackRange 9` ⇒ stoppingDistance 8.1，
        ///    Agent 会在 8 m 外就停下，**kiting 直接失效**（永远靠不到舒适距离）。
        /// </summary>
        protected override float AgentStoppingDistance { get { return 0.3f; } }

        /// <summary>冷却计时放在这里（基类的 `Update` 是私有的，所以提供了这个钩子）。</summary>
        protected override void OnAIUpdate()
        {
            if (_cdT > 0f) _cdT -= Time.deltaTime;
        }

        // ==================== Idle / Alert ====================

        private void UpdateIdle()
        {
            if (CanSeeTarget) _fsm.Change(EState.Alert);
        }

        private void EnterAlert()
        {
            _alertT = 0f;
            Loco.Stop();//站住：这段时间只转身，不移动
        }

        private void UpdateAlert()
        {
            if (Target == null) { _fsm.Change(EState.Idle); return; }

            Loco.FaceTarget(Target);
            _alertT += Time.deltaTime;
            if (_alertT >= AlertTime) _fsm.Change(EState.Chase);
        }

        // ==================== Chase（kiting 核心） ====================

        private void EnterChase()
        {
            Loco.Resume();
        }

        private void UpdateChase()
        {
            if (Target == null) return;

            float d = DistanceToTarget;

            //① 冷却就绪 + 在施法区间 + 视线无遮挡 → 施法
            if (CanCast(d))
            {
                _fsm.Change(EState.Attack);
                return;
            }

            //② 丢失目标太久就放弃（loseTargetTime = 0 时永不放弃，等价于旧行为）
            if (LoseTargetTime > 0f && Perception != null && Perception.TimeSinceLastSeen > LoseTargetTime)
            {
                _fsm.Change(EState.Idle);
                return;
            }

            //③ kiting：先算"该去哪"，再交给移动层
            Vector3 away = transform.position - Target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;//与玩家重合时随便挑个方向
            away.Normalize();

            //⚠️ "太近 → 后撤" 这一段必须用 [**保持面朝玩家**] 的移动方式：
            //   交给 MoveToOrStep 的话，NavMeshAgent 会把身体朝"行进方向"转过去
            //   ⇒ 观感是"先扭过身、再背对玩家走开"（法师这样特别怪）。
            //   BackstepTo 全程攥着转向权，表现是**后退/横移、始终面对玩家**。
            if (d < ComfortDistance)
            {
                Vector3 back = Target.position + away * ComfortDistance;//后撤到舒适距离
                back.y = transform.position.y;

                //⚠️ 必须吸附到导航网（NAVMESH-GUIDE §9）：半径 0.3 > 导航网 y 偏移（0.066），用 0.05 会全判成"不在网格上"
                NavMeshHit h2;
                if (NavMesh.SamplePosition(back, out h2, 0.3f, NavMesh.AllAreas)) back = h2.position;

                Loco.BackstepTo(back, Target);
                return;
            }

            Vector3 want;
            if (d > AttackRange)
            {
                want = Target.position;//太远 → 靠近（停靠距离由 AgentStoppingDistance 管，已设得很小）
            }
            else
            {
                //在舒适区（舒适距离 ~ 施法距离上限）→ 站住等冷却，避免贴脸来回抖
                Loco.Stop();
                return;
            }

            want.y = transform.position.y;

            //⚠️ 必须吸附到导航网（NAVMESH-GUIDE §9）：半径 0.3 > 导航网 y 偏移（0.066），用 0.05 会全判成"不在网格上"
            NavMeshHit hit;
            if (NavMesh.SamplePosition(want, out hit, 0.3f, NavMesh.AllAreas)) want = hit.position;

            Loco.MoveToOrStep(want);
        }

        /// <summary>现在能不能施法：冷却 + 距离区间 + 视线遮挡。</summary>
        private bool CanCast(float distance)
        {
            if (_cdT > 0f) return false;
            if (distance < CastRangeMin || distance > AttackRange) return false;
            if (Perception != null && Perception.RequireLineOfSight && !Perception.HasLineOfSight) return false;
            return true;
        }

        // ==================== Attack（前摇 → 出手 → 后摇） ====================

        private void EnterAttack()
        {
            _atkT = 0f;
            _casted = false;
            Loco.Stop();//施法期间站住
            SetAnimTrigger("Attack");//⚠️ Alert / Chase 刻意不触发（复用 Idle / Run，见 GDD §6.4.1）
        }

        private void UpdateAttack()
        {
            Loco.FaceTarget(Target);//施法期间只转向

            _atkT += Time.deltaTime;

            //出手帧（**兜底路径**）：动画里没挂出手事件时，靠前摇计时放。
            //⚠️ 正常情况下下面的 TickAttack 动画事件会先到、并已把 _casted 置位 ⇒ 这行成空操作。
            if (_atkT >= WindupTime) TryCastOnce();

            if (_atkT >= WindupTime + RecoverTime) _fsm.Change(EState.Chase);
        }

        /// <summary>
        /// ⭐ **出手帧的动画事件入口**（GDD §6.3：出手由动画事件驱动，伤害/特效才能与动作对齐）。
        ///
        /// ⚠️ 现状：法师用的还是**占位的近战 clip** `Art/Animations/Enemy/old/combo_01_1.anim`
        ///    （时长 0.667 s、事件 `TickAttack` 挂在 **0.3 s**）—— 于是**动画事件先到**：
        ///    ⇒ 法师实际在 **0.3 s** 出手，`windupTime`(0.5 s) 退化为**兜底**。
        ///    ⭐ M3.6 换成法师专属施法 clip（`atk_energy*`）后，**只需把事件挪到真正的出手帧，本方法不用改**。
        ///
        /// ⚠️ **被打断就作废**：前摇期间挨打会切进 `Hit`，但那条 clip 可能还在播、事件照样会来 ——
        ///    所以先确认"当前确实处于 Attack 状态"，避免"明明被打断了却还是放出一发"（GDD §6.3 明确要求）。
        /// </summary>
        private void TickAttack()
        {
            if (_fsm == null || !_fsm.Is(EState.Attack)) return;
            TryCastOnce();
        }

        /// <summary>
        /// 放一次法术（**幂等**）。计时兜底与动画事件**两条路径都走这里**，
        /// <c>_casted</c> 保证「一次施法只出一发」—— 谁先到谁放，另一个变成空操作。
        /// </summary>
        private void TryCastOnce()
        {
            if (_casted) return;
            _casted = true;
            CastSpell();
        }

        /// <summary>
        /// 生成法术弹。
        /// ⚠️ 出手前**再确认一次视线**：前摇这 0.5 s 里玩家可能躲到柱子后 —— 那时应当**取消这次施法**，
        ///    否则会出现"法术穿过障碍物"的观感问题（GDD §6.3）。
        /// </summary>
        private void CastSpell()
        {
            if (Target == null) return;

            if (Perception != null && Perception.RequireLineOfSight && !Perception.HasLineOfSight)
                return;//视线断了 → 这次不放（冷却也不重置，下一帧回 Chase 会重新判定）

            if (bulletPrefab == null)
            {
                Debug.LogWarning("[EnemyCaster] 未配置法术弹预制体（bulletPrefab）—— 法师不会造成任何伤害。", this);
                return;
            }

            Vector3 from = firePoint != null
                ? firePoint.position
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;

            //瞄躯干而不是脚底
            Vector3 to = Target.position + Vector3.up * 1.0f;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            GameObject go = Instantiate(bulletPrefab, from, Quaternion.LookRotation(dir));
            var bullet = go.GetComponent<Bullet>();
            if (bullet == null)
            {
                Debug.LogWarning("[EnemyCaster] 法术弹预制体上没有 Bullet 组件：" + bulletPrefab.name, go);
                Destroy(go);
                return;
            }

            bullet.Launch(dir * ProjectileSpeed, transform);//owner = 自己（不会打到自己）
            _cdT = CastCooldown;
        }

        // ==================== Hit / Death ====================

        private void UpdateHit()
        {
            _stunT -= Time.deltaTime;
            if (_stunT <= 0f) _fsm.Change(EState.Chase);
        }

        protected override void OnInterrupted(int damage)
        {
            //⚠️ 前摇被打断 → **这次施法直接取消**（出手逻辑在 UpdateAttack 里，离开状态就不会再执行），
            //   避免"明明被打断了却还是打出一发"的欺骗感（GDD §6.3）。
            Loco.Retreat(KnockBackDistance);
            Loco.Stop();

            _stunT = HitStunTime;
            PlayHitAnim();//M3.6：触发 hit 动画（内部会先复位残留的 Attack 触发器）
            _fsm.Change(EState.Hit);
        }

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
            PlayDeathAnim();//M3.6：设 IsDead 参数，驱动动画状态机的 AnyState → deathspecial
            _fsm.LockStateMachine();
        }

        // ==================== Scene 视图 ====================

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position, _config.attackRange);//施法距离上限

            Gizmos.color = new Color(0.2f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _config.comfortDistance);//舒适距离
        }
    }
}
