using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Fx
{
    public class DamagePopup : MonoBehaviour
    {
        private Text _label;
        private Action<DamagePopup> _onDone;

        private void Awake()
        {
            _label = GetComponent<Text>();
            _label.raycastTarget = false;//不挡鼠标
        }

        public void Show(string text,Vector3 worldPos,Action<DamagePopup> onDone)
        {
            _onDone = onDone;

            _label.text = text;
            Color c = _label.color;
            c.a = 1f;
            _label.color = c;

            //定位
            transform.position = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 0.3f);

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FloatUp());
        }

        private IEnumerator FloatUp()
        {
            float t = 0f, dur = 0.7f;
            Vector3 start = transform.position;
            Color c = _label.color;
            while(t<dur)
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