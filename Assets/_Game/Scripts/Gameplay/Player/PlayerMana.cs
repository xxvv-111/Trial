using System;
using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家**魔力**（2026-10-06 由「体力」改回「魔力」）。
    ///
    /// ⚠️ **规则变更**：游戏背景引入了魔法，所以资源语义变回**魔力**，并且
    /// <c>移动 / 冲刺 / 普攻全部免费</c>，**只有特殊攻击（火球）消耗魔力**。
    /// 原「体力」下的 <c>TrySpendDash</c> / <c>TrySpendAttack</c> 已删除。
    ///
    /// ⚠️ **魔力不随时间自动回复**（2026-10-06 二次变更）：本类**不再有 Update**，
    /// 恢复只能由显式来源触发 —— 唯一入口是 <see cref="Restore"/>（供击杀回魔 / 拾取回魔等接入）。
    /// 原 <c>manaRegenDelay</c> / <c>manaRegenRate</c> 两个配置字段已随之删除。
    ///
    /// ⚠️ 所有消耗必须走本类的 <c>TrySpend*</c> 出口（**统一出口原则**）—— 禁止动作自行扣魔力。
    /// ⚠️ 成本表也集中在这里 —— 其它组件只依赖本类，不必各自持有 PlayerConfig / WeaponConfig。
    /// </summary>
    public class PlayerMana : MonoBehaviour
    {
        /// <summary>(当前魔力, 魔力上限)。HUD 魔力条订阅。</summary>
        public event Action<float, float> OnManaChanged;

        /// <summary>
        /// 魔力不足以支付本次消耗时触发 —— 用于 HUD 闪烁等反馈（"动作不触发 + 明确提示"）。
        /// ⚠️ 提示音待 M5 接入音频系统时补，这里先只做视觉反馈。
        /// </summary>
        public event Action OnManaSpendFailed;

        [SerializeField] private PlayerConfig _config;

        [Tooltip("当前武器（M2.1）。为空时回退到 PlayerConfig 的兜底字段。\n" +
                 "⚠️ 成本表仍然**集中在本类**（统一出口原则）——其它组件只调 TrySpend*，不必自己持有武器。")]
        [SerializeField] private WeaponConfig _weapon;

        public float MaxMana { get; private set; }
        public float CurMana { get; private set; }

        private float _lastBroadcast = -1f;

        /// <summary>换武器（M2.2 用）。成本表随之切换。</summary>
        public void SetWeapon(WeaponConfig weapon)
        {
            _weapon = weapon;
        }

        private void Start()
        {
            MaxMana = _config.maxMana;
            CurMana = MaxMana;
            Broadcast(force: true);
        }

        // ⚠️ 2026-10-06：原来的 Update() 在这里做"静置 0.6s 后按 25/s 自动回复"，
        //    **已整段删除** —— 魔力不再自动回复。别再加回来。

        // ==================== 消耗出口 ====================

        /// <summary>
        /// 通用消耗出口。魔力不足时返回 false 且**不扣减**，
        /// 调用方必须据此**拒绝执行该动作**。
        /// </summary>
        public bool TrySpend(float cost)
        {
            if (CurMana < cost)
            {
                OnManaSpendFailed?.Invoke();
                return false;
            }

            CurMana -= cost;
            Broadcast(force: true);
            return true;
        }

        /// <summary>
        /// 尝试支付一次特殊攻击。<paramref name="overrideCost"/> ≥ 0 时用调用方给的覆盖值，
        /// 否则**武器配置优先**（默认 30，武器可覆盖该值）。
        ///
        /// ⭐ 这是本类**唯一**被动作调用的消耗入口 —— 冲刺与普攻已改为免费。
        /// </summary>
        public bool TrySpendSpecial(float overrideCost = -1f)
        {
            if (overrideCost >= 0f) return TrySpend(overrideCost);

            if (_weapon != null) return TrySpend(_weapon.specialManaCost);

            return TrySpend(_config.specialManaCost);
        }

        // ==================== 恢复出口（唯一的"加魔力"入口） ====================

        /// <summary>
        /// **显式恢复魔力**。⭐ 这是魔力唯一的增加途径 —— 本类不会随时间自动回复。
        ///
        /// 预留接入点（后续里程碑按需接，当前**没有任何调用方**）：
        ///   · 击杀敌人回魔（在 `EnemyHealth` 死亡时调 `playerMana.Restore(n)`）
        ///   · 拾取物回魔（魔力药水 / 精英掉落）
        ///   · 关卡奖励 / 复活时补满
        ///
        /// <paramref name="amount"/> ≤ 0 时直接忽略（避免误调时出现"减魔"）。
        /// </summary>
        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            if (CurMana >= MaxMana) return;

            CurMana = Mathf.Min(MaxMana, CurMana + amount);
            Broadcast(force: true);
        }

        /// <summary>把魔力补满（复活 / 过关补给用）。</summary>
        public void RestoreFull()
        {
            Restore(MaxMana);
        }

        private void Broadcast(bool force = false)
        {
            //值没变就别刷 UI（现在是"只在消耗/恢复时变化"，防抖依然有用）
            if (!force && Mathf.Abs(CurMana - _lastBroadcast) < 0.01f) return;
            _lastBroadcast = CurMana;
            OnManaChanged?.Invoke(CurMana, MaxMana);
        }
    }
}
