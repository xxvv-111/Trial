using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家移动：读输入 → CharacterController 移动 → 驱动动画混合树。
    /// ⚠️ 已知问题（T1）：PlayerFSM 用 <c>_motor.enabled = false</c> 来"停止移动"，
    ///    这会把本脚本一起停掉，连贴地位移也停了（跳跃已取消，故降为低优先级）。
    /// ⚠️ 已知问题（T2）：玩家身上同时有 Rigidbody + CapsuleCollider + CharacterController，
    ///    建议只保留 CharacterController。
    /// </summary>
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        [SerializeField] private float groundStick = 0.1f;//每帧向下的贴地位移

        private CharacterController _cc;
        private Animator _anim;
        private float speed;//移动速度（来自配置）

        private void Awake()
        {
            speed = config.moveSpeed;
            _anim = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Vector2 axis = InputService.Instance.Move;
            Vector3 move = new Vector3(axis.x, 0f, axis.y) * (speed * Time.deltaTime);

            Face(axis);

            //向下挤压一点保证贴地（俯视角无跳跃，不需要真正的重力模拟）
            _cc.Move(move + Vector3.down * groundStick);

            //驱动动画混合树
            float target = axis.sqrMagnitude > 0.01f ? speed : 0f;
            _anim.SetFloat("speed", target, 0.15f, Time.deltaTime);
        }

        /// <summary>把输入方向转成朝向（俯视固定镜头下，屏幕方向即世界轴）。</summary>
        private void Face(Vector2 a)
        {
            if (a.sqrMagnitude <= 0.01f) return;//没输入就保持当前朝向

            Vector3 moveDir = new Vector3(a.x, 0f, a.y);
            Quaternion target = Quaternion.LookRotation(moveDir);

            //限速转向，避免瞬间扭头
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, 720f * Time.deltaTime);
        }
    }
}
