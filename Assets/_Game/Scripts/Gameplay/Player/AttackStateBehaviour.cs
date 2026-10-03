using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 挂在攻击动画状态上：进入 / 退出时通知 PlayerAttack，
    /// 用**动画状态机回调替代每帧轮询**。
    /// （在 PlayerAC.controller 的 Attack1~4 四个状态上各挂一个）
    /// </summary>
    public class AttackStateBehaviour : StateMachineBehaviour
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
            var attack = animator.GetComponent<PlayerAttack>();
            if (attack != null) attack.SetAttacking(value);
        }
    }
}
