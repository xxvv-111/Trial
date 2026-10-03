using UnityEngine;
using Game.Fx;

namespace Game.Gameplay
{
    /// <summary>把 PlayerAttack.OnHit 事件桥接到打击特效系统（火花 + 伤害飘字）。</summary>
    public class AttackFxBridge : MonoBehaviour
    {
        [SerializeField] private HitFxSystem fx;
        private PlayerAttack _attack;

        private void OnEnable()
        {
            _attack = GetComponent<PlayerAttack>();
            if (_attack != null && fx != null) _attack.OnHit += fx.Play;
        }

        private void OnDisable()
        {
            if (_attack != null && fx != null) _attack.OnHit -= fx.Play;
        }
    }
}
