using Game.Core;
using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    public class PlayerHealth : MonoBehaviour
    {
        //血量事件，血条订阅
        public event Action<int, int> OnHpChanged;

        [SerializeField] private PlayerConfig _config;

        public int MaxHp { get; private set; }
        public int CurHp { get; private set; }

        //private Animator _anim;w6
        public bool IsDead => CurHp <= 0;

        //死亡事件
        public event Action Died;

        private void Awake()
        {
            //_anim = GetComponent<Animator>();w6
        }

        void Start()
        {
            MaxHp = _config.maxHp;
            CurHp = MaxHp;
            OnHpChanged?.Invoke(CurHp, MaxHp);//初始化血量
        }

        public void ApplyDamage(int damage)
        {
            //if (_dead) return;w6
            CurHp = Mathf.Max(0, CurHp - damage);
            //if (CurHp <= 0) Die();w6
            //else _anim.SetTrigger("Hit");w6
            Broadcast();//广播血量变化
        }

        private void Broadcast() => OnHpChanged?.Invoke(CurHp, MaxHp);

        //private void Die()w6
        //{
        //    _dead = true;
        //    _anim.SetBool("IsDead", true);
        //    Died?.Invoke();

        //    GetComponent<PlayerAttack>()?.OnPlayerDied();
        //    GetComponent<PlayerMotor>().enabled = false;
        //    GetComponent<PlayerDash>().enabled = false;
        //}
    }
}