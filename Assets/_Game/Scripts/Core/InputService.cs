using UnityEngine;

namespace Game.Core
{
    /// <summary>输入的**唯一入口**。业务代码不要直接读 Input.*，一律走这里。</summary>
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }//唯一

        private PlayerControls _controls;//自动生成的输入封装

        [Tooltip("⭐ 输入缓冲窗口（秒）：动作尚未就绪时按下的键会被缓存这么久，等能执行的那一刻立刻补执行。\n" +
                 "作用：冲刺 / 连段的后摇阶段按下也生效 —— 玩家不需要「等动作播完再按」。\n" +
                 "0 = 关闭缓冲（退回旧行为：只认当帧按下的键）。参考区间 0.10 ~ 0.20。")]
        [SerializeField] private float _inputBufferTime = 0.15f;

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
            if (!on) ClearBuffers();//⚠️ 关闸时清缓冲：否则「暂停期间按的键」会在恢复的瞬间被补执行
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

        /// <summary>
        /// 暂停键（ESC）本帧是否按下。
        ///
        /// ⚠️ **刻意不受 <see cref="GameplayInputEnabled"/> 影响** ——
        ///    暂停菜单必须能在"游戏性输入已关闭"的状态下被打开与关闭，
        ///    若走 Player action map 则会被闸门一起关掉，导致**暂停后按 ESC 关不掉菜单**。
        ///
        /// 实现上直接用旧版 `Input.GetKeyDown`（项目 Active Input Handling = Both，可用），
        /// 好处是零配置：不必往 `.inputactions` 里加会跟 UI 的 Cancel 撞车的映射。
        /// 仍然留在本类里，保持"输入只从 InputService 出去"的约定。
        /// </summary>
        public bool PausePressedThisFrame
        {
            get { return Input.GetKeyDown(KeyCode.Escape); }
        }

        // ==================== 输入缓冲（2026-10-07 新增） ====================
        //
        // 为什么需要：上面的 `*PressedThisFrame` 是**边沿读取**，只在按下的那一帧为 true。
        //   如果那一刻角色正处于后摇 / 硬直等「暂时不能执行」的状态，这次按键就被**彻底丢弃** ——
        //   玩家感受就是「我明明按了却没反应」。缓冲把按键记成「最近 N 秒内按过」，
        //   由状态机在**能执行的时候**询问它、并在成功后消费掉。
        //
        // ⚠️ 用 `Time.unscaledTime` 而不是 `Time.time`：顿帧（HitStop 把 timeScale 压到 0）期间
        //    `Time.time` 不再前进，会让缓冲「永不过期」。
        // ⚠️ **先问后消费**：调用方必须 `if (HasBufferedX && TryX()) ConsumeX();`
        //    —— 执行失败（例如连段窗口还没开）时**不要**消费，让按键继续留在缓冲里等下一次机会。

        private float _attackAt = float.NegativeInfinity;
        private float _dashAt = float.NegativeInfinity;
        private float _specialAt = float.NegativeInfinity;

        private void Update()
        {
            //⚠️ `_controls` 判空是必需的，不是防御性冗余：
            //   ① `Awake` 里发现"自己不是唯一实例"时会 `Destroy` 并提前 return，此时 `_controls` 仍是 null，
            //      而本方法在销毁生效前**还会跑一帧**；
            //   ② 退出 Play / 卸载场景时 `OnDestroy` 已把 `_controls` 清空（见那里）。
            if (!GameplayInputEnabled || _controls == null) return;

            if (_controls.Player.Attack.triggered) _attackAt = Time.unscaledTime;
            if (_controls.Player.Dash.triggered) _dashAt = Time.unscaledTime;
            if (_controls.Player.Special.triggered) _specialAt = Time.unscaledTime;
        }

        /// <summary>缓冲里是否有「尚未被消费」的攻击输入。**只读，不消费**。</summary>
        public bool HasBufferedAttack { get { return Buffered(_attackAt); } }

        /// <summary>缓冲里是否有尚未被消费的冲刺输入。只读。</summary>
        public bool HasBufferedDash { get { return Buffered(_dashAt); } }

        /// <summary>缓冲里是否有尚未被消费的特殊攻击输入。只读。</summary>
        public bool HasBufferedSpecial { get { return Buffered(_specialAt); } }

        /// <summary>消费掉缓冲的攻击输入。**只在动作真的执行成功后调用**。</summary>
        public void ConsumeAttack() { _attackAt = float.NegativeInfinity; }

        /// <summary>消费掉缓冲的冲刺输入。</summary>
        public void ConsumeDash() { _dashAt = float.NegativeInfinity; }

        /// <summary>消费掉缓冲的特殊攻击输入。</summary>
        public void ConsumeSpecial() { _specialAt = float.NegativeInfinity; }

        private bool Buffered(float at)
        {
            if (!GameplayInputEnabled) return false;
            return Time.unscaledTime - at <= _inputBufferTime;
        }

        /// <summary>清空全部缓冲（关闸时调用，避免「暂停期间按的键」在恢复瞬间触发）。</summary>
        public void ClearBuffers()
        {
            _attackAt = float.NegativeInfinity;
            _dashAt = float.NegativeInfinity;
            _specialAt = float.NegativeInfinity;
        }

        private void OnDestroy()
        {
            if (_controls != null)
            {
                _controls.Disable();//先关掉 action map
                _controls.Dispose();//再销毁 asset
                _controls = null;   //⭐ 清空引用 —— 让 `Update` 的判空能挡住"销毁之后还跑一帧"
            }
            if (Instance == this) Instance = null;
        }
    }
}
