using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Save;

namespace Game.Gameplay
{
    /// <summary>
    /// 把「当前进行中任务的地点」写进同一物体上的 <see cref="SceneGate"/>。
    ///
    /// <para>━━ 为什么需要它 ━━</para>
    /// <para>
    /// <see cref="SceneGate"/> 早就有了 <c>SetTarget</c> / <c>ClearTarget</c>，缺的只是
    /// **谁在什么时候调**。本类就是那个"谁"：订阅 <c>GameEvents.QuestsChanged</c>，
    /// 每当任务状态变化（接取 / 推进 / 交付）就自己算一遍该指向哪。
    /// </para>
    /// <para>
    /// ⚠️ **刻意不放进 <c>QuestService</c>**：服务只管进度与状态机，不该知道
    /// "场景里有一扇门"这件事。反过来门也不该逼服务改规则，所以这里走事件单向依赖。
    /// </para>
    ///
    /// <para>━━ 规则 ━━</para>
    /// <list type="bullet">
    ///   <item>只看 <c>Active</c>（进行中）的任务。`ReadyToTurnIn` 要回酒馆交付，不该把门指向它；</item>
    ///   <item>多条进行中且地点不同时，取**任务表里靠前**的那条（V1 约定，简单可预期）；</item>
    ///   <item>没有进行中的任务 / 任务都不指定地点 ⇒ <c>ClearTarget()</c> + **把门锁上**，
    ///         提示条显示暗色的「暂无任务地点」（2026-10-24 定：没有任务地点时不该白送一个目的地）；</item>
    ///   <item>有任务地点 ⇒ 解锁，且提示文案跟随任务变「前往覆雪林地」。</item>
    /// </list>
    ///
    /// <para>━━ 挂法 ━━</para>
    /// <para>与 <see cref="SceneGate"/> 挂在**同一个物体**上（下标里已用 RequireComponent 保证）。
    /// 同一物体上的 <see cref="Interactable"/> 会被自动接管（改文案 / 上锁）。</para>
    /// </summary>
    [RequireComponent(typeof(SceneGate))]
    public class QuestGateBinder : MonoBehaviour
    {
        /// <summary>没有任务地点时提示条上的暗色文案。</summary>
        private const string NoTargetHint = "暂无任务地点";

        [Tooltip("场景注册表，用来把 targetScene（Forest）翻译成显示名（覆雪林地）。\n" +
                 "留空则直接显示场景文件名。")]
        [SerializeField] private SceneCatalog _catalog;

        private SceneGate _gate;

        /// <summary>同一物体上的交互组件（可能为空：门不一定用 Interactable 驱动）。</summary>
        private Interactable _interactable;

        /// <summary>当前由任务写进去的场景名。<c>null</c> = 用的是 Inspector 默认值。</summary>
        private string _applied;

        /// <summary>当前门是否已被本组件锁上。</summary>
        private bool _locked;

        /// <summary>是否已经成功刷过一次（用于停止 <see cref="Update"/> 里的轮询）。</summary>
        private bool _polled;

        private void Awake()
        {
            _gate = GetComponent<SceneGate>();
            _interactable = GetComponent<Interactable>();
        }

        private void OnEnable()
        {
            //⚠️ 必须成对退订：静态事件不随场景销毁，漏退订会让已销毁的门继续被调用（历史坑 T7）
            GameEvents.QuestsChanged += Refresh;
        }

        private void OnDisable()
        {
            GameEvents.QuestsChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            if (_polled) return;

            //⭐ 与 QuestService / ProgressService 同一套路：单例都是 BeforeSceneLoad 自动创建的，
            //   Awake 顺序不保证，Start 时可能还没就绪。只试一次会永久"没反应"，所以每帧重试。
            var svc = QuestService.Instance;
            if (svc == null || !svc.IsReady) return;

            _polled = true;
            Refresh();
        }

        /// <summary>按当前任务状态重算大门目标。订阅 <c>QuestsChanged</c> 后自动调用。</summary>
        public void Refresh()
        {
            if (_gate == null) return;

            var svc = QuestService.Instance;
            if (svc == null || !svc.IsReady) return;//未就绪：保持现状，等 Update 重试

            string target = FindActiveTargetScene(svc);

            if (string.IsNullOrEmpty(target))
            {
                if (_applied == null && _locked) return;//本来就是锁着且没覆写，不必重复处理
                _gate.ClearTarget();
                _applied = null;
                ApplyLocked();
                Debug.Log("[QuestGate] 没有进行中的任务 → 大门锁上（" + NoTargetHint + "）");
                return;
            }

            if (_applied == target && !_locked) return;//目标没变且是解锁状态，不重复打日志

            _gate.SetTarget(target);
            _applied = target;
            ApplyUsable(target);
            Debug.Log("[QuestGate] 大门目标 → " + target + "（提示：" + PromptFor(target) + "）");
        }

        /// <summary>锁门：按键无效，但提示条仍显示「暂无任务地点」（暗色，说明原因而不是凭空消失）。</summary>
        private void ApplyLocked()
        {
            _locked = true;
            if (_interactable != null) _interactable.SetLocked(true, NoTargetHint);
        }

        /// <summary>解锁：文案跟随任务地点变「前往覆雪林地」。</summary>
        private void ApplyUsable(string sceneName)
        {
            _locked = false;
            if (_interactable == null) return;

            _interactable.SetLocked(false);
            _interactable.SetPrompt(PromptFor(sceneName));
        }

        /// <summary>门上的提示文案。显示名来自 <see cref="SceneCatalog"/>，查不到就用场景文件名。</summary>
        private string PromptFor(string sceneName)
        {
            string display = _catalog != null ? _catalog.DisplayNameOf(sceneName) : sceneName;
            return "前往" + display;
        }

        /// <summary>取第一条「进行中且有地点」的任务的目标场景；没有返回 null。</summary>
        private static string FindActiveTargetScene(QuestService svc)
        {
            var all = svc.All;
            for (int i = 0; i < all.Count; i++)
            {
                var st = all[i];
                if (st.Status != QuestStatus.Active) continue;
                if (st.Config == null) continue;
                if (string.IsNullOrEmpty(st.Config.targetScene)) continue;
                return st.Config.targetScene;
            }
            return null;
        }
    }
}
