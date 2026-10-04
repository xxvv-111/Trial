using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 第三人称跟随相机：**固定俯角、始终看向角色**，鼠标可微调视角、滚轮缩放。
    ///
    /// 机位算法（简单且稳定）：
    ///   焦点 = 角色 + 上抬 <c>_focusHeight</c>（身体中部）
    ///   视线 = 俯角 <c>_pitch</c> × 水平角 <c>_yaw</c>
    ///   机位 = 焦点 **沿视线后退** <c>_distance</c>
    ///   → 因为机位是从焦点沿视线推出来的，**视线必然穿过角色**，
    ///     所以角色永远在画面中心，不会跑偏、不会出画。
    ///
    /// ⚠️ 本版**去掉了越肩横移与防穿墙**：
    ///    那两处逻辑（尤其每帧 SphereCast）会让机位在「近/远」之间跳变 → **相机抖动**。
    ///    若之后确实遇到穿墙，再以带阻尼的方式加回，而不是每帧硬切换。
    ///
    /// ⚠️ **水平角不跟随角色朝向**（`_followTargetYaw` 默认 false）。
    ///    原因：工程里「移动方向相对相机」+「角色转向移动方向」已构成两环，
    ///    若相机再跟随角色朝向，就形成**正反馈**：按 A → 角色左转 → 相机左转 →
    ///    「左边」又变了 → 角色继续转 …… 结果按 WASD 时**视角持续旋转**。
    ///    正确分工：**相机 yaw 归鼠标，角色朝向归移动方向**，两者互不驱动。
    ///
    /// ⚠️ 视角可旋转后，**移动必须「相对相机」**，见 <see cref="PlayerMotor"/>。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private Transform _target;

        [Tooltip("视线焦点相对角色脚底的高度（看向身体中部，约 1.0）。")]
        [SerializeField] private float _focusHeight = 1.0f;

        [Header("机位")]
        [Tooltip("相机到焦点的距离。")]
        [SerializeField] private float _distance = 3f;
        [Tooltip("俯角（度）。越大越俯视。")]
        [SerializeField] private float _pitch = 40f;
        [SerializeField] private float _minPitch = 5f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("缩放")]
        [SerializeField] private float _minDistance = 1.5f;
        [SerializeField] private float _maxDistance = 10f;
        [Tooltip("每格滚轮改变的距离。")]
        [SerializeField] private float _zoomStep = 0.5f;

        [Header("手感")]
        [Tooltip("鼠标灵敏度（度/像素）。")]
        [SerializeField] private float _lookSensitivity = 0.35f;
        [Tooltip("位置平滑时间。设为 0 = 硬跟随，最不容易抖。")]
        [SerializeField] private float _smoothTime = 0f;

        [Header("朝向")]
        [Tooltip("⚠️ 慎用：勾上 = 相机水平角**跟随角色朝向**。\n" +
                 "但这与「移动相对相机」+「角色转向移动方向」构成**正反馈循环**，\n" +
                 "会导致按 WASD 时视角持续旋转（螺旋）。\n" +
                 "默认关闭：相机水平角由鼠标控制，角色转向移动方向 —— 这是稳定且标准的三方。\n" +
                 "只有在角色**不转向移动方向**时才建议开启。")]
        [SerializeField] private bool _followTargetYaw = false;

        [Tooltip("开局把相机的水平角对齐到角色当前朝向（满足「一开始和角色面朝一个方向」，但不会持续跟随）。")]
        [SerializeField] private bool _alignYawToTargetOnStart = true;

        [Header("鼠标指针")]
        [Tooltip("勾上 = 进入游戏即锁定指针（正式游玩手感）。取消 = 指针自由，便于在编辑器里调试。")]
        [SerializeField] private bool _lockCursorOnStart = false;

        private Vector3 _velocity;//SmoothDamp 用
        private float _yaw;//相机水平角（鼠标控制）
        private float _pitchOffset; //鼠标带来的俯角偏移
        private float _scrollDistance;//滚轮带来的距离变化

        /// <summary>当前水平朝向（供其它系统做「相对相机」换算）。</summary>
        public Quaternion PlanarRotation => Quaternion.Euler(0f, CurrentYaw(), 0f);

        private void Start()
        {
            if (_lockCursorOnStart) LockCursor(true);

            //开局把水平角对齐到角色朝向：满足「一开始和角色面朝一个方向」
            //（注意是**一次性对齐**，之后不再跟随 —— 持续跟随会导致 WASD 时视角旋转）
            if (_alignYawToTargetOnStart && _target != null)
                _yaw = _target.eulerAngles.y;

            //开局直接把相机摆到正确机位：
            //否则会从场景里存的旧位置「飞」过来，看起来就像「初始位置不对」。
            SnapToTarget();
        }

        private void Update()
        {
            if (InputService.Instance != null)
            {
                //⚠️ LookDelta 在 InputService 里已被 GameplayInputEnabled 闸住 ——
                //   游戏结束/暂停后这里拿到的是 zero，视角不会继续转。
                Vector2 look = InputService.Instance.LookDelta;
                _yaw += look.x * _lookSensitivity;

                //俯角偏移以「目标俯角」为基准浮动，避免基准被鼠标带跑
                _pitchOffset -= look.y * _lookSensitivity;
                _pitchOffset = Mathf.Clamp(_pitchOffset, _minPitch - _pitch, _maxPitch - _pitch);

                float scroll = InputService.Instance.ScrollDelta;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _scrollDistance -= scroll * _zoomStep;
                    _scrollDistance = Mathf.Clamp(_scrollDistance, _minDistance - _distance, _maxDistance - _distance);
                }
            }

            HandleCursor();
        }

        /// <summary>
        /// 鼠标指针管理。
        ///
        /// ⚠️ **结算 / 暂停后必须把指针还给 UI**，否则会出现两个问题：
        ///   1. 指针仍被锁住 → 结算面板的按钮**点不动**
        ///   2. ⚠️ 这里用的是旧版 `Input.GetMouseButtonDown`，**不受 timeScale 影响** ——
        ///      若不拦住，结算后点击会**把刚刚解锁的指针重新锁回去**
        /// </summary>
        private void HandleCursor()
        {
            bool gameplayActive = (InputService.Instance == null)
                               || InputService.Instance.GameplayInputEnabled;

            if (!gameplayActive)
            {
                //游戏结束/暂停：交出指针控制权，只解锁、不再锁回
                if (Cursor.lockState == CursorLockMode.Locked) LockCursor(false);
                return;
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
            Quaternion rot = CurrentRotation();
            Vector3 desired = focus - rot * Vector3.forward * CurrentDistance();

            if (_smoothTime <= 0f)
            {
                //硬跟随：直接赋值，没有插值残留 → 最稳、最不容易抖
                transform.position = desired;
                _velocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref _velocity,
                    _smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            transform.rotation = rot;
        }

        /// <summary>把相机瞬移到当前应该待的位置（开局调用，避免从场景里的旧位置飞过来）。</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            Vector3 focus = FocusPoint();
            Quaternion rot = CurrentRotation();

            transform.position = focus - rot * Vector3.forward * CurrentDistance();
            transform.rotation = rot;
            _velocity = Vector3.zero;
        }

        private Vector3 FocusPoint() => _target.position + Vector3.up * _focusHeight;

        private Quaternion CurrentRotation()
            => Quaternion.Euler(Mathf.Clamp(_pitch + _pitchOffset, _minPitch, _maxPitch), CurrentYaw(), 0f);

        private float CurrentYaw()
        {
            //⚠️ 默认走 _yaw（鼠标控制）—— 不跟随角色。
            //   跟随角色会与「移动相对相机 + 角色转向移动方向」构成正反馈 → WASD 时视角持续旋转。
            if (_followTargetYaw && _target != null) return _target.eulerAngles.y;
            return _yaw;
        }

        private float CurrentDistance()
            => Mathf.Clamp(_distance + _scrollDistance, _minDistance, _maxDistance);

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
