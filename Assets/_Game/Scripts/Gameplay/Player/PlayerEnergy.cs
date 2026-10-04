using System;
using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家体力（GDD §4.8）：一般移动免费，冲刺 / 普攻 / 特殊攻击收费。
    ///
    /// ⚠️ 所有消耗必须走本类的 <c>TrySpend*</c> 出口，**禁止任何动作自行扣体力**（统一出口原则）。
    /// ⚠️ 各动作的**成本表也集中在这里** —— 其它组件只依赖本类，不必各自持有 PlayerConfig。
    /// </summary>
    public class PlayerEnergy : MonoBehaviour
    {
        /// <summary>(当前体力, 体力上限)。HUD 体力条订阅。</summary>
        public event Action<float, float> OnEnergyChanged;

        /// <summary>
        /// 体力不足以支付本次消耗时触发 —— 用于 HUD 闪烁等反馈（GDD §4.8 要求"动作不触发 + 明确提示"）。
        /// ⚠️ 提示音待 M5 接入音频系统时补，这里先只做视觉反馈。
        /// </summary>
        public event Action OnSpendFailed;

        [SerializeField] private PlayerConfig _config;

        public float MaxEnergy { get; private set; }
        public float CurEnergy { get; private set; }

        private float _regenDelayTimer;//距离开始回复还剩多久
        private float _lastBroadcast = -1f;

        private void Start()
        {
            MaxEnergy = _config.maxEnergy;
            CurEnergy = MaxEnergy;
            Broadcast(force: true);
        }

        private void Update()
        {
            //再生延迟：消耗后要静置一段时间才开始回（GDD §4.8：停止消耗 0.6s 后开始回复）
            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= Time.deltaTime;
                if (_regenDelayTimer > 0f) return;
            }

            if (CurEnergy < MaxEnergy)
            {
                CurEnergy = Mathf.Min(MaxEnergy, CurEnergy + _config.energyRegenRate * Time.deltaTime);
                Broadcast();
            }
        }

        // ==================== 唯一的消耗出口 ====================

        /// <summary>
        /// 通用消耗出口。体力不足时返回 false 且**不扣减**，
        /// 调用方必须据此**拒绝执行该动作**（GDD §4.8：消耗 > 当前体力 就不执行）。
        /// </summary>
        public bool TrySpend(float cost)
        {
            if (CurEnergy < cost)
            {
                OnSpendFailed?.Invoke();
                return false;
            }

            CurEnergy -= cost;
            _regenDelayTimer = _config.energyRegenDelay;//重置再生延迟
            Broadcast(force: true);
            return true;
        }

        /// <summary>尝试支付一次冲刺。不足则返回 false。</summary>
        public bool TrySpendDash() => TrySpend(_config.dashEnergyCost);

        /// <summary>
        /// 尝试支付第 <paramref name="comboIndex"/> 段普攻（0 基）。
        /// 不足则返回 false —— 该段**不可接续**（前一段正常播完，见 GDD §4.8 连段细则）。
        /// </summary>
        public bool TrySpendAttack(int comboIndex)
        {
            var costs = _config.attackEnergyCost;
            if (costs == null || costs.Length == 0) return true;//未配置则视为免费

            int i = Mathf.Clamp(comboIndex, 0, costs.Length - 1);
            return TrySpend(costs[i]);
        }

        /// <summary>
        /// 尝试支付一次特殊攻击。<paramref name="overrideCost"/> ≥ 0 时用武器自己的覆盖值
        /// （GDD §4.8：特殊攻击消耗 30，武器可覆盖该值）。
        /// </summary>
        public bool TrySpendSpecial(float overrideCost = -1f)
            => TrySpend(overrideCost >= 0f ? overrideCost : _config.specialEnergyCost);

        private void Broadcast(bool force = false)
        {
            //回复是每帧执行的，值没变就别刷 UI
            if (!force && Mathf.Abs(CurEnergy - _lastBroadcast) < 0.01f) return;
            _lastBroadcast = CurEnergy;
            OnEnergyChanged?.Invoke(CurEnergy, MaxEnergy);
        }
    }
}
