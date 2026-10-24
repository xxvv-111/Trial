namespace Game.Gameplay
{
    /// <summary>
    /// <see cref="IInteractable"/> 的**可选扩展**：物体当前不可交互时，
    /// 仍然希望告诉玩家"为什么不能按"，而不是让提示条直接消失。
    ///
    /// <para>━━ 为什么是独立接口，而不是往 IInteractable 里塞一个成员 ━━</para>
    /// <para>
    /// 主接口的约定是「<c>CanInteract == false</c> ⇒ 完全不参与选中」，这对绝大多数情况是对的：
    /// 战斗中不能调查的探查点、魔晶不够的升级台 —— 提示条直接消失最干净。
    /// 但有些物体**必须解释原因**：酒馆大门在没有任务地点时是锁着的，
    /// 提示条若直接消失，玩家只会以为"这扇门坏了"。
    /// 用一个小接口单独表达这件事，就不必逼所有交互物实现一个永远用不到的成员。
    /// </para>
    ///
    /// <para>⚠️ 选中优先级：**可交互的物体永远优先**。只有在附近没有任何可交互目标时，
    /// 才会退而选中带 <see cref="DisabledHint"/> 的那个（提示条此时显示暗色文字）。</para>
    /// </summary>
    public interface IInteractableHint
    {
        /// <summary>
        /// 不可交互时提示条上显示的文字（例："暂无任务地点"）。
        /// 返回 null / 空串 = 不参与选中，退回「直接隐藏」的原行为。
        /// </summary>
        string DisabledHint { get; }
    }
}
