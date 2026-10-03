using UnityEngine;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 体力条 HUD。订阅 <see cref="PlayerEnergy.OnEnergyChanged"/>，用锚点拉伸填充块。
    /// （原类名 ManaBar / 方法 OnMana 沿用了旧"蓝条"叫法，这里统一为体力语义。）
    /// </summary>
    public class EnergyBar : MonoBehaviour
    {
        [SerializeField] private PlayerEnergy _player;
        [SerializeField] private RectTransform _fill;

        private void OnEnable() { if (_player != null) _player.OnEnergyChanged += OnEnergyChanged; }
        private void OnDisable() { if (_player != null) _player.OnEnergyChanged -= OnEnergyChanged; }

        private void OnEnergyChanged(float cur, float max)
        {
            SetFill(max <= 0f ? 0f : cur / max);
        }

        /// <summary>fraction ∈ [0,1]，用锚点拉伸填充块。</summary>
        private void SetFill(float fraction)
        {
            if (_fill == null) return;

            fraction = Mathf.Clamp01(fraction);
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(fraction, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }
    }
}
