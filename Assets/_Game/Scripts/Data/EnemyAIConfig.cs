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

        [Header("受击反应（GDD §6.1：小怪会被打断，Boss 不会）")]
        [Tooltip("是否会被受击打断（硬直）。\n⚠️ 小怪 true；**Boss 必须设 false** —— Boss 只闪白、不硬直。")]
        public bool canBeInterrupted = true;

        [Tooltip("受击硬直时长（秒）。仅在 canBeInterrupted = true 时生效。")]
        public float hitStunTime = 0.4f;

        [Tooltip("受击后退距离（米）。0 = 不击退。")]
        public float knockBackDistance = 0.4f;

        [Tooltip("受击闪白时长（秒）。")]
        public float flashTime = 0.15f;
    }
}
