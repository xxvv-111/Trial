using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>开始界面：开始游戏 / 退出（分别挂在 Button_Start、Button_Quit 的 OnClick 上）。</summary>
    public class MainMenu : MonoBehaviour
    {
        public void StartGame() => SceneManager.LoadScene("Game");

        public void QuitGame() => Application.Quit();
    }
}
