using Game.Core;
using Game.Data;
using JetBrains.Annotations;
using UnityEngine;
namespace Game.Gameplay
{
    public class EnemyMelee : MonoBehaviour,IDamageable
    {
        [Header("数值")]
        [SerializeField] private EnemyConfig config;
        private int hp;
        private float speed;
        //追击距离
        private float chaseRange;
        //玩家位置
        private Transform _player;
        public int HP => hp;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player").transform;
            hp = config.maxHp;
            speed = config.moveSpeed;
            chaseRange = config.chaseRange;
        }

        void Update()
        {
            if (_player == null) return;

            //计算距离忽略高度
            Vector3 target = new Vector3(_player.position.x, transform.position.y, _player.position.z);
            float dist = Vector3.Distance(transform.position, target);

            //追击
            if (dist < chaseRange)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

                Vector3 dir = target - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
        }

        //受击，玩家攻击调用
        public void TakeDamage(int dmg)
        {
            hp -= dmg;
            Debug.Log($"[nemyMelee]{name}挨打 -{dmg}，剩{hp}");
            if (hp <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            EventCenter.Publish(new EnemyDiedEvent { enemy = this, pos = transform.position });
            Destroy(gameObject);
        }
    }
}
