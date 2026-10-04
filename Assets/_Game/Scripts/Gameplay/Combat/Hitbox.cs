using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>判定体所属阵营。用于区分表现（特效颜色/音效），过滤主要靠 <see cref="Hitbox.TargetLayers"/>。</summary>
    public enum HitboxTeam
    {
        Player,
        Enemy
    }

    /// <summary>
    /// 攻击判定体（GDD §8）：在动画关键帧期间**真实存在**的 Trigger 碰撞体。
    ///
    /// 用法：挂在角色子物体上 → 由 <see cref="HitboxController"/> 统一开关，不自行计时。
    ///
    /// ⚠️ **必须自带 kinematic Rigidbody**（用 RequireComponent 自动补上）：
    ///    Unity 的 `OnTriggerEnter` 要求**至少一方有 Rigidbody**，而**敌人身上没有 Rigidbody**，
    ///    所以判定体必须自己带一个，否则**它开了也永远不触发**（很难查的现象）。
    ///    kinematic 不参与物理模拟，因此**不影响角色移动**。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Hitbox : MonoBehaviour
    {
        [Header("判定参数")]
        [SerializeField] private HitboxTeam _team = HitboxTeam.Player;
        [SerializeField] private int _damage = 10;

        [Tooltip("可命中的层。玩家判定体填 Enemy；敌人判定体填 Player。")]
        [SerializeField] private LayerMask _targetLayers;

        [Header("调试")]
        [SerializeField] private Color _gizmoColor = new Color(1f, 0.3f, 0.3f, 0.45f);

        private Collider _col;
        private HitboxController _controller;

        /// <summary>本次开启期间已命中的目标。防同一次挥砍重复扣血（GDD §8）。</summary>
        private readonly HashSet<Collider> _hitThisSwing = new HashSet<Collider>();

        public HitboxTeam Team { get { return _team; } }
        public int Damage { get { return _damage; } }
        public LayerMask TargetLayers { get { return _targetLayers; } }
        public Color GizmoColor { get { return _gizmoColor; } }
        public bool IsActive { get { return _col != null && _col.enabled; } }

        private void Awake()
        {
            _col = GetComponent<Collider>();
            if (_col == null)
            {
                Debug.LogError("[Hitbox] 缺少 Collider，判定体无法工作：" + name, this);
                enabled = false;
                return;
            }
            _col.isTrigger = true;

            // ⚠️ 关键：自带 kinematic Rigidbody（详见类注释）
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _controller = GetComponentInParent<HitboxController>();

            _col.enabled = false;//默认关闭，等关键帧开启
        }

        /// <summary>
        /// 开启判定体。**每次开启都会清空已命中记录** ——
        /// 因为"同一目标只吃一次"的作用域是**一次挥砍**，不是整个角色生命周期。
        /// </summary>
        public void Activate()
        {
            if (_col == null) return;
            _hitThisSwing.Clear();
            _col.enabled = true;
        }

        /// <summary>关闭判定体。</summary>
        public void Deactivate()
        {
            if (_col == null) return;
            _col.enabled = false;
        }

        /// <summary>设置本次伤害（换武器 / 连段切换时由 PlayerAttack 同步）。</summary>
        public void SetDamage(int damage)
        {
            _damage = damage;
        }

        /// <summary>
        /// 由**武器配置**驱动判定盒的尺寸与位置（M2.1）。
        ///
        /// 这样"每把武器的每段判定范围"就是一个配置值，
        /// 不必为每把武器在预制体上各摆一套判定体。
        ///
        /// <paramref name="localPos"/> 一般传 <c>(0, 中心高度, 长度/2)</c> ——
        /// 让判定盒覆盖「身前 0 ~ 长度」这一段（与 M1.2 的手工摆放一致）。
        /// </summary>
        public void ApplyShape(Vector3 size, Vector3 localPos)
        {
            if (_col == null) _col = GetComponent<Collider>();

            BoxCollider box = _col as BoxCollider;
            if (box == null)
            {
                Debug.LogWarning("[Hitbox] 仅 BoxCollider 支持 ApplyShape：" + name, this);
                return;
            }

            box.size = size;
            transform.localPosition = localPos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_col == null || !_col.enabled) return;

            //层过滤（碰撞矩阵已保证，这里再挡一层，防止数组误配时静默出错）
            if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

            //同一次挥砍对同一目标只结算一次
            if (_hitThisSwing.Contains(other)) return;

            if (_controller == null) _controller = GetComponentInParent<HitboxController>();
            if (_controller == null)
            {
                Debug.LogWarning("[Hitbox] 找不到所属 HitboxController：" + name, this);
                return;
            }

            if (_controller.TryProcessHit(this, other))
                _hitThisSwing.Add(other);
        }

        private void OnDrawGizmos()
        {
            if (_col == null) _col = GetComponent<Collider>();
            if (_col == null) return;

            if (!Application.isPlaying)
            {
                //编辑期常显（半透明），便于摆位置
                DrawGizmo(new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.12f));
                return;
            }

            //运行期：只有开了调试开关、且判定体确实激活时才画
            if (!HitboxController.ShowGizmos) return;
            if (!_col.enabled) return;
            DrawGizmo(_gizmoColor);
        }

        private void DrawGizmo(Color c)
        {
            Gizmos.color = c;

            BoxCollider box = _col as BoxCollider;
            if (box != null)
            {
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = old;
                return;
            }

            SphereCollider sph = _col as SphereCollider;
            if (sph != null)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(sph.center), sph.radius);
                return;
            }

            CapsuleCollider cap = _col as CapsuleCollider;
            if (cap != null)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(cap.center), cap.radius);
                return;
            }

            Gizmos.DrawWireCube(_col.bounds.center, _col.bounds.size);
        }
    }
}
