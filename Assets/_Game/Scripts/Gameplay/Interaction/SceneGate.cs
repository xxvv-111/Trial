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
        [Tooltip("**默认**目标场景名（Inspector 里填的那份）。必须已加入 Build Settings（本项目 = Castle）。\n" +
                 "⚠️ 运行时若有更具体的目标（例如任务指定的地点），以运行时值为准 —— 见 SetTarget。")]
        [SerializeField] private string _sceneName = "Castle";

        [Tooltip("勾选 = 异步加载，大场景不会卡住一瞬。")]
        [SerializeField] private bool _async = true;

        private bool _loading;

        /// <summary>
        /// **运行时覆写**的目标场景名（空 = 用 Inspector 里的 <c>_sceneName</c>）。
        ///
        /// <para>⭐ 2026-10-22 新增，为任务系统留的口子：接取任务后调用
        /// <see cref="SetTarget"/>，门口这道门就会切到任务地点。</para>
        /// </summary>
        private string _runtimeTarget;

        /// <summary>当前**实际生效**的目标场景名（运行时覆写优先）。</summary>
        public string TargetScene { get { return string.IsNullOrEmpty(_runtimeTarget) ? _sceneName : _runtimeTarget; } }

        /// <summary>是否已被运行时改过目标（UI 可以据此决定显示"默认目的地"还是"任务目的地"）。</summary>
        public bool HasRuntimeTarget { get { return !string.IsNullOrEmpty(_runtimeTarget); } }

        /// <summary>
        /// 改目标场景（任务系统接取任务时调用）。
        /// ⚠️ 传空串 / null = 清除覆写，退回 Inspector 里的默认目标。
        /// ⚠️ 不校验场景是否存在 —— 调用方自己保证它是 <c>SceneCatalog</c> 里登记过的场景名。
        /// </summary>
        public void SetTarget(string sceneName)
        {
            _runtimeTarget = sceneName;
        }

        /// <summary>清除运行时覆写，退回默认目标。</summary>
        public void ClearTarget()
        {
            _runtimeTarget = null;
        }

        /// <summary>切换场景。挂到 <c>Interactable.onInteract</c> 上。</summary>
        public void Go()
        {
            if (_loading) return;//防连按：异步加载期间可能又被触发一次

            //⭐ 运行时覆写优先（任务指定的地点），否则用 Inspector 的默认目标
            string target = TargetScene;

            if (string.IsNullOrEmpty(target))
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
                SceneManager.LoadSceneAsync(target);
            }
            else
            {
                SceneManager.LoadScene(target);
            }
        }
    }
}
