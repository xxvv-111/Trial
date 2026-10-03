using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>玩家血量。只负责扣血与广播，死亡判定与状态切换由 PlayerFSM 统一负责。</summary>
    public class PlayerHealth : MonoBehaviour
    {
        //血量变化事件，血条（HPBar）订阅
        public event Action<int, int> OnHpChanged;

        [SerializeField] private PlayerConfig _config;

        public int MaxHp { get; private set; }
        public int CurHp { get; private set; }

        public bool IsDead => CurHp <= 0;

        /// <summary>受击无敌时长。读配置，缺配置时回退到 0.8s，避免 NRE。</summary>
        public float HitInvulnTime => _config != null ? _config.hitInvulnTime : 0.8f;

        private void Start()
        {
            MaxHp = _config.maxHp;
            CurHp = MaxHp;
            OnHpChanged?.Invoke(CurHp, MaxHp);//初始化血量
        }

        public void ApplyDamage(int damage)
        {
            CurHp = Mathf.Max(0, CurHp - damage);
            Broadcast();
        }

        private void Broadcast() => OnHpChanged?.Invoke(CurHp, MaxHp);
    }
}
