using Game.Core;
using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyHealth : MonoBehaviour, IDamageable       
    {
        public static event Action<EnemyHealth> Died;

        [SerializeField] private EnemyAIConfig _config;

        public int MaxHp { get; private set; }
        public int CurHp { get; private set; }

        private void OnEnable()
        {
            MaxHp = _config.hp;
            CurHp = MaxHp;
        }

        public void TakeDamage(int damage)
        {
            CurHp -= damage;
            if (CurHp <= 0)  Die();
        }

        private void Die()
        {
            Died?.Invoke(this);

            gameObject.SetActive(false);
        }
    }
}
