using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// **魔力条** HUD（2026-10-06 由「体力条」改回「魔力条」，类名 <c>EnergyBar</c> → <c>ManaBar</c>）。
    /// 订阅 <see cref="PlayerMana.OnManaChanged"/> 更新填充，
    /// 并在魔力不足时**闪烁报警**（要求"动作不触发 + 明确提示"）。
    ///
    /// ⚠️ 脚本资产本身**没有换 GUID**（用 git mv 同时移动 .cs 与 .meta），
    ///    所以场景里 <c>ManaBarBack</c> 上的组件引用**不会断**。
    /// </summary>
    public class ManaBar : MonoBehaviour
    {
        [SerializeField] private PlayerMana _player;
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
            _player.OnManaChanged += OnManaChanged;
            _player.OnManaSpendFailed += Flash;
        }

        private void OnDisable()
        {
            if (_player == null) return;
            _player.OnManaChanged -= OnManaChanged;
            _player.OnManaSpendFailed -= Flash;
        }

        private void OnManaChanged(float cur, float max)
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

        /// <summary>魔力不足时闪烁提示。</summary>
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
