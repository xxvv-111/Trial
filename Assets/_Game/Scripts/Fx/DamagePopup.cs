using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Fx
{
    /// <summary>伤害飘字。由 <see cref="HitFxSystem"/> 通过对象池驱动，不自行 Instantiate。</summary>
    public class DamagePopup : MonoBehaviour
    {
        private Text _label;
        private Action<DamagePopup> _onDone;

        private void Awake()
        {
            _label = GetComponent<Text>();
            if (_label != null) _label.raycastTarget = false;//不挡鼠标
        }

        public void Show(string text, Vector3 worldPos, Action<DamagePopup> onDone)
        {
            if (_label == null) return;

            _onDone = onDone;

            _label.text = text;
            Color c = _label.color;
            c.a = 1f;
            _label.color = c;

            //定位（屏幕上偏一点，避免与角色重叠）
            if (Camera.main != null)
                transform.position = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 0.3f);

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FloatUp());
        }

        /// <summary>上飘并淡出，结束后归还对象池。</summary>
        private IEnumerator FloatUp()
        {
            const float dur = 0.7f;
            float t = 0f;
            Vector3 start = transform.position;
            Color c = _label.color;

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                transform.position = start + Vector3.up * (k * 60f);
                c.a = 1f - k;
                _label.color = c;
                yield return null;
            }

            _onDone?.Invoke(this);
        }

        private void OnDisable() => StopAllCoroutines();
    }
}
