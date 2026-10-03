using UnityEngine;
namespace Game.Gameplay
{
    public class PlayerInput : MonoBehaviour
    {
        //移动方向
        public Vector2 moveAxis
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
        //是否冲刺
        public bool DashPressedThisFrame
        {
            get
            {
                return Input.GetKeyDown(KeyCode.Space)||Input.GetMouseButtonDown(1);
            }
        }
    }
}