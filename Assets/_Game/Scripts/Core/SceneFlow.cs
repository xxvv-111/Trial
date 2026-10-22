using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 场景切换事件转发器：把 <c>SceneManager.sceneLoaded</c> 转成
    /// <see cref="GameEvents.SceneEntered"/>，供任务系统（"到达场景"目标）等订阅。
    ///
    /// <para>**为什么需要这个类**：<c>SceneManager.sceneLoaded</c> 是**实例无关的静态事件**，
    /// 但订阅它的对象必须活到下一次加载。摆在场景里的订阅者会在切场景时被销毁 ——
    /// 于是"最后一次加载"永远收不到。所以订阅方必须常驻（<c>DontDestroyOnLoad</c>）。</para>
    ///
    /// <para>用 <c>BeforeSceneLoad</c> 自动创建，理由和 <see cref="Save.ProgressService"/> 一样：
    /// 开发时经常直接打开 Castle / Tavern 按 Play，不会经过 MainMenu，摆场景里就会漏。
    /// 每局只创建一个（第二次启动会自毁）。</para>
    ///
    /// ⚠️ 首个场景的 `sceneLoaded` 可能在订阅之前就已经触发过（时序由引擎决定），
    /// 所以 <c>Start</c> 里补发一次；用一个标志位避免重复发。
    /// 任务的"到达场景"目标本身是幂等的（progress 直接置满），即使重复也不会出错。
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        static SceneFlow _instance;

        bool _raisedAny;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[SceneFlow]");
            DontDestroyOnLoad(go);
            go.AddComponent<SceneFlow>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);   // 场景里被误摆了一份：保留先来的
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            // 兜底：首个场景的加载事件若在订阅之前已经发生，这里补一次
            if (_raisedAny) return;
            Raise(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _instance = null;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Raise(scene);

        void Raise(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name)) return;

            _raisedAny = true;
            Debug.Log($"[SceneEntered] {scene.name}");   // 验收用：进场景应看到这一行
            GameEvents.RaiseSceneEntered(scene.name);
        }
    }
}
