using UnityEngine;
using Game.Gameplay;

namespace Game.UI
{
    public class HPBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _player;
        [SerializeField] private RectTransform _fill;//实际血条

        private void OnEnable()
        {
            if (_player != null) _player.OnHpChanged += OnHp;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnHpChanged -= OnHp;
        }

        private void OnHp(int cur,int max)
        {
            SetFill(max <= 0 ? 0f : cur / (float)max);
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