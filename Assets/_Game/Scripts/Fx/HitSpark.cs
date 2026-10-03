using System;
using System.Collections;
using UnityEngine;

namespace Game.Fx
{
    public class HitSpark : MonoBehaviour
    {
        private Renderer _r;//着色器
        private Material _mat;//材质
        private Action<HitSpark> _onDone;//释放

        private void Awake()
        {
            _r = GetComponent<Renderer>();
            _mat = _r.material;
        }

        public void Show(Vector3 pos,Action<HitSpark> onDone)//展示过程
        {
            _onDone = onDone;
            transform.position = pos;
            transform.rotation = Camera.main.transform.rotation;
            Color c = _mat.color;
            c.a = 1f;
            _mat.color = c;
            transform.localScale = Vector3.one * 0.4f;

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FlashAnim());
        }

        private IEnumerator FlashAnim()//展示用协程
        {
            float t = 0f, dur = 0.18f;
            Color c = _mat.color;
            while(t<dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                transform.localScale=Vector3.one*Mathf.Lerp(0.4f,1.2f,k);
                c.a = 1f - k;
                _mat.color = c;
                yield return null;
            }
            _onDone?.Invoke(this);
        }

        private void OnDisable() => StopAllCoroutines();
    }
}