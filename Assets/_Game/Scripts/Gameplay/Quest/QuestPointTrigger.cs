using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 场景里的「探查点」。玩家走进它的碰撞范围就发 <see cref="GameEvents.PointReached"/>，
    /// 任务目标 <c>QuestObjectiveType.ReachPoint</c> 靠这个推进。
    ///
    /// <para>⭐ **为什么需要它**：`ReachScene` 只能表达"进了这张地图"，无法表达"走到了地图某个具体位置"。
    /// 而"探查上山通路"这种任务，必须真的走到路线末端才算完成 —— 中途被打断、绕路都不算。</para>
    ///
    /// <para><b>怎么摆</b>：在目标的末端位置建一个空物体 → 加 `SphereCollider`（自动设为 Trigger）→
    /// 挂本组件 → 把 <see cref="_pointId"/> 填成任务配置里 `ReachPoint` 目标的 `targetId`。
    /// **两处 id 必须逐字一致**，拼错的表现是"任务永远完不成"。</para>
    ///
    /// <para>⚠️ 依赖玩家在 <c>Player</c> 层（= 8）。玩家根物体是 CharacterController，走进范围会触发
    /// <c>OnTriggerEnter</c>；玩家身上的武器判定体在别的层，不会误触发。</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestPointTrigger : MonoBehaviour
    {
        [Tooltip("探查点 id。⚠️ 必须与 QuestConfig 里 ReachPoint 目标的 targetId 逐字一致。")]
        [SerializeField] private string _pointId = "point_forest_scout";

        [Tooltip("只响应这些层的碰撞体。默认只勾 Player(8) —— 避免怪物或子弹误触发。")]
        [SerializeField] private LayerMask _playerLayers = 1 << 8;

        [Tooltip("只上报一次。关掉的话玩家每次进出都会重新上报（对 needCount=1 的目标没有区别，但会刷日志）。")]
        [SerializeField] private bool _onlyOnce = true;

        [Tooltip("调试用：在 Scene 视图画出触发半径。")]
        [SerializeField] private bool _drawGizmo = true;

        private bool _raised;

        private void Reset()
        {
            // 在 Inspector 里刚挂上组件时，顺手把碰撞体设成 Trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            _raised = false;   // 场景重进 / 对象复用后可再次上报
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_onlyOnce && _raised) return;

            // 用位掩码判断层，比 CompareTag 稳：玩家的碰撞体可能不在根上
            if ((_playerLayers.value & (1 << other.gameObject.layer)) == 0) return;

            if (string.IsNullOrWhiteSpace(_pointId))
            {
                Debug.LogWarning($"[PointReached] 探查点 {name} 没填 pointId，本次不生效", this);
                return;
            }

            _raised = true;
            Debug.Log($"[PointReached] {_pointId}（{name}）");
            GameEvents.RaisePointReached(_pointId);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmo) return;

            var sphere = GetComponent<SphereCollider>();
            var radius = sphere != null ? sphere.radius * Mathf.Max(transform.lossyScale.x, 0.0001f) : 1f;
            var center = sphere != null ? transform.TransformPoint(sphere.center) : transform.position;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
