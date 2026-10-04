using UnityEngine;
using Game.Fx;

namespace Game.Gameplay
{
    /// <summary>
    /// 把 <see cref="HitboxController.OnHit"/> 桥接到打击特效系统（火花 + 伤害飘字）。
    ///
    /// ⚠️ 订阅源在 M1.2 从 `PlayerAttack.OnHit` 改成了 `HitboxController.OnHit` ——
    ///    因为伤害结算已统一到判定体系统，`PlayerAttack` 不再自己发命中事件。
    /// 这样**所有**来源的命中（普攻/特殊攻击/长枪投掷）都会走同一条特效通道。
    /// </summary>
    public class AttackFxBridge : MonoBehaviour
    {
        [SerializeField] private HitFxSystem fx;
        private HitboxController _hitboxes;

        private void OnEnable()
        {
            _hitboxes = GetComponent<HitboxController>();
            if (_hitboxes != null && fx != null) _hitboxes.OnHit += fx.Play;

            if (_hitboxes != null && fx == null)
                Debug.LogWarning("[AttackFxBridge] fx 未指定，命中特效不会播放。", this);
        }

        private void OnDisable()
        {
            if (_hitboxes != null && fx != null) _hitboxes.OnHit -= fx.Play;
        }
    }
}
