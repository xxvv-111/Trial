using System;
using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家体力。规则见 GDD §4.8：一般移动免费，冲刺 / 普攻 / 特殊攻击收费。
    /// ⚠️ 所有消耗必须走 <see cref="TrySpend"/> 这**唯一出口**，禁止任何动作自行扣体力。
    /// </summary>
    public class PlayerEnergy : MonoBehaviour
    {
        /// <summary>(当前体力, 体力上限)。HUD 体力条订阅。</summary>
        public event Action<float, float> OnEnergyChanged;

        [SerializeField] private PlayerConfig _config;

        public float MaxEnergy { get; private set; }
        public float CurEnergy { get; private set; }

        private void Start()
        {
            MaxEnergy = _config.maxEnergy;
            CurEnergy = MaxEnergy;
            Broadcast();
        }

        /// <summary>
        /// 唯一的体力消耗出口。体力不足时返回 false 且**不扣减**，
        /// 调用方必须据此**拒绝执行该动作**（GDD §4.8：体力不足则硬性禁止）。
        /// </summary>
        public bool TrySpend(float cost)
        {
            if (CurEnergy < cost) return false;
            CurEnergy -= cost;
            Broadcast();
            return true;
        }

        private void Broadcast() => OnEnergyChanged?.Invoke(CurEnergy, MaxEnergy);
    }
}
