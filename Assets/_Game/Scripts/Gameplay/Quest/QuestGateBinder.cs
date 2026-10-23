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
    ///   <item>没有进行中的任务 ⇒ <c>ClearTarget()</c>，回落到 Inspector 里的默认目标。</item>
    /// </list>
    ///
    /// <para>━━ 挂法 ━━</para>
    /// <para>与 <see cref="SceneGate"/> 挂在**同一个物体**上（下标里已用 RequireComponent 保证）。</para>
    /// </summary>
    [RequireComponent(typeof(SceneGate))]
    public class QuestGateBinder : MonoBehaviour
    {
        private SceneGate _gate;

        /// <summary>当前由任务写进去的场景名。<c>null</c> = 用的是 Inspector 默认值。</summary>
        private string _applied;

        /// <summary>是否已经成功刷过一次（用于停止 <see cref="Update"/> 里的轮询）。</summary>
        private bool _polled;

        private void Awake()
        {
            _gate = GetComponent<SceneGate>();
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
                if (_applied == null) return;//本来就没覆写，不必重复清
                _gate.ClearTarget();
                _applied = null;
                Debug.Log("[QuestGate] 没有进行中的任务 → 大门回落到默认目标（" + _gate.TargetScene + "）");
                return;
            }

            if (_applied == target) return;//目标没变，不重复打日志

            _gate.SetTarget(target);
            _applied = target;
            Debug.Log("[QuestGate] 大门目标 → " + target);
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
