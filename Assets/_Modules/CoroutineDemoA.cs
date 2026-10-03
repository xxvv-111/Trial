using System.Collections;   // 协程要用 IEnumerator，它住在这个命名空间
using UnityEngine;

public class CoroutineDemoA : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(CountDown());   // 发车：让协程跑起来
    }

    // IEnumerator = 协程的"行程单"类型（不是 void！）
    private IEnumerator CountDown()
    {
        Debug.Log("第 0 秒：发车");
        yield return new WaitForSeconds(1f);   // 停靠站：等真实 1 秒
        Debug.Log("第 1 秒：1");
        yield return new WaitForSeconds(1f);   // 再等 1 秒
        Debug.Log("第 2 秒：2");
        yield return new WaitForSeconds(1f);
        Debug.Log("第 3 秒：3，到站");
    }
}
