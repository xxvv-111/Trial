//角色状态
namespace Game.Gameplay
{
    /// <summary>
    /// 玩家状态。⚠️ 新增状态时记得：① 在 <see cref="PlayerFSM.Awake"/> 里补三张字典的表项；
    /// ② 在 <c>PlayerAC.controller</c> 里加对应状态与转场。
    /// </summary>
    public enum PlayerState { Idle, Run, Dash, Attack, Special, Hit, Death }
}
