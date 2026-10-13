using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 暂停菜单：<c>ESC</c> 开/关，暂停时冻结时间并锁住游戏性输入。
    ///
    /// ⚠️ **两道闸必须同时上**（只做一道会留下 bug）：
    ///   1. `Time.timeScale = 0` —— 让动画与逻辑停下
    ///   2. `InputService.SetGameplayInputEnabled(false)` —— 让**鼠标不能再转视角**
    ///
    /// 为什么第 2 道必需：`timeScale = 0` **拦不住输入** ——
    /// `Update()` 仍每帧执行，且 Input System 完全不受 timeScale 影响。
    /// 详见 <see cref="InputService"/> 的注释。
    ///
    /// 指针不用本类操心：关掉游戏性输入后，<see cref="Gameplay.CameraFollow"/>
    /// 会自动解锁指针（否则按钮点不动）；恢复时它会重新锁定。
    ///
    /// 面板的两个按钮接法（可用 <c>Assets/Editor/PauseMenuBuilder.cs</c> 一键生成）：
    ///   · 「Resume」    → <see cref="Resume"/>
    ///   · 「Main Menu」 → <see cref="BackToMainMenu"/>
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;

        [Tooltip("「回到主菜单」要加载的场景名。必须已在 Build Settings 里（本项目 = MainMenu）。")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        /// <summary>是否正处在暂停状态。</summary>
        public bool IsPaused { get; private set; }

        private float _prevTimeScale = 1f;//暂停前的 timeScale，恢复时还原

        private void Start()
        {
            IsPaused = false;
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            if (InputService.Instance == null) return;
            if (!InputService.Instance.PausePressedThisFrame) return;

            //⚠️ **ESC 要让给已经打开的 UIWindow 面板**（任务面板 / 交互面板 …）：
            //   UIWindow 自己也监听 ESC 来关闭面板，不让的话按一次会**同时**发生两件事
            //   —— 关掉那个面板 + 弹出暂停菜单。OpenCount 由 UIWindow 维护。
            if (UIWindow.OpenCount > 0) return;

            //已结算（死亡/通关）时不响应 —— 那由 GameManager 的结算面板负责
            if (GameManager.Instance != null && GameManager.Instance.HasEnded) return;

            Toggle();
        }

        /// <summary>开/关暂停。也可挂到 UI 按钮上。</summary>
        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        /// <summary>暂停。</summary>
        public void Pause()
        {
            if (IsPaused) return;

            IsPaused = true;
            _prevTimeScale = Time.timeScale;

            if (_panel != null) _panel.SetActive(true);

            Time.timeScale = 0f;//闸 1：冻结时间
            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(false);//闸 2：锁输入（视角不再转）
        }

        /// <summary>继续游戏。挂到「继续游戏」按钮上。</summary>
        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            if (_panel != null) _panel.SetActive(false);

            //还原暂停前的 timeScale（若异常则回落到 1）
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;

            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(true);
        }

        /// <summary>
        /// 回到主菜单。挂到「回到主菜单」按钮上。
        ///
        /// ⚠️⚠️ **顺序不能反**：必须**先**还原 `Time.timeScale` 与输入，**再** `LoadScene`。
        ///   `Time.timeScale` 是**全局**状态，**切场景不会自动重置** ——
        ///   带着 `timeScale = 0` 进主菜单，会得到"主菜单里按钮看着正常、但 Time 全冻结"的怪场景。
        ///   （输入同理：虽则 `InputService` 每场景各有一个实例、新场景自然是新的，
        ///     但这里顺手还原一次成本为零，也防住将来把它改成常驻单例。）
        ///
        /// ℹ️ 与 <see cref="Gameplay.GameManager.BackToMenu"/> 的区别：那个是给**结算面板**用的（战斗场景），
        ///   本方法是给**暂停菜单**用的 —— Tavern 这类没有 GameManager 的场景只能走这条。
        /// </summary>
        public void BackToMainMenu()
        {
            Time.timeScale = 1f;

            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(true);

            IsPaused = false;

            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}
