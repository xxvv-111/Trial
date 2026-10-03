using Game.Core;
using System.Collections;
using UnityEngine;

namespace Modules.PoolLab
{
    public class PoolLab : MonoBehaviour
    {
        [SerializeField] private PoolProbe prefab;
        private ObjectPool<PoolProbe> _pool;

        private void Start()
        {
            PoolProbe.AwakeCount = 0;
            _pool=new ObjectPool<PoolProbe>(prefab,5,transform);
            StartCoroutine(Emitting());
        }

        private IEnumerator Emitting()
        {
            for(int i=0;i<12;i++)
            {
                var p = _pool.Get();
                p.OnDone = _pool.Release;
                p.transform.position = Random.insideUnitSphere * 2f;
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(2f);
            Debug.Log($"[ProolLab]借了12次，真创捷{PoolProbe.AwakeCount}次");
        }

    }
}