using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 第三人称跟随相机：**鼠标控制视角（环绕）+ 滚轮缩放**。
    ///
    /// 结构：
    ///   · <c>_yaw</c> / <c>_pitch</c> 决定机位方向（球面环绕），<c>_distance</c> 决定远近
    ///   · 机位 = 焦点 + 旋转 × 后退 × 距离，再平滑趋近（避免抖动）
    ///   · 焦点 = 玩家位置 + <c>_heightOffset</c>（抬起一点，看向上半身而非脚底）
    ///
    /// 平滑只在**位置**上做（<c>SmoothDamp</c>）—— 角度是即时响应的，位置跟随自然就柔顺，
    /// 无需再对角度做二次平滑（那样只会变钝）。
    ///
    /// ⚠️ 视角可旋转后，**移动必须改成「相对相机」**，否则按 W 会朝世界 +Z 走而不是屏幕上方 ——
    ///    见 <see cref="PlayerMotor"/>。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _heightOffset = 1.2f;//看向玩家胸口而不是脚底

        [Header("视角（球面环绕）")]
        [SerializeField] private float _yaw = 0f;          //水平角
        [SerializeField] private float _pitch = 55f;       //俯角（越大越接近正俯视）
        [SerializeField] private float _minPitch = 15f;
        [SerializeField] private float _maxPitch = 85f;

        [Header("距离")]
        [SerializeField] private float _distance = 10f;
        [SerializeField] private float _minDistance = 4f;
        [SerializeField] private float _maxDistance = 22f;
        [SerializeField] private float _zoomStep = 1.2f;   //每格滚轮改变的距离

        [Header("手感")]
        [SerializeField] private float _lookSensitivity = 0.16f;//鼠标灵敏度（度/像素）
        [SerializeField] private float _smoothTime = 0.08f;     //位置平滑，越小越跟手

        [Header("鼠标指针")]
        [Tooltip("勾上 = 进入游戏即锁定指针（正式游玩手感）。取消 = 指针自由，便于在编辑器里调试。运行中按 Esc 解锁，点击游戏窗口重新锁定。")]
        [SerializeField] private bool _lockCursorOnStart = false;

        private Vector3 _velocity;//SmoothDamp 用，必须是字段

        private void Start()
        {
            if (_lockCursorOnStart) LockCursor(true);
        }

        private void Update()
        {
            if (InputService.Instance != null)
            {
                Vector2 look = InputService.Instance.LookDelta;
                _yaw += look.x * _lookSensitivity;
                _pitch -= look.y * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

                float scroll = InputService.Instance.ScrollDelta;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _distance -= scroll * _zoomStep;
                    _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
                }
            }

            //Esc 解锁指针、点击重新锁定（仅在开启锁定时生效）
            if (_lockCursorOnStart)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
                if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 focus = _target.position + Vector3.up * _heightOffset;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = focus + rot * Vector3.back * _distance;

            //用 Realtime：结算暂停（timeScale = 0）时相机也不应卡死
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            //始终看向焦点（用当前实际机位算，过渡更自然）
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>当前视角的水平朝向（供其它系统做「相对相机」换算）。</summary>
        public Quaternion PlanarRotation => Quaternion.Euler(0f, _yaw, 0f);
    }
}
