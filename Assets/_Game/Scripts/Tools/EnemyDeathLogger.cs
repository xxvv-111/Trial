using UnityEngine;

using Game.Core;
namespace Game.Gameplay
{
    public class EnemyDeathLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            EventCenter.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            Debug.Log($"[EnemyDeathLogger] 敌人死亡！位置：{evt.pos}");
        }
    }
}