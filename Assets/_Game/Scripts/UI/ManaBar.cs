using UnityEngine;
using Game.Gameplay;

namespace Game.UI
{
    public class ManaBar : MonoBehaviour
    {
        [SerializeField] private PlayerEnergy _player;
        [SerializeField] private RectTransform _fill;

        private void OnEnable() { if (_player != null) _player.OnEnergyChanged += OnMana; }
        private void OnDisable() { if (_player != null) _player.OnEnergyChanged -= OnMana; }

        private void OnMana(float cur, float max)
        {
            SetFill(max <= 0 ? 0f : cur / max);
        }

        private void SetFill(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(fraction, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }
    }
}
