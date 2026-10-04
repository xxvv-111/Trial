using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家移动：读输入 → **按相机朝向换算方向** → CharacterController 移动 → 驱动动画混合树。
    ///
    /// ⚠️ **移动是「相对相机」的**（W = 屏幕上方）。相机现在可被鼠标旋转，
    ///    若用世界轴会变成「转视角后按 W 往斜里走」，手感直接崩。
    ///
    /// ⚠️ 已知问题（T1）：PlayerFSM 用 <c>_motor.enabled = false</c> 来停止移动，
    ///    这会把本脚本一起停掉，连贴地位移也停了（跳跃已取消，故降为低优先级）。
    /// ⚠️ 已知问题（T2）：玩家身上同时有 Rigidbody + CapsuleCollider + CharacterController，
    ///    建议只保留 CharacterController。
    /// </summary>
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        [Tooltip("相机 Transform。留空则自动取 Camera.main。用于把输入换算成「相对相机」的方向。")]
        [SerializeField] private Transform _camera;

        [SerializeField] private float groundStick = 0.1f;//每帧向下的贴地位移
        [SerializeField] private float turnSpeed = 720f;//转身速度（度/秒）

        private CharacterController _cc;
        private Animator _anim;
        private float speed;//移动速度（来自配置）

        private void Awake()
        {
            speed = config.moveSpeed;
            _anim = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (_camera == null && Camera.main != null)
                _camera = Camera.main.transform;
        }

        private void Update()
        {
            Vector2 axis = InputService.Instance.Move;
            bool hasInput = axis.sqrMagnitude > 0.01f;

            Vector3 dir = CameraRelative(axis);

            //移动
            Vector3 move = dir * (speed * Time.deltaTime);
            _cc.Move(move + Vector3.down * groundStick);//向下挤压一点保证贴地

            //朝向：转向移动方向（不是转向相机方向）
            if (hasInput) Face(dir);

            //驱动动画混合树
            float target = hasInput ? speed : 0f;
            _anim.SetFloat("speed", target, 0.15f, Time.deltaTime);
        }

        /// <summary>
        /// 把输入(Vector2)换算成世界方向：**以相机为参照系**。
        /// axis.y = 前/后（屏幕上下），axis.x = 左/右（屏幕左右）。
        /// </summary>
        private Vector3 CameraRelative(Vector2 axis)
        {
            if (axis.sqrMagnitude <= 0.01f) return Vector3.zero;

            Vector3 fwd = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_camera != null)
            {
                //相机前向投影到水平面 —— 这就是「屏幕上方」在世界里的方向
                fwd = _camera.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

                right = _camera.right;
                right.y = 0f;
                right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            }

            return (fwd * axis.y + right * axis.x).normalized;
        }

        /// <summary>朝移动方向限速转身。</summary>
        private void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude <= 0.001f) return;

            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
