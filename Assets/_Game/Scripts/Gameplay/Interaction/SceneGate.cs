using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 场景门：被交互后切到目标场景。挂在门口，与 <see cref="Interactable"/> 放**同一个物体**。
    ///
    /// ⚠️ **为什么需要这个类**：<c>UnityEvent</c> 只能挂**实例方法**，
    ///    而 <c>SceneManager.LoadScene</c> 是**静态方法** —— 拖不进 Inspector 的方法列表。
    ///    所以用一个一行的组件把"切场景"包成实例方法。
    ///    （替代方案是给 Interactable 加一个"动作类型"枚举，但那样会让通用组件越来越臃肿。）
    ///
    /// 用法：把 <c>Interactable.onInteract</c> 拖到本组件的 <see cref="Go"/>。
    /// </summary>
    public class SceneGate : MonoBehaviour
    {
        [Tooltip("目标场景名。必须已加入 Build Settings（本项目 = Castle）。")]
        [SerializeField] private string _sceneName = "Castle";

        [Tooltip("勾选 = 异步加载，大场景不会卡住一瞬。")]
        [SerializeField] private bool _async = true;

        private bool _loading;

        /// <summary>切换场景。挂到 <c>Interactable.onInteract</c> 上。</summary>
        public void Go()
        {
            if (_loading) return;//防连按：异步加载期间可能又被触发一次

            if (string.IsNullOrEmpty(_sceneName))
            {
                Debug.LogError("[SceneGate] 目标场景名为空，无法切换。");
                return;
            }

            _loading = true;

            //⚠️⚠️ 切场景前**必须**把时间和输入还原：
            //   若此刻任务面板还开着（Time.timeScale = 0），新场景会直接继承暂停态，
            //   表现为"进了城堡但谁都不动"。
            //   Castle 的 GameManager.Start() 也会兜底重置，但不要依赖别人擦屁股。
            Time.timeScale = 1f;
            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(true);

            if (_async)
            {
                // TODO（留档）：现在的异步"秒切"，没有过渡动画。
                //   要做出场淡入淡出，可以在这里先播一个全屏黑幕的 CanvasGroup 淡入，
                //   等 op.isDone 后再淡出（用协程 + WaitForSecondsRealtime，
                //   ⚠️ 注意用 Realtime 版本，因为切场景前 timeScale 可能刚被改）。
                SceneManager.LoadSceneAsync(_sceneName);
            }
            else
            {
                SceneManager.LoadScene(_sceneName);
            }
        }
    }
}
