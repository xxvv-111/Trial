using Game.Core;
using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 敌人血量。实现 IDamageable，死亡时广播 <see cref="Died"/> 供房间计数。
    ///
    /// ⚠️ **T7**：<see cref="Died"/> 是 **static 事件**（所有敌人共用一个订阅点），
    ///   跨场景重开理论上存在重复订阅风险。RoomController 已用 OnEnable/OnDisable 成对订阅兜住，
    ///   风险实际较低；若要彻底稳妥可改为实例事件。
    ///
    /// ⚠️ **T21**：本类受击时**只扣血、不通知 AI** —— 导致**小怪挨打毫无反应**（连闪白都不触发）。
    ///   按 GDD §6.1「小怪会被打断」，M1.2 需要在这里通知 AI 进入 Hit 硬直 + 闪白；
    ///   而 Boss 侧要保证不被此逻辑打断。
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static event Action<EnemyHealth> Died;

        [SerializeField] private EnemyAIConfig _config;

        public int MaxHp { get; private set; }
        public int CurHp { get; private set; }

        private void OnEnable()
        {
            //重置于满血：让被复用的敌人（场景重开 / 对象池）状态干净
            MaxHp = _config.hp;
            CurHp = MaxHp;
        }

        public void TakeDamage(int damage)
        {
            CurHp -= damage;
            if (CurHp <= 0) Die();
        }

        private void Die()
        {
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
