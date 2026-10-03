using UnityEngine;

namespace Game.Gameplay
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private int damage = 10;

        private Vector3 _velocity;//飞行速度
        private Transform _owner;//谁开的枪

        public void Launch(Vector3 velocity,Transform owner)
        {
            _velocity= velocity;
            _owner = owner;
            Destroy(gameObject, lifeTime);//定时自销毁
        }

        private void Update()
        {
            transform.position += _velocity * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner!=null&&other.transform.IsChildOf(_owner)) return;//不伤害自己
            
            if(other.TryGetComponent<PlayerFSM>(out var ph))
            {
                ph.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger) Destroy(gameObject);//打墙消失
        }
    }
}