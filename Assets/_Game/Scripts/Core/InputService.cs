using UnityEngine;

namespace Game.Core
{
    /// <summary>输入的**唯一入口**。业务代码不要直接读 Input.*，一律走这里。</summary>
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }//唯一

        private PlayerControls _controls;//自动生成的输入封装

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
        }

        /// <summary>移动输入。超过单位长度时归一化（斜向不加速）。</summary>
        public Vector2 Move
        {
            get
            {
                Vector2 k = _controls.Player.Move.ReadValue<Vector2>();
                return k.sqrMagnitude > 1f ? k.normalized : k;
            }
        }

        public bool DashPressedThisFrame => _controls.Player.Dash.triggered;
        public bool AttackPressedThisFrame => _controls.Player.Attack.triggered;

        /// <summary>特殊攻击键（Q / 鼠标右键）。空手状态下由 PlayerFSM 重映射为"召回"。</summary>
        public bool SpecialPressedThisFrame => _controls.Player.Special.triggered;

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
