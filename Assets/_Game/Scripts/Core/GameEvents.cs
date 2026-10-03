using System;

namespace Game.Core
{
    public class GameEvents
    {
        public static event Action PlayerDied;
        public static event Action BossDied;

        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseBossDied() => BossDied?.Invoke();
    }
}