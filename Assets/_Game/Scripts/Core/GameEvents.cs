using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>全局流程事件门面。⚠️ 订阅方必须成对订阅/退订（OnEnable/OnDisable），否则跨场景会重复。</summary>
    public static class GameEvents
    {
        public static event Action PlayerDied;
        public static event Action BossDied;

        /// <summary>
        /// 敌人死亡。参数 = (enemyId, 死亡位置)。
        ///
        /// <para><c>enemyId</c> 来自 <c>EnemyHealth.ConfigId</c>（即 <c>EnemyAIConfig.enemyId</c>），
        /// **可能是空字符串**（配置没填）。订阅方要自己跳过空 id，不要默认它一定有值。</para>
        ///
        /// <para>⚠️ 刻意**不传 <c>EnemyHealth</c> 实例**：那样 <c>Game.Core</c> 就会依赖 <c>Game.Gameplay</c>，
        /// 而 Gameplay 本来就依赖 Core —— 将来一旦拆 asmdef 就是循环引用。位置信息足够掉落物用了。</para>
        /// </summary>
        public static event Action<string, Vector3> EnemyKilled;

        /// <summary>
        /// 进入某个场景（含**第一次**进游戏的那个场景）。参数 = 场景名，取自
        /// <c>SceneManager.GetActiveScene().name</c>，与 <c>SceneCatalog</c> 里的 key 一致。
        ///
        /// <para>由 <see cref="SceneFlow"/> 转发 —— 它是常驻对象，所以切场景不会丢订阅。</para>
        /// </summary>
        public static event Action<string> SceneEntered;

        /// <summary>
        /// 玩家进入了某个"探查点"的范围。参数 = 点的 <c>pointId</c>。
        ///
        /// <para>由场景里的 <c>Gameplay.QuestPointTrigger</c> 发出。任务目标类型
        /// <c>ReachPoint</c> 靠它推进 —— 比"到达场景"更细：可以要求玩家走到地图某个具体位置。</para>
        /// </summary>
        public static event Action<string> PointReached;

        /// <summary>
        /// 任务数据变了（**脏通知**，不带参数）。订阅方收到后自己去
        /// <c>QuestService</c> 拉一份列表快照重建 UI。
        ///
        /// <para>为什么不做成 <c>QuestUpdated(questId, cur, need)</c>：UI 需要的是"列表快照"，
        /// 而且接取 / 可交付 / 已完成这些**状态变化**也要刷新。为每种变化定义一个事件会让订阅方
        /// 变成一堆 if；一个脏通知 + 主动拉取，代码少一半且不会漏。</para>
        /// </summary>
        public static event Action QuestsChanged;

        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseBossDied() => BossDied?.Invoke();
        public static void RaiseEnemyKilled(string enemyId, Vector3 position) => EnemyKilled?.Invoke(enemyId, position);
        public static void RaiseSceneEntered(string sceneName) => SceneEntered?.Invoke(sceneName);
        public static void RaisePointReached(string pointId) => PointReached?.Invoke(pointId);
        public static void RaiseQuestsChanged() => QuestsChanged?.Invoke();
    }
}
