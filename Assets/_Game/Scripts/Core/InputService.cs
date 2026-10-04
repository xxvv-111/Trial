using UnityEngine;

namespace Game.Core
{
    /// <summary>输入的**唯一入口**。业务代码不要直接读 Input.*，一律走这里。</summary>
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }//唯一

        private PlayerControls _controls;//自动生成的输入封装

        /// <summary>
        /// 游戏性输入是否可用。结算 / 暂停时置 false。
        ///
        /// ⚠️ **为什么必须有这道闸**：<c>Time.timeScale = 0</c> 拦不住输入 ——
        ///    · `Update()` 在 timeScale = 0 时**仍然每帧执行**（只有 `FixedUpdate` 与 `deltaTime` 会停）
        ///    · Input System 的鼠标/按键**完全不受 timeScale 影响**，ReadValue 照常给值
        ///    所以会出现「游戏已结束，但鼠标仍在转视角 / 角色仍能走」的现象。
        ///
        /// 这里闸住 **Player** action map（UI map 保持可用，否则暂停菜单点不动）。
        /// </summary>
        public bool GameplayInputEnabled { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)//已有实例 → 自己是多余的
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _controls = new PlayerControls();
            _controls.Enable();
            GameplayInputEnabled = true;
        }

        /// <summary>
        /// 开关游戏性输入。结算弹出、暂停菜单打开时传 false。
        /// UI 输入不受影响（它在另一个 action map 里）。
        /// </summary>
        public void SetGameplayInputEnabled(bool on)
        {
            GameplayInputEnabled = on;
            if (_controls == null) return;

            if (on) _controls.Player.Enable();
            else _controls.Player.Disable();
        }

        /// <summary>移动输入。超过单位长度时归一化（斜向不加速）。</summary>
        public Vector2 Move
        {
            get
            {
                if (!GameplayInputEnabled) return Vector2.zero;

                Vector2 k = _controls.Player.Move.ReadValue<Vector2>();
                return k.sqrMagnitude > 1f ? k.normalized : k;
            }
        }

        /// <summary>视角输入（鼠标移动 / 右摇杆）。由相机读取，用于旋转视角。</summary>
        public Vector2 LookDelta
        {
            get { return GameplayInputEnabled ? _controls.Player.Look.ReadValue<Vector2>() : Vector2.zero; }
        }

        /// <summary>滚轮输入（y 分量用于缩放）。</summary>
        public float ScrollDelta
        {
            get { return GameplayInputEnabled ? _controls.Player.Scroll.ReadValue<Vector2>().y : 0f; }
        }

        public bool DashPressedThisFrame
        {
            get { return GameplayInputEnabled && _controls.Player.Dash.triggered; }
        }

        public bool AttackPressedThisFrame
        {
            get { return GameplayInputEnabled && _controls.Player.Attack.triggered; }
        }

        /// <summary>特殊攻击键（Q / 鼠标右键）。空手状态下由 PlayerFSM 重映射为"召回"。</summary>
        public bool SpecialPressedThisFrame
        {
            get { return GameplayInputEnabled && _controls.Player.Special.triggered; }
        }

        private void OnDestroy()
        {
            if (_controls != null)
            {
                _controls.Disable();//先关掉 action map
                _controls.Dispose();//再销毁 asset
            }
            if (Instance == this) Instance = null;
        }
    }
}
