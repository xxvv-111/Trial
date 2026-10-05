using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 挂在 **`Special_Attack`** 动画状态上：进入 / 退出时通知 <see cref="PlayerFSM"/>
    /// "施法动画正在播 / 已结束"，用**动画状态机回调替代每帧轮询**。
    /// （与 <see cref="AttackStateBehaviour"/> 完全同一套思路，只是通知对象换成 FSM。）
    ///
    /// ⚠️ 为什么不复用 `AttackStateBehaviour`：那个改的是 `PlayerAttack.isAttacking`，
    ///   而该标记被 `PlayerFSM.IsAttackAnimOver()` 与 `PlayerAttack` 的连段逻辑共用 ——
    ///   让特殊攻击也去改它，语义会混（连段窗口判断会被误影响）。
    /// </summary>
    public class SpecialStateBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, true);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, false);
        }

        private void Set(Animator animator, bool value)
        {
            var fsm = animator.GetComponent<PlayerFSM>();
            if (fsm != null) fsm.SetCasting(value);
        }
    }
}
