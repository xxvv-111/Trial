using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("敌人基础")]
        public int maxHp = 100;
        public int attacKDamage = 5;
        public float moveSpeed = 2f;
        public float chaseRange = 6f;
    }
}