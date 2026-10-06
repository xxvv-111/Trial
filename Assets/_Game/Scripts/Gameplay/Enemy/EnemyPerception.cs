using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 敌人视野感知（M3.3）。实现 GDD §6.1 的「感知 = 距离 + 视野判定」：
    /// <list type="number">
    ///   <item>距离是否在 <see cref="ViewDistance"/> 内</item>
    ///   <item>玩家是否落在视野扇形内（<see cref="Vector3.Angle"/> 对 <see cref="ViewAngle"/> 的一半）</item>
    ///   <item><see cref="Physics.Raycast"/> 是否被墙 / 障碍物挡住（视线遮挡）</item>
    /// </list>
    ///
    /// ⚠️ **职责边界（NAVMESH-GUIDE §10）**：
    /// <list type="bullet">
    ///   <item>本组件只答「**我有没有看见玩家**」</item>
    ///   <item>寻路（<see cref="EnemyLocomotion"/>）答「我该怎么走过去」</item>
    ///   <item>状态机答「我现在该做什么」</item>
    /// </list>
    /// NavMesh 完全不提供本组件的能力，别指望烘焙来解决「看不见」的问题。
    ///
    /// 性能：结果**按帧缓存**，同一帧内 Get 多少次都只算一次（多个状态 / 多处调用不会重复射线）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyPerception : MonoBehaviour
    {
        [Header("视野（GDD §6.1）")]
        [Tooltip("视野距离（米）。")]
        [SerializeField] private float _viewDistance = 12f;

        [Tooltip("视野扇形的**全角**（度）。360 = 退化成纯距离感知（与接入感知之前的行为一致）。")]
        [SerializeField] private float _viewAngle = 160f;

        [Tooltip("是否要求视线无遮挡：被墙 / 柱子挡住 → 看不见。")]
        [SerializeField] private bool _requireLineOfSight = true;

        [Header("补充判据")]
        [Tooltip("贴脸感知半径（米）：玩家进到这个距离内，**即使在他背后也会发现**（GDD §6.3「玩家贴脸 → 优先脱离」）。0 = 关闭。")]
        [SerializeField] private float _proximityRange = 2f;

        [Header("射线参数")]
        [Tooltip("眼睛高度（米）：视线射线的起点抬升量。")]
        [SerializeField] private float _eyeHeight = 1.2f;

        [Tooltip("瞄准点高度（米）：视线射线的终点在玩家身上抬升多少（对着躯干而不是脚底打）。")]
        [SerializeField] private float _aimHeight = 1.0f;

        [Tooltip("哪些层算「遮挡物」。本次计算时会自动排除自己和目标所在的层。")]
        [SerializeField] private LayerMask _occluderMask = ~0;

        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

        // ==================== 运行时状态 ====================

        private Transform _target;

        private int _cachedFrame = -1;//本帧是否已算过（同一帧只算一次）
        private bool _canSee;
        private bool _inCone;
        private bool _hasLos;
        private float _distance = float.MaxValue;

        private bool _wasSeeing;//上一帧是否看得见（判断 Spotted / Lost 的边沿）
        private Vector3 _lastKnownPosition;
        private float _lastSeenTime = -9999f;

        /// <summary>刚看见目标时触发一次（从「看不见」跨到「看得见」的那一帧）。</summary>
        public event Action<Transform> Spotted;

        /// <summary>刚丢失目标时触发一次（从「看得见」跨到「看不见」的那一帧）。</summary>
        public event Action Lost;

        // ==================== 对外接口 ====================

        /// <summary>被感知的目标（通常是玩家）。换了目标会自动让本帧缓存失效。</summary>
        public Transform Target
        {
            get { return _target; }
            set
            {
                if (_target == value) return;
                _target = value;
                _cachedFrame = -1;
                _wasSeeing = false;
                _canSee = _inCone = _hasLos = false;
                _distance = float.MaxValue;
            }
        }

        public bool HasTarget { get { return _target != null; } }

        /// <summary>本帧是否看见目标 = 距离达标 **且** 在扇形内 **且** 视线无遮挡。</summary>
        public bool CanSeeTarget { get { EnsureFresh(); return _canSee; } }

        /// <summary>本帧距离是否在视野距离内（不含扇形与遮挡判断，调试 / 状态机算距离区间时用）。</summary>
        public bool InRange { get { EnsureFresh(); return _distance <= _viewDistance; } }

        /// <summary>本帧是否落在视野扇形内（含贴脸豁免）。</summary>
        public bool InViewCone { get { EnsureFresh(); return _inCone; } }

        /// <summary>本帧视线是否无遮挡。</summary>
        public bool HasLineOfSight { get { EnsureFresh(); return _hasLos; } }

        /// <summary>到目标的水平距离（米）；没目标时返回 <see cref="float.MaxValue"/>。</summary>
        public float Distance { get { EnsureFresh(); return _distance; } }

        /// <summary>最后一次看见目标的位置（丢了目标也能去这里搜）。</summary>
        public Vector3 LastKnownPosition { get { return _lastKnownPosition; } }

        /// <summary>距最后一次看见目标过了多少秒。</summary>
        public float TimeSinceLastSeen { get { return Time.time - _lastSeenTime; } }

        public float ViewDistance { get { return _viewDistance; } }
        public float ViewAngle { get { return _viewAngle; } }
        public float ProximityRange { get { return _proximityRange; } }
        public bool RequireLineOfSight { get { return _requireLineOfSight; } }

        /// <summary>视线射线的起点（眼睛位置）。</summary>
        public Vector3 EyePosition { get { return transform.position + Vector3.up * _eyeHeight; } }

        /// <summary>由敌人 AI 从 <c>EnemyAIConfig</c> 统一下发参数，
        /// 这样**调参只在一处**（配置资产），不必逐个预制体改组件。</summary>
        public void Configure(float viewDistance, float viewAngle, bool requireLineOfSight,
                              float proximityRange, float eyeHeight, float aimHeight)
        {
            _viewDistance = Mathf.Max(0.1f, viewDistance);
            _viewAngle = Mathf.Clamp(viewAngle, 1f, 360f);
            _requireLineOfSight = requireLineOfSight;
            _proximityRange = Mathf.Max(0f, proximityRange);
            _eyeHeight = eyeHeight;
            _aimHeight = aimHeight;
            _cachedFrame = -1;
        }

        /// <summary>运行时改视野距离（弓兵的射击距离等场景用）。</summary>
        public void SetViewDistance(float value)
        {
            _viewDistance = Mathf.Max(0.1f, value);
            _cachedFrame = -1;
        }

        // ==================== 计算 ====================

        //同一帧只算一次：多个调用点（状态机 / 调试 Gizmos / 外部查询）不会重复发射线
        private void EnsureFresh()
        {
            if (_cachedFrame == Time.frameCount) return;
            _cachedFrame = Time.frameCount;
            Evaluate();
        }

        private void Evaluate()
        {
            _canSee = false;
            _inCone = false;
            _hasLos = false;
            _distance = float.MaxValue;

            if (_target == null)
            {
                UpdateEdge(false);
                return;
            }

            Vector3 flat = _target.position - transform.position;
            flat.y = 0f;
            _distance = Vector3.Distance(transform.position, _target.position);

            //① 距离
            bool inRange = _distance <= _viewDistance;

            //② 扇形（360° 或目标正好在脚下时直接算通过）
            if (_viewAngle >= 360f || flat.sqrMagnitude <= 0.0001f)
                _inCone = true;
            else
                _inCone = Vector3.Angle(transform.forward, flat) <= _viewAngle * 0.5f;

            //贴脸豁免：背后贴上来也算发现（只豁免扇形，不豁免遮挡）
            if (_proximityRange > 0f && _distance <= _proximityRange)
                _inCone = true;

            //③ 视线遮挡
            Vector3 eye = EyePosition;
            Vector3 aim = _target.position + Vector3.up * _aimHeight;
            _hasLos = !_requireLineOfSight || CheckLineOfSight(eye, aim);

            _canSee = inRange && _inCone && _hasLos;

            if (_canSee)
            {
                _lastKnownPosition = _target.position;
                _lastSeenTime = Time.time;
            }

            UpdateEdge(_canSee);
        }

        private void UpdateEdge(bool seeing)
        {
            if (seeing == _wasSeeing) return;

            _wasSeeing = seeing;
            if (seeing)
            {
                if (Spotted != null) Spotted(_target);
            }
            else
            {
                if (Lost != null) Lost();
            }
        }

        /// <summary>从眼睛到目标躯干打一条射线，中间撞到东西就算被遮挡。</summary>
        private bool CheckLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist <= 0.01f) return true;

            return !Physics.Raycast(from, delta / dist, dist, EffectiveMask, _triggerInteraction);
        }

        /// <summary>实际参与遮挡判定的层：
        /// ⚠️ 必须排除**自己**（射线起点常在自身碰撞体内部）与**目标**（终点就在目标身上），
        /// 否则每次都会打到自己 / 打到玩家 → 永远判成「被遮挡」。</summary>
        private int EffectiveMask
        {
            get
            {
                int mask = _occluderMask.value;
                mask &= ~(1 << gameObject.layer);
                if (_target != null) mask &= ~(1 << _target.gameObject.layer);
                return mask;
            }
        }

        // ==================== Scene 视图可视化 ====================

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = EyePosition;

            //视野距离
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, _viewDistance);

            //扇形两条边
            if (_viewAngle < 360f)
            {
                float half = _viewAngle * 0.5f;
                Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
                Gizmos.DrawRay(eye, Quaternion.Euler(0f, -half, 0f) * transform.forward * _viewDistance);
                Gizmos.DrawRay(eye, Quaternion.Euler(0f, half, 0f) * transform.forward * _viewDistance);
            }

            //贴脸感知圈
            if (_proximityRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.78f, 0.2f, 1f);
                Gizmos.DrawWireSphere(transform.position, _proximityRange);
            }

            if (_target == null) return;

            //到目标的视线：只在运行时按「看得见 / 看不见」上色；编辑模式下缓存是空的，统一画黄色
            Gizmos.color = !Application.isPlaying
                ? new Color(1f, 1f, 0.3f, 1f)
                : (_canSee ? Color.green : Color.red);
            Gizmos.DrawLine(eye, _target.position + Vector3.up * _aimHeight);

            if (_hasLos) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_target.position + Vector3.up * _aimHeight, Vector3.one * 0.3f);
        }
    }
}
