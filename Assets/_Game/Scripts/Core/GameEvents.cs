using System;

namespace Game.Core
{
    /// <summary>全局流程事件门面。⚠️ 订阅方必须成对订阅/退订（OnEnable/OnDisable），否则跨场景会重复。</summary>
    public static class GameEvents
    {
        public static event Action PlayerDied;
        public static event Action BossDied;

        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseBossDied() => BossDied?.Invoke();
    }
}