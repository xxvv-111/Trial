using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "EnemyAIConfig", menuName = "Game/EnemyAIConfig")]
    public class EnemyAIConfig : ScriptableObject
    {
        public string displayName = "拳击手";
        public int hp = 30;
        public float aggroRange = 8f;//追击距离
        public float attackRange = 1f;//攻击距离
        public float moveSpeed = 3f;//追击速度
        public float windupTime = 1f;//前摇
        public float recoverTime = 1.5f;//后摇
        public int damage = 10;
        public GameObject prefab;//可选,按表生成怪用
        public GameObject hitEffect;//受击特效
    }
}