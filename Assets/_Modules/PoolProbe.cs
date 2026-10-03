using System.Collections;
using System;
using UnityEngine;

namespace Modules.PoolLab
{
    public class PoolProbe : MonoBehaviour
    {
        public static int AwakeCount = 0;
        public Action<PoolProbe> OnDone;//用完释放

        private void Awake()
        {
            AwakeCount++;
            Debug.Log($"[PoolProbe]真的创建第{AwakeCount}次 ");
        }

        private void OnEnable() => StartCoroutine(AutoBack());

        private IEnumerator AutoBack()
        {
            float t = 0f;
            while (t < 1f) 
            {
                t += Time.deltaTime;
                yield return null;
            }
            OnDone?.Invoke(this);
        }

        private void OnDisable() => StopAllCoroutines();
    }
}