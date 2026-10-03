using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Game.Core
{
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }//唯一

        private PlayerControls _controls;//输入控制

        //移动端
        //private Vector2 _virtualMove;
        //public void SetVirtualMove(Vector2 v)
        //{
        //    _virtualMove = v;
        //}

        private void Awake()
        {
            if(Instance != null && Instance != this)//是本身
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            _controls = new PlayerControls();
            _controls.Enable();
        }

        public Vector2 Move
        {
            get
            {
                Vector2 k=_controls.Player.Move.ReadValue< Vector2 > ();
                return k.sqrMagnitude>1f ? k.normalized : k;
            }
        }

        public bool DashPressedThisFrame => _controls.Player.Dash.triggered;
        public bool AttackPressedThisFrame => _controls.Player.Attack.triggered;

        private void OnDestroy()
        {
            if (_controls != null)
            {
                _controls.Disable();//先关掉action map
                _controls.Dispose();//再销毁 asset
            }
            if (Instance == this) Instance = null;
        }
    }
}