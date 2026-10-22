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

        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseBossDied() => BossDied?.Invoke();
        public static void RaiseEnemyKilled(string enemyId, Vector3 position) => EnemyKilled?.Invoke(enemyId, position);
    }
}
