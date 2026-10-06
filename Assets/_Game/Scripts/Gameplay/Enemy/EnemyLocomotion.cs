using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// 敌人移动层（M3.2 的「**把选目标点与交给 Agent 走分层**」，NAVMESH-GUIDE §7 / §9）。
    ///
    /// 为什么单独一层：
    /// <list type="bullet">
    ///   <item>状态机只管**算出「该去哪」**，不关心是 Agent 走还是自己挪 —— 两种敌人共用同一套调用</item>
    ///   <item>⚠️ 为了不出现「抖动 / 抽搐 / 瞬移」，**位置只有一个写入者**：
    ///        有 Agent 时全部交给 Agent，本类绝不写 <c>transform.position</c></item>
    /// </list>
    ///
    /// ⚠️ **无 NavMesh 时的兜底**：如果同物体上没有 <see cref="NavMeshAgent"/>，
    ///    或者 Agent 不在导航网格上（NavMesh 还没烘焙 / 没吸附上去），
    ///    本类会**退回直线位移**（等价于 M3 之前的 <c>MoveTowardPlayer()</c>），
    ///    这样没烘焙前敌人也不会站着不动。烘焙完成后自动切到 Agent，**不用改调用方代码**。
    ///
    /// ⚠️ 本类是普通 C# 类（不是 MonoBehaviour），由 <see cref="EnemyAIController{TState}"/> 持有，
    ///    省掉每个敌人预制体都要多挂一个组件的麻烦。
    /// </summary>
    public class EnemyLocomotion
    {
        private readonly Transform _self;
        private readonly NavMeshAgent _agent;
        private float _speed;

        //目标点缓存：NavMeshAgent.SetDestination 不必每帧重复调用（玩家原地不动时纯属浪费）
        private Vector3 _lastDestination;
        private bool _hasDestination;
        private const float RepathThreshold = 0.25f;//目标点移动超过这个距离才重设路径

        private bool _warnedNoNavMesh;

        public EnemyLocomotion(Transform self, float speed, float stoppingDistance)
        {
            _self = self;
            _speed = speed;
            _agent = self.GetComponent<NavMeshAgent>();

            if (_agent == null) return;

            _agent.speed = speed;
            _agent.stoppingDistance = stoppingDistance;
            _agent.updateRotation = true;
            _agent.autoBraking = true;
        }

        /// <summary>同物体上是否挂了 NavMeshAgent。</summary>
        public bool HasAgent { get { return _agent != null; } }

        /// <summary>是否真的在用 Agent 驱动（有 Agent 且它已在导航网格上）。</summary>
        public bool UsesNavMesh { get { return _agent != null && _agent.isOnNavMesh; } }

        public float Speed
        {
            get { return _speed; }
            set
            {
                _speed = value;
                if (_agent != null) _agent.speed = value;
            }
        }

        /// <summary>
        /// 本帧的**实际水平速度**（m/s），供动画层驱动「待机 ↔ 移动」两个状态的切换。
        ///
        /// ⚠️ 两种驱动方式要分别取：
        ///   · 走 Agent：<c>agent.velocity</c> 是真实瞬时速度 —— 起步/刹车/被 RVO 挤到时都能如实反映。
        ///     若改用"是否收到过 MoveTo 指令"当判据，敌人被同伴挤住时会出现"原地跑"。
        ///   · 无 Agent（直线位移兜底）：没有速度概念，用"最近有没有真的走过"近似成满速或 0 ——
        ///     带 0.15 s 宽限是因为 <see cref="DirectStep"/> 只在状态机每帧调过来时才执行，
        ///     而读速度发生在同一帧更早的位置（否则永远读到 0）。
        /// </summary>
        public float CurrentSpeed
        {
            get
            {
                if (_agent != null && _agent.isOnNavMesh)
                {
                    Vector3 v = _agent.velocity;
                    return new Vector2(v.x, v.z).magnitude;
                }

                return (Time.time - _lastStepTime <= 0.15f) ? _speed : 0f;
            }
        }

        /// <summary>最近一次直线位移的时间（只在无 Agent 兜底路径上用，见 <see cref="CurrentSpeed"/>）。</summary>
        private float _lastStepTime = -999f;

        // ==================== 「保持面朝目标」的后退/侧移模式 ====================

        /// <summary>true 时移动期间由我们接管转向（不交还 Agent），从而保持面朝 <see cref="_faceTarget"/>。</summary>
        private bool _keepFacing;
        private Transform _faceTarget;
        private float _faceDegPerSec = 720f;

        // ==================== 追击 ====================

        /// <summary>朝目标移动（Agent 优先，退回直线位移）。状态机的 Chase 直接调这个。</summary>
        public void ChaseTarget(Transform target)
        {
            if (target == null) return;
            MoveToOrStep(target.position);
        }

        /// <summary>
        /// 走到某个世界坐标点：**Agent 优先，没 Agent 时退回逐帧直线位移**。
        /// 状态机只要"算出该去哪"就直接调这个，不必自己判断有没有 Agent。
        /// （M3.4 的 `Reposition` 后退就是用本方法走向"背离玩家"的目标点）
        ///
        /// ⚠️ 本方法会把转向权**交还 Agent** —— Agent 会朝"行进方向"转身。
        ///    要"一边后退、一边保持面朝目标"（法师 kiting / 面朝玩家倒退）请用 <see cref="BackstepTo"/>。
        /// </summary>
        public void MoveToOrStep(Vector3 worldPoint)
        {
            _keepFacing = false;
            _faceTarget = null;

            if (MoveTo(worldPoint)) return;
            DirectStep(worldPoint);
        }

        /// <summary>
        /// ⭐ **后退 / 侧移**：走到某点，但**移动期间保持面朝 <paramref name="faceTarget"/>**。
        ///
        /// 与 <see cref="MoveToOrStep"/> 的唯一区别是**转向权的归属**：
        /// 那个会把转向交还 Agent，Agent 于是朝行进方向转身 ⇒ 表现成"先扭过身、再背对玩家走开"；
        /// 这个全程不交还（<c>updateRotation = false</c>），由本类每帧 <c>RotateTowards</c> 攥着朝向。
        ///
        /// 用途：远程敌人被贴近时的后撤（kiting），以及将来"面朝玩家倒退收招"的走位。
        /// ⚠️ 每帧调一次即可；<see cref="Stop"/> / <see cref="Resume"/> / 下一次 <see cref="MoveToOrStep"/> 都会解除本模式。
        /// </summary>
        public void BackstepTo(Vector3 worldPoint, Transform faceTarget, float degreesPerSecond = 720f)
        {
            _keepFacing = true;
            _faceTarget = faceTarget;
            _faceDegPerSec = degreesPerSecond;

            if (MoveTo(worldPoint)) return;
            DirectStep(worldPoint);
        }

        /// <summary>走到某个世界坐标点。返回 false 表示没在用 Agent（调用方需自行决定兜底方式）。</summary>
        public bool MoveTo(Vector3 worldPoint)
        {
            if (!UsesNavMesh)
            {
                WarnIfNoNavMesh();
                return false;
            }

            //⚠️ 转向权归谁：默认交还 Agent（它朝行进方向转）；"保持面朝目标"模式必须由我们攥着 ——
            //   否则 Agent 每帧会把朝向掰回行进方向 ⇒ 后撤时变成背对玩家走开。
            if (_keepFacing)
            {
                if (_agent.updateRotation) _agent.updateRotation = false;
            }
            else
            {
                if (!_agent.updateRotation) _agent.updateRotation = true;
            }

            _agent.isStopped = false;

            if (!_hasDestination || (_lastDestination - worldPoint).sqrMagnitude > RepathThreshold * RepathThreshold)
            {
                _agent.SetDestination(worldPoint);
                _lastDestination = worldPoint;
                _hasDestination = true;
            }

            if (_keepFacing && _faceTarget != null) FacePoint(_faceTarget.position, _faceDegPerSec);

            return true;
        }

        /// <summary>无 Agent 时的兜底：直接朝目标转向 + 直线位移。</summary>
        private void DirectStep(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - _self.position;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.01f) return;

            _lastStepTime = Time.time;//给 CurrentSpeed 用（见该属性注释）
            if (!_keepFacing) _self.rotation = Quaternion.LookRotation(dir.normalized);
            _self.position += dir.normalized * (_speed * Time.deltaTime);

            if (_keepFacing && _faceTarget != null) FacePoint(_faceTarget.position, _faceDegPerSec);
        }

        // ==================== 停下 / 恢复 ====================

        /// <summary>
        /// 进入「收费动作」（攻击 / 前摇）时停下。
        /// ⚠️ 用 <c>isStopped</c>，**不要用已过时的 <c>Stop()</c>**（NAVMESH-GUIDE §4.2）。
        /// </summary>
        public void Stop()
        {
            _hasDestination = false;//下次 MoveTo 必须重新寻路
            _lastStepTime = -999f;//无 Agent 兜底路径下，立刻把 CurrentSpeed 归零

            //解除"保持面朝目标"模式。⚠️ 只解除模式、**不动朝向** ⇒ 停下后仍然面朝刚才的目标
            _keepFacing = false;
            _faceTarget = null;

            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>动作结束，恢复移动。</summary>
        public void Resume()
        {
            _hasDestination = false;

            _keepFacing = false;
            _faceTarget = null;

            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
        }

        // ==================== 转向 ====================

        /// <summary>
        /// 手动转向目标（攻击 / 瞄准时用）。
        /// ⚠️ 会先接管转向权（<c>updateRotation = false</c>），否则会和 Agent 的自动转向打架；
        ///    下一次 <see cref="MoveTo"/> 时再交还给它。
        /// </summary>
        public void FaceTarget(Transform target, float degreesPerSecond = 720f)
        {
            //显式手动转向 ⇒ 解除"保持面朝目标"的后退模式（本方法自己就在管朝向）
            _keepFacing = false;
            _faceTarget = null;

            if (target == null) return;
            FacePoint(target.position, degreesPerSecond);
        }

        /// <summary>手动转向某个世界坐标点。</summary>
        public void FacePoint(Vector3 worldPoint, float degreesPerSecond = 720f)
        {
            Vector3 dir = worldPoint - _self.position;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.01f) return;

            if (_agent != null && _agent.isOnNavMesh && _agent.updateRotation)
                _agent.updateRotation = false;

            _self.rotation = Quaternion.RotateTowards(
                _self.rotation,
                Quaternion.LookRotation(dir.normalized),
                degreesPerSecond * Time.deltaTime);
        }

        // ==================== 位移类动作 ====================

        /// <summary>
        /// 沿自身「后方」退一段距离（受击击退 / 攻击后拉开距离用）。
        /// ⚠️ 有 Agent 时必须走 <c>agent.Move</c> 而不是直接改 transform ——
        ///    直接改 transform 会**忽略碰撞**、也可能把敌人推出导航网格。
        /// </summary>
        public void Retreat(float distance)
        {
            if (distance <= 0f) return;

            _keepFacing = false;//击退不受"保持面朝目标"模式约束
            _faceTarget = null;

            Vector3 delta = -_self.forward * distance;

            if (UsesNavMesh)
            {
                _agent.Move(delta);
                return;
            }

            _self.position += delta;
        }

        // ==================== 生命周期 ====================

        /// <summary>
        /// 对象被重新激活时调用（<c>RoomController</c> 用 <c>SetActive(true)</c> 放怪）。
        /// Agent 在被重新激活后可能还没吸附到网格上，需要 <c>Warp</c> 强行吸附，
        /// 否则后续 <c>SetDestination</c> 会直接报错（NAVMESH-GUIDE §7.3）。
        /// </summary>
        public void SyncWithNavMesh()
        {
            if (_agent == null) return;
            if (_agent.isOnNavMesh) return;

            _hasDestination = false;
            if (!_agent.Warp(_self.position))
                WarnIfNoNavMesh();
        }

        /// <summary>「没烘焙 / 不在网格上」只提醒一次，避免每帧刷屏。</summary>
        private void WarnIfNoNavMesh()
        {
            if (_warnedNoNavMesh) return;
            _warnedNoNavMesh = true;

            if (_agent == null)
                Debug.Log("[EnemyLocomotion] 同物体上没有 NavMeshAgent —— 暂时走直线位移（M3 之前的旧行为）。" +
                          "烘焙 NavMesh 并挂上 Agent 后会自动切换，无需改代码。", _self);
            else
                Debug.LogWarning("[EnemyLocomotion] NavMeshAgent 不在导航网格上（位置在网格外，或还没 Bake）——" +
                                 "暂时走直线位移。请先按 Docs/NAVMESH-GUIDE.md §5 烘焙 NavMesh。", _self);
        }
    }
}
