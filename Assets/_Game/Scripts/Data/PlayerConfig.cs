using UnityEngine;

namespace Game.Data
{
    /// <summary>玩家数值配置。所有数值改动走这里，不要在代码里硬编码。</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Game/PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("移动")]
        public float moveSpeed = 6f;
        public float dashSpeed = 10f;
        public float dashTimer = 0.5f;
        public float dashDelay = 0.1f;
        public float iFrameTime = 0.5f;//冲刺无敌时长

        [Header("生存")]
        public int maxHp = 100;
        public float hitInvulnTime = 0.8f;//受击后的无敌时长（原硬编码在 PlayerFSM）

        [Header("战斗")]
        public float comboWindow = 1f;//连击窗口
        [Tooltip("⚠️ M2.1 起已迁到 WeaponConfig —— 本字段仅在**未装配武器**时作兜底。")]
        public float attackRange = 2.5f;//判定中心离自己多远
        [Tooltip("⚠️ M2.1 起已迁到 WeaponConfig.damage —— 本字段仅在**未装配武器**时作兜底。")]
        public int[] attackDamage = { 12, 15, 10, 20 };//4 段普攻伤害

        [Header("魔力（2026-10-06 由「体力」改回「魔力」）")]
        [Tooltip("魔力上限。\n⚠️ 规则变更后 移动 / 冲刺 / 普攻 全部免费，**只有特殊攻击消耗魔力**。")]
        public float maxMana = 100f;
        [Tooltip("特殊攻击的兜底魔力消耗。⚠️ 武器配置了 specialManaCost 时以武器为准。")]
        public float specialManaCost = 30f;
        //⚠️ 2026-10-06：原 manaRegenDelay / manaRegenRate 两个字段**已删除**。
        //  魔力不再随时间自动回复（设计决定），恢复只能由显式来源触发
        //  —— 接入点是 PlayerMana.Restore(amount)（击杀回魔 / 拾取回魔等，后续里程碑再接）。
    }
}