using UnityEngine;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 弹出式 UI 面板的基类：负责"打开 / 关闭"，以及**打开时必须做的两件事**。
    ///
    /// ⚠️⚠️ **两道闸必须同时上**（只做一道会留下 bug，本项目踩过）：
    ///   1. `Time.timeScale = 0` —— 让动画与逻辑停下
    ///   2. `InputService.SetGameplayInputEnabled(false)` —— 让**鼠标不能再转视角**
    ///   为什么第 2 道必需：`timeScale = 0` **拦不住输入** ——
    ///   `Update()` 在 timeScale = 0 时仍每帧执行，且 Input System 完全不受 timeScale 影响。
    ///   详见 <see cref="InputService"/> 的类注释。
    ///
    /// 挂法：挂在面板根节点上（<c>_panel</c> 留空 = 用自身），
    /// 并**在场景里把该节点预先设为 inactive**（避免运行时闪一下）。
    /// 然后由 <see cref="Gameplay.Interactable"/> 的 onInteract 调用 <see cref="Open"/>。
    ///
    /// ⚠️ **指针不用本类操心**：关掉游戏性输入后，<see cref="Gameplay.CameraFollow"/>
    ///    会自动解锁指针（否则按钮点不动）；关闭恢复时它会重新锁定。
    /// </summary>
    public class UIWindow : MonoBehaviour
    {
        [Tooltip("面板根节点。留空 = 本脚本所在物体。")]
        [SerializeField] protected GameObject _panel;

        [Tooltip("按 ESC 是否关闭本面板。")]
        [SerializeField] private bool _closeOnEscape = true;

        /// <summary>本面板当前是否打开。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 当前处于打开状态的面板数量。
        ///
        /// ⚠️ **留给 <c>PauseMenu</c> 避让 ESC 用**：ESC 现在同时被
        ///    `PauseMenu`（内部用 `Input.GetKeyDown(KeyCode.Escape)`）和本类监听。
        ///    酒馆里暂时没有 PauseMenu 所以不冲突；**将来把 PauseMenu 放进酒馆时**，
        ///    请在它的 <c>Update</c> 开头加一行：
        ///    <c>if (UIWindow.OpenCount &gt; 0) return;</c>
        ///    否则按一次 ESC 会**同时**关面板 + 开暂停菜单。
        /// </summary>
        public static int OpenCount { get; private set; }

        private float _prevTimeScale = 1f;

        /// <summary>面板根节点。未指定时 = 本脚本所在物体。</summary>
        protected GameObject Panel { get { return _panel != null ? _panel : gameObject; } }

        /// <summary>
        /// 留给子类的"启用"钩子。
        ///
        /// ⚠️ 之所以要在基类里**显式声明**：Unity 的 <c>OnEnable</c> 是消息方法而不是 virtual 方法，
        ///    子类直接写 <c>override void OnEnable()</c> 会报 **CS0115**（没有找到适合的方法来重写）。
        ///    基类声明成 virtual 之后子类才能 override ——
        ///    这对"面板预设为 inactive、第一次打开时才初始化内容"的场景是必需的
        ///    （inactive 物体**不会执行 Start**）。
        /// </summary>
        protected virtual void OnEnable() { }

        protected virtual void Start()
        {
            // ⚠️ 本方法只负责"保证初始是关的"。若你已在场景里把面板设为 inactive，
            //    本方法根本不会执行（inactive 物体不跑 Start）—— 行为一致，不影响功能。
            if (!IsOpen) Panel.SetActive(false);
        }

        protected virtual void Update()
        {
            if (!IsOpen || !_closeOnEscape) return;

            InputService input = InputService.Instance;
            if (input == null) return;

            //⚠️ 用 InputService 的暂停键判断：它**刻意不受 GameplayInputEnabled 影响**，
            //   否则"面板打开时游戏性输入已被关掉"，ESC 就关不掉面板了。
            if (input.PausePressedThisFrame) Close();
        }

        /// <summary>打开面板。重复调用无副作用。挂到 <c>Interactable.onInteract</c> 上。</summary>
        public virtual void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            OpenCount++;

            Panel.SetActive(true);

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;//闸 1：冻结时间

            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(false);//闸 2：锁输入
        }

        /// <summary>关闭面板，并还原时间与输入。挂到面板的"关闭"按钮上。</summary>
        public virtual void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            OpenCount = Mathf.Max(0, OpenCount - 1);

            Panel.SetActive(false);

            //还原打开前的 timeScale（若异常则回落到 1）
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;

            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(true);
        }
    }
}
