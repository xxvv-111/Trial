using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 第三人称**越肩视角**相机：鼠标控制环绕 + 滚轮缩放 + 肩部横移。
    ///
    /// 与普通「跟随相机」的关键差别：
    ///   · 相机**不是看向玩家**，而是与环绕方向**平行**地看出去
    ///   · 加上 <c>_shoulderOffset</c> 横移后，角色自然落在屏幕一侧 → 这就是越肩构图
    ///
    /// 结构：
    ///   焦点 = 玩家 + 上抬 <c>_heightOffset</c>（肩/头高）
    ///   机位 = 焦点 + 环绕旋转 × (肩部横移, 0, 后退距离)
    ///   朝向 = 环绕旋转（**不是** LookAt 焦点 —— LookAt 会退化成"围绕角色的追尾相机"）
    ///
    /// ⚠️ 视角可旋转后，**移动必须「相对相机」**，见 <see cref="PlayerMotor"/>。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private Transform _target;

        [Tooltip("焦点高度（相对玩家脚底）。越肩视角一般取肩/头高，1.4~1.6。")]
        [SerializeField] private float _heightOffset = 1.5f;

        [Header("越肩偏移")]
        [Tooltip("横向偏移。正值 = 相机在玩家右肩（角色显示在屏幕左侧）；负值 = 左肩；0 = 正后方追尾。")]
        [SerializeField] private float _shoulderOffset = 0.7f;

        [Header("视角")]
        [Tooltip("水平角。")]
        [SerializeField] private float _yaw = 0f;
        [Tooltip("俯角。越肩视角一般 10~25；越大越接近俯视。")]
        [SerializeField] private float _pitch = 18f;
        [SerializeField] private float _minPitch = -10f;
        [SerializeField] private float _maxPitch = 70f;

        [Header("距离")]
        [SerializeField] private float _distance = 3.4f;
        [SerializeField] private float _minDistance = 1.5f;
        [SerializeField] private float _maxDistance = 12f;
        [Tooltip("每格滚轮改变的距离。")]
        [SerializeField] private float _zoomStep = 0.6f;

        [Header("手感")]
        [Tooltip("鼠标灵敏度（度/像素）。")]
        [SerializeField] private float _lookSensitivity = 0.35f;
        [Tooltip("位置平滑时间，越小越跟手。0 = 完全硬跟随。")]
        [SerializeField] private float _smoothTime = 0.06f;

        [Header("防穿墙")]
        [SerializeField] private bool _collisionCheck = true;
        [Tooltip("球形探测半径，避免相机贴面穿进墙里。")]
        [SerializeField] private float _collisionRadius = 0.2f;
        [Tooltip("撞墙后额外留出的间隙。")]
        [SerializeField] private float _collisionBuffer = 0.05f;

        [Header("鼠标指针")]
        [Tooltip("勾上 = 进入游戏即锁定指针（正式游玩手感）。取消 = 指针自由，便于在编辑器里调试。运行中按 Esc 解锁，点击游戏窗口重新锁定。")]
        [SerializeField] private bool _lockCursorOnStart = false;

        private Vector3 _velocity;//SmoothDamp 用，必须是字段

        /// <summary>当前视角的水平朝向（供其它系统做「相对相机」换算）。</summary>
        public Quaternion PlanarRotation => Quaternion.Euler(0f, _yaw, 0f);

        private void Start()
        {
            if (_lockCursorOnStart) LockCursor(true);

            //开局直接把相机摆到正确机位：
            //否则会从场景里存的旧位置「飞」过来，看起来就像「初始位置不对」。
            SnapToTarget();
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

            Vector3 focus = FocusPoint();
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = ResolvePosition(focus, rot);

            //用 Realtime：结算暂停（timeScale = 0）时相机也不应卡死
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            //关键：朝向 = 环绕旋转本身（不是 LookAt 焦点）
            transform.rotation = rot;
        }

        /// <summary>把相机瞬移到当前应该待的位置（开局调用，避免从旧位置飞过来）。</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            Vector3 focus = FocusPoint();
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

            transform.position = ResolvePosition(focus, rot);
            transform.rotation = rot;
            _velocity = Vector3.zero;
        }

        private Vector3 FocusPoint() => _target.position + Vector3.up * _heightOffset;

        /// <summary>算出机位：焦点 + 环绕旋转 × (肩部横移, 0, 后退)，并按需做防穿墙收缩。</summary>
        private Vector3 ResolvePosition(Vector3 focus, Quaternion rot)
        {
            Vector3 local = new Vector3(_shoulderOffset, 0f, -_distance);
            Vector3 desired = focus + rot * local;

            if (!_collisionCheck) return desired;

            //从焦点向机位做球形探测：撞到东西就把相机拉近
            Vector3 dir = desired - focus;
            float dist = dir.magnitude;
            if (dist <= 0.001f) return desired;

            RaycastHit hit;
            if (Physics.SphereCast(focus, _collisionRadius, dir.normalized, out hit, dist,
                                   ~0, QueryTriggerInteraction.Ignore))
            {
                float safe = Mathf.Max(_minDistance * 0.6f, hit.distance - _collisionBuffer);
                desired = focus + dir.normalized * safe;
            }

            return desired;
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
