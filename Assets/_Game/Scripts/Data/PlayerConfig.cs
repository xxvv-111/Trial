using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    /// <summary>玩家数值配置。所有数值改动走这里，不要在代码里硬编码。</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Game/PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        //⭐ 2026-10-13：移动从"单档速度"改成**走路 / 跑步双档**。
        //   规则：有移动输入 = 走路；有移动输入 **且** 按住加速键 = 跑步；松开加速键 = 自动回走路。
        //   ⚠️ 这两个数值必须与 Animator 混合树的阈值 **一一对应**（见 Player1_AC / Player2_AC 的 Locomotion 混合树）：
        //      阈值 = 0 / walkSpeed / runSpeed。只改这里不改混合树 → 动画会混到错误的档位。

        [Header("移动（走路 / 跑步 双档）")]
        [Tooltip("走路速度（米/秒）。有移动输入、**未按住加速键**时用这一档。\n" +
                 "⚠️ 必须同时改 Animator 混合树里 Walk 那一项的阈值。")]
        public float walkSpeed = 3f;

        [Tooltip("跑步速度（米/秒）。有移动输入、**按住加速键**（左 Shift / 手柄左摇杆按下）时用这一档。\n" +
                 "⚠️ 必须同时改 Animator 混合树里 Run 那一项的阈值。\n" +
                 "（本字段原名叫 moveSpeed，已自动迁移过来，旧值不会丢。）")]
        [FormerlySerializedAs("moveSpeed")]
        public float runSpeed = 6f;

        public float dashSpeed = 10f;
        public float dashTimer = 0.5f;
        public float dashDelay = 0.1f;
        public float iFrameTime = 0.5f;//冲刺无敌时长

        [Header("跳跃（2026-10-13 新增，同日改为双动画 + 落地后摇）")]
        [Tooltip("⭐ **滞空时长**（秒）—— 从起跳到位移落回最低点，**不含落地后摇**。\n" +
                 "默认 0.96 = **实测**的动画触地时刻（P1Kimodo_jump：0.96 s 时脚回到站立高度、重心降到最低）。\n" +
                 "⚠️ 这不是动画总长（动画 1.333 s）—— 差额 0.37 s 是「落地缓冲 + 起身」，由 landRecoverTime 接管。\n" +
                 "调大它 = 角色在空中飘得更久（会与动画脱节，落地会显得没有重量）。")]
        public float jumpTime = 0.96f;
        [Tooltip("跳起高度（米）。0.6 来自动画 RootT.y 曲线实测（0.805 → 1.403）。\n" +
                 "ℹ️ 逐帧采样得到重心实际抬升约 0.80 m（含收腿姿势）；想让跳跃更有劲可以试 0.7~0.8。")]
        public float jumpHeight = 0.6f;
        [Tooltip("⭐ **落地后摇**（秒）。触地后这段时间角色**完全定身**：不能移动、不能转向、也不能再次起跳，\n" +
                 "用来让「落地缓冲 → 起身站直」这段动画播完，落地才有重量感。\n" +
                 "默认 0.37 = 动画总长 1.333 − 实测触地 0.96。\n" +
                 "0 = 落地即可行动（旧行为，落地会很滑）。")]
        public float landRecoverTime = 0.37f;
        [Tooltip("空中水平速度的**上限**（米/秒）。\n" +
                 "⭐ 实际水平速度 = min(起跳瞬间的移动速度, 本值)：\n" +
                 "   静止起跳 → 0（原地跳）；走路中起跳 → walkSpeed；跑步中起跳 → runSpeed。\n" +
                 "调小它 = 跳跃时前进得更短（可用于「跳不远」的手感限制）。\n" +
                 "起跳瞬间的**方向**由输入锁定为空中航向（见 PlayerJump.BeginJump）。")]
        public float jumpAirSpeed = 6f;

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