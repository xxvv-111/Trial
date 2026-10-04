using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 第三人称跟随相机：**俯角可仰视、始终看向角色**，鼠标控视角、滚轮缩放。
    ///
    /// 机位算法（简单且稳定）：
    ///   焦点 = 角色 + 上抬 <c>_focusHeight</c>（身体中部）
    ///   视线 = 俯角 × 水平角 <c>_yaw</c>
    ///   机位 = 焦点 **沿视线后退** <c>_distance</c>
    ///   → 因为机位是从焦点沿视线推出来的，**视线必然穿过角色**，
    ///     所以角色永远在画面中心，不会跑偏、不会出画。
    ///
    /// ⚠️ **地面安全下限（防穿地）**：相机不允许低于 <c>_groundY + _groundClearance</c>。
    ///    实现方式是**钳制俯角**而不是钳制机位高度 —— 因为钳制高度会压缩相机距离、
    ///    导致极端角度下相机贴近角色；钳制俯角则保持距离不变，且机位仍从焦点沿视线推出 →
    ///    **角色继续居中**（钳制高度后就必须改用 LookAt 才能居中）。
    ///
    ///    ⚠️ 该下限**随缩放距离变化**：`p_min = -asin((focusY - (groundY + clearance)) / distance)`。
    ///    距离拉得越远，允许的仰角越小（相机在更远处更容易沉到地面以下）。
    ///    所以**不要**用一个固定常数当安全下限 —— 拉远必穿地。
    ///
    /// ⚠️ 本版仍**不含防穿墙**：那处逻辑（每帧 SphereCast）会让机位在「近/远」之间跳变 → **相机抖动**。
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
        [Tooltip("基准俯角（度）。越大越俯视。鼠标在此基准上下偏移。")]
        [SerializeField] private float _pitch = 40f;
        [Tooltip("俯角下限（度）。**负值 = 允许仰视**（相机低于焦点往上看）。\n" +
                 "⚠️ 仅作「设计上限」；真正的物理下限由「地面安全角」自动收窄（见 _groundClearance）。")]
        [SerializeField] private float _minPitch = -60f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("地面钳制（防穿地）")]
        [Tooltip("地面高度。相机不会被允许低于「地面 + 离地余量」。")]
        [SerializeField] private float _groundY = 0f;

        [Tooltip("相机离地面的最小余量（米）。太小会让近裁剪面切进地面。")]
        [SerializeField] private float _groundClearance = 0.3f;

        [Tooltip("开局自动探测地面高度（从角色向下打射线，忽略角色自身）。\n" +
                 "探测失败则回落到 _groundY。若关卡有高低落差，建议关掉并手动指定。")]
        [SerializeField] private bool _autoDetectGroundY = true;

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
        private bool _wasGameplayActive = true;//检测「输入从关闭→开启」的上升沿，用于恢复指针锁定

        /// <summary>当前水平朝向（供其它系统做「相对相机」换算）。</summary>
        public Quaternion PlanarRotation => Quaternion.Euler(0f, CurrentYaw(), 0f);

        private void Start()
        {
            if (_lockCursorOnStart) LockCursor(true);

            ResolveGroundY();

            //开局把水平角对齐到角色朝向：满足「一开始和角色面朝一个方向」
            //（注意是**一次性对齐**，之后不再跟随 —— 持续跟随会导致 WASD 时视角旋转）
            if (_alignYawToTargetOnStart && _target != null)
                _yaw = _target.eulerAngles.y;

            //开局直接把相机摆到正确机位：
            //否则会从场景里存的旧位置「飞」过来，看起来就像「初始位置不对」。
            SnapToTarget();
        }

        /// <summary>
        /// 探测角色脚下的地面高度。
        /// ⚠️ 必须**忽略角色自身**的碰撞体 —— 否则射线打到自己的胶囊体，
        ///    会把"地面"当成角色脚底所在的层（实测踩过：射线命中 Player @ y=1.6）。
        /// </summary>
        private void ResolveGroundY()
        {
            if (!_autoDetectGroundY || _target == null) return;

            Vector3 origin = _target.position + Vector3.up * 2f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.transform.IsChildOf(_target)) continue;//跳过角色自身
                if (hits[i].distance < nearest) nearest = hits[i].distance;
            }

            if (!float.IsPositiveInfinity(nearest))
                _groundY = (origin + Vector3.down * nearest).y;
            else
                Debug.LogWarning("[CameraFollow] 向下探测地面失败，沿用 _groundY = " + _groundY + "。", this);
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
        /// ⚠️ **游戏性输入关闭（结算 / 暂停）时必须把指针还给 UI**，否则：
        ///   1. 指针仍被锁住 → 结算/暂停面板的按钮**点不动**
        ///   2. 这里用的是旧版 `Input.GetMouseButtonDown`，**不受 timeScale 影响** ——
        ///      若不拦住，点一下会**把刚解锁的指针重新锁回去**
        ///
        /// ⚠️ **ESC 已不在此处理** —— 交给 <see cref="UI.PauseMenu"/> 统一负责。
        ///    否则按 ESC 会同时"解锁指针"和"开关暂停菜单"，两套逻辑互相打架。
        /// </summary>
        private void HandleCursor()
        {
            bool gameplayActive = (InputService.Instance == null)
                               || InputService.Instance.GameplayInputEnabled;

            if (!gameplayActive)
            {
                //结算 / 暂停：交出指针控制权，只解锁、不再锁回
                if (Cursor.lockState == CursorLockMode.Locked) LockCursor(false);
                _wasGameplayActive = false;
                return;
            }

            //从「关闭」回到「开启」（例如暂停菜单点了继续）→ 重新锁定指针
            if (!_wasGameplayActive)
            {
                _wasGameplayActive = true;
                if (_lockCursorOnStart) LockCursor(true);
            }

            //点击游戏窗口 → 重新锁定（仅在开启锁定时生效）
            if (_lockCursorOnStart && Input.GetMouseButtonDown(0)
                && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 focus = FocusPoint();
            Quaternion rot = CurrentRotation();
            Vector3 desired = focus - rot * Vector3.forward * CurrentDistance();

            desired = ClampAboveGround(desired);

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

            //⚠️ 朝向仍用 rot（而非 LookAt）—— 因为机位是从焦点沿视线推出来的，
            //   视线必然穿过焦点，角色自动居中。仅在「安全网」兜底时才略有偏差。
            transform.rotation = rot;
        }

        /// <summary>
        /// 把相机瞬移到当前应该待的位置（开局调用，避免从场景里的旧位置飞过来）。
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            Vector3 focus = FocusPoint();
            Quaternion rot = CurrentRotation();

            transform.position = ClampAboveGround(focus - rot * Vector3.forward * CurrentDistance());
            transform.rotation = rot;
            _velocity = Vector3.zero;
        }

        /// <summary>相机允许的最低高度（地面 + 离地余量）。</summary>
        private float MinCameraY { get { return _groundY + _groundClearance; } }

        /// <summary>
        /// 安全网：机位若低于地面则抬到最低高度。
        ///
        /// ⚠️ 正常情况下**不会触发** —— 因为 <see cref="CurrentRotation"/> 已经把俯角
        ///    限制在「地面安全角」以内。这里只是兜底（例如角色被抬到很高的落差边缘、
        ///    或探测到的地面高度不准）。一旦触发，相机与焦点的距离会被压缩，
        ///    此时角色可能略微偏离画面中心。
        /// </summary>
        private Vector3 ClampAboveGround(Vector3 pos)
        {
            if (pos.y < MinCameraY) pos.y = MinCameraY;
            return pos;
        }

        private Vector3 FocusPoint() => _target.position + Vector3.up * _focusHeight;

        /// <summary>
        /// 实际俯角 = 鼠标值先受 <c>[_minPitch, _maxPitch]</c> 约束，
        /// 再受「地面安全角」约束（取其更保守者）。
        /// </summary>
        private float EffectivePitch()
        {
            float p = Mathf.Clamp(_pitch + _pitchOffset, _minPitch, _maxPitch);
            return Mathf.Max(p, GroundSafeMinPitch());
        }

        /// <summary>
        /// 「地面安全角」：在这个俯角下，相机正好落在最低允许高度上。
        ///
        /// 推导：机位高度 = focusY + sin(pitch) × distance ≥ groundY + clearance
        ///   → sin(pitch) ≥ (groundY + clearance − focusY) / distance
        ///   → pitch ≥ asin(...)   （结果为负 = 允许仰视）
        ///
        /// ⚠️ **该值随距离变化**：距离越远，允许的仰角越小。
        ///    所以安全下限不能用固定常数 —— 滚轮拉远后固定常数必穿地。
        /// </summary>
        private float GroundSafeMinPitch()
        {
            if (_target == null) return -90f;

            float d = Mathf.Max(0.05f, CurrentDistance());
            float focusY = _target.position.y + _focusHeight;

            float needSin = (MinCameraY - focusY) / d;
            return Mathf.Asin(Mathf.Clamp(needSin, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>相机朝向：由「有效俯角」+ 水平角构成（无翻滚）。</summary>
        private Quaternion CurrentRotation()
            => Quaternion.Euler(EffectivePitch(), CurrentYaw(), 0f);

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
