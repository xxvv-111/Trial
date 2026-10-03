using Game.Core;
using Game.Data;
using JetBrains.Annotations;
using UnityEngine;
namespace Game.Gameplay
{

    public class Target : MonoBehaviour, IDamageable
    {
        [Header("数值")]
        [SerializeField] private EnemyConfig config;
        private int hp;

        void Start()
        {
            hp = 10000;
        }

        void Update()
        {

        }

        //受击，玩家攻击调用
        public void TakeDamage(int dmg)
        {
            hp -= dmg;
            Debug.Log($"[nemyMelee]{name}挨打 -{dmg}，剩{hp}");
        }
    }
}
