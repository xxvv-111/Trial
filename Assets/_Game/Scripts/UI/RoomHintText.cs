using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class RoomHintText : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private Coroutine _co;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _text.text = "";
        }

        public void Show(string msg)
        {
            _text.text = msg;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(HideAfter(3f));
        }

        private IEnumerator HideAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            _text.text = "";
        }
    }
}