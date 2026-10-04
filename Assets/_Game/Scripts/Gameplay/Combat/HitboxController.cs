using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 判定体总管（每个角色一个，GDD §8）。
    ///
    /// 职责：
    ///   1. **注册**本角色所有 <see cref="Hitbox"/>
    ///   2. **按名开关**（给动画关键帧 / StateMachineBehaviour 调用）
    ///   3. **统一处理命中**：过滤 → 扣血 → 广播特效事件
    ///
    /// ⚠️ 所有命中都要经过 <see cref="TryProcessHit"/> 这一个出口 ——
    ///    这样"伤害表现、飘字、特效"天然统一，不会出现某招式漏播特效的情况。
    /// </summary>
    public class HitboxController : MonoBehaviour
    {
        /// <summary>命中事件 (命中点, 伤害)。特效系统订阅它（见 AttackFxBridge）。</summary>
        public event Action<Vector3, int> OnHit;

        /// <summary>运行期调试：按 F1 显示所有激活判定体的 Gizmos。全局共享。</summary>
        public static bool ShowGizmos = false;

        [Header("判定体")]
        [Tooltip("留空则 Awake 时自动从子物体收集。")]
        [SerializeField] private Hitbox[] _hitboxes;

        [Header("调试")]
        [Tooltip("允许运行时按 F1 切换判定体 Gizmos 显示。")]
        [SerializeField] private bool _f1ToggleGizmos = true;

        /// <summary>本角色已注册的判定体数量。</summary>
        public int HitboxCount { get { return _hitboxes == null ? 0 : _hitboxes.Length; } }

        /// <summary>待"定时自动关闭"的判定体。</summary>
        private struct PendingClose
        {
            public Hitbox Box;
            public float Until;
        }
        private readonly List<PendingClose> _pendingClose = new List<PendingClose>();

        private void Awake()
        {
            if (_hitboxes == null || _hitboxes.Length == 0)
                _hitboxes = GetComponentsInChildren<Hitbox>(true);
        }

        private void Update()
        {
            if (_f1ToggleGizmos && Input.GetKeyDown(KeyCode.F1))
            {
                ShowGizmos = !ShowGizmos;
                Debug.Log("[Hitbox] 判定体可视化: " + (ShowGizmos ? "开" : "关"));
            }

            //定时关闭：动画上只有一个"命中帧"事件，所以用时长收敛判定窗口
            if (_pendingClose.Count > 0)
            {
                float now = Time.time;
                for (int i = _pendingClose.Count - 1; i >= 0; i--)
                {
                    if (now < _pendingClose[i].Until) continue;
                    if (_pendingClose[i].Box != null) _pendingClose[i].Box.Deactivate();
                    _pendingClose.RemoveAt(i);
                }
            }
        }

        // ==================== 开关（供动画关键帧调用） ====================

        /// <summary>按名开启判定体。名字 = 判定体所在 GameObject 的名字。</summary>
        public void EnableHitbox(string hitboxName)
        {
            EnableHitbox(hitboxName, -1f);
        }

        /// <summary>
        /// 按名开启判定体，并在 <paramref name="duration"/> 秒后**自动关闭**。
        /// <paramref name="duration"/> ≤ 0 表示不自动关（由调用方负责关）。
        ///
        /// ⚠️ 同一判定体重复开启时，会**先清掉它之前的定时条目** ——
        ///    否则连段快速衔接时，前一段的旧计时会把新开的判定体提前关掉。
        /// </summary>
        public void EnableHitbox(string hitboxName, float duration)
        {
            Hitbox h = Find(hitboxName);
            if (h == null)
            {
                Debug.LogWarning("[HitboxController] 找不到判定体: " + hitboxName, this);
                return;
            }

            h.Activate();

            if (duration <= 0f) return;

            for (int i = _pendingClose.Count - 1; i >= 0; i--)
                if (_pendingClose[i].Box == h) _pendingClose.RemoveAt(i);

            PendingClose pc;
            pc.Box = h;
            pc.Until = Time.time + duration;
            _pendingClose.Add(pc);
        }

        /// <summary>按名关闭判定体。</summary>
        public void DisableHitbox(string hitboxName)
        {
            Hitbox h = Find(hitboxName);
            if (h != null) h.Deactivate();
        }

        /// <summary>关闭所有判定体（收招、受击中断、死亡时调用，避免残留判定）。</summary>
        public void DisableAllHitboxes()
        {
            _pendingClose.Clear();
            if (_hitboxes == null) return;
            for (int i = 0; i < _hitboxes.Length; i++)
                if (_hitboxes[i] != null) _hitboxes[i].Deactivate();
        }

        private Hitbox Find(string hitboxName)
        {
            if (_hitboxes == null || string.IsNullOrEmpty(hitboxName)) return null;
            for (int i = 0; i < _hitboxes.Length; i++)
                if (_hitboxes[i] != null && _hitboxes[i].name == hitboxName) return _hitboxes[i];
            return null;
        }

        // ==================== 命中收口（唯一出口） ====================

        /// <summary>
        /// 处理一次命中。返回 true 表示"已结算"（调用方据此记为已命中）。
        /// 返回 false 的情况：目标不可受击 / 打到自己 / 目标已死亡。
        /// </summary>
        public bool TryProcessHit(Hitbox box, Collider other)
        {
            if (box == null || other == null) return false;

            //不能打到自己（判定体是角色子物体，理论上矩阵已排除，这里再挡一层）
            if (other.transform.IsChildOf(transform)) return false;

            //找可受击目标（可能挂在父物体上，例如模型分件）
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null) return false;

            target.TakeDamage(box.Damage);

            Vector3 point = other.ClosestPoint(transform.position);
            if (OnHit != null) OnHit(point, box.Damage);
            return true;
        }
    }
}
