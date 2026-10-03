using System.Threading.Tasks;   // Task 住这
using UnityEngine;

public class AsyncDemo : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))   // 按 K 点火
            FireAndForget();               // async void 只能当"点火入口"用
    }

    private async void FireAndForget()     // 注意：async void
    {
        try                                // async void 的异常没人能接，自己全包住
        {
            Debug.Log("[async] 开始等 1 秒（我没卡住主线程）");
            await Task.Delay(1000);        // 挂起点：1 秒后从这行之后继续
            Debug.Log("[async] 1 秒到了，我回到主线程了");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[async] 出错：" + e.Message);
        }
    }
}