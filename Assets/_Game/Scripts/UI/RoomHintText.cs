using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>房间提示文字：显示 msg，若干秒后自动清空。</summary>
    public class RoomHintText : MonoBehaviour
    {
        [SerializeField] private float showSeconds = 3f;

        private TextMeshProUGUI _text;
        private Coroutine _co;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            if (_text != null) _text.text = "";
        }

        /// <summary>显示提示。重复调用会自动覆盖上一条（不叠加计时）。</summary>
        public void Show(string msg)
        {
            if (_text == null) return;

            _text.text = msg;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(HideAfter(showSeconds));
        }

        private IEnumerator HideAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            if (_text != null) _text.text = "";
            _co = null;
        }
    }
}
