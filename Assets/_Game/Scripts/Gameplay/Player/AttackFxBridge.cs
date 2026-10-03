using UnityEngine;
using Game.Fx;

namespace Game.Gameplay
{
    public class AttackFxBridge : MonoBehaviour
    {
        [SerializeField] private HitFxSystem fx;
        private PlayerAttack _attack;

        private void OnEnable()
        {
            _attack = GetComponent<PlayerAttack>();
            if (_attack != null) _attack.OnHit += fx.Play;
        }

        private void OnDisable()
        {
            if (_attack != null) _attack.OnHit -= fx.Play;
        }
    }
}