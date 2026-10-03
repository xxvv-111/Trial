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
        public float attackRange = 2.5f;//判定中心离自己多远
        public int[] attackDamage = { 12, 15, 10, 20 };//4 段普攻伤害

        [Header("体力")]
        public float maxEnergy = 100f;
        public float dashEnergyCost = 20f;//冲刺消耗
        public int[] attackEnergyCost = { 8, 10, 12, 15 };//4 段普攻消耗（⚠️ 数值待实测校准）
        public float specialEnergyCost = 30f;//特殊攻击消耗（各武器可覆盖）
        public float energyRegenDelay = 0.6f;//停止消耗后多久开始回复
        public float energyRegenRate = 25f;//每秒回复量
    }
}