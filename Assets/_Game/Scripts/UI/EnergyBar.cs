using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 体力条 HUD。订阅 <see cref="PlayerEnergy.OnEnergyChanged"/> 更新填充，
    /// 并在体力不足时**闪烁报警**（GDD §4.8 要求"动作不触发 + 明确提示"）。
    /// </summary>
    public class EnergyBar : MonoBehaviour
    {
        [SerializeField] private PlayerEnergy _player;
        [SerializeField] private RectTransform _fill;

        private Image _fillImage;
        private Color _baseColor;
        private Coroutine _flashCo;

        private void Awake()
        {
            if (_fill != null)
            {
                _fillImage = _fill.GetComponent<Image>();
                if (_fillImage != null) _baseColor = _fillImage.color;
            }
        }

        private void OnEnable()
        {
            if (_player == null) return;
            _player.OnEnergyChanged += OnEnergyChanged;
            _player.OnSpendFailed += Flash;
        }

        private void OnDisable()
        {
            if (_player == null) return;
            _player.OnEnergyChanged -= OnEnergyChanged;
            _player.OnSpendFailed -= Flash;
        }

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

        /// <summary>体力不足时闪烁提示。</summary>
        private void Flash()
        {
            if (_fillImage == null) return;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            Color warn = new Color(1f, 0.35f, 0.35f, _baseColor.a);

            for (int i = 0; i < 3; i++)
            {
                _fillImage.color = warn;
                //⚠️ 用 Realtime：游戏暂停（timeScale = 0）时闪烁不能卡在中间态
                yield return new WaitForSecondsRealtime(0.06f);
                _fillImage.color = _baseColor;
                yield return new WaitForSecondsRealtime(0.06f);
            }

            _fillImage.color = _baseColor;
            _flashCo = null;
        }
    }
}
