using System;
using System.Collections.Generic;

namespace Game.Gameplay
{
    /// <summary>
    /// 状态机的**非泛型外壳**。存在的唯一理由：持有它的
    /// <see cref="EnemyAIController"/> 必须是**非泛型 MonoBehaviour**
    /// —— Unity 的序列化不支持泛型组件，泛型基类里的 <c>[SerializeField]</c> 字段有丢引用的风险。
    /// 所以：泛型的部分全部放进纯 C# 类，MonoBehaviour 侧保持"无聊但安全"。
    /// </summary>
    public abstract class EnemyFsmBase
    {
        public abstract bool IsRunning { get; }
        public abstract bool IsLocked { get; }
        public abstract string CurrentStateName { get; }

        /// <summary>勾上后每次状态切换都经 <see cref="Logger"/> 打印（排查「敌人为什么不动」时很有用）。</summary>
        public bool LogTransitions { get; set; }

        /// <summary>日志出口，由控制器接到 UnityEngine.Debug（本文件不引用 UnityEngine）。</summary>
        public Action<string> Logger { get; set; }

        /// <summary>每帧派发（由控制器调用）。</summary>
        public abstract void Tick();

        /// <summary>锁死状态机（死亡后不再接受切换）。</summary>
        public abstract void LockStateMachine();
    }

    /// <summary>
    /// 敌人状态机（M3.2）—— 与 <see cref="PlayerFSM"/> 同风格的**三字典表驱动**实现。
    ///
    /// 设计意图（GDD §6.4）：两种敌人（剑兵 / 弓兵）共用一套骨架，
    /// **差异只在「状态集合 + 转移条件 + 数值」** —— 加第三种敌人（如盾兵）几乎是纯配置工作。
    ///
    /// ⚠️ 本类是**纯 C#** 类，刻意不继承 MonoBehaviour、不引用 UnityEngine：
    /// <list type="bullet">
    ///   <item>绕开「泛型 MonoBehaviour 不被 Unity 序列化支持」的坑</item>
    ///   <item>可以在**没有 Unity 的环境里跑自测**（见 <c>EnemyStateMachineSelfTest</c>），
    ///         状态迁移这种纯逻辑不该只能靠实机试出来</item>
    /// </list>
    ///
    /// ⚠️ 本工程 csproj 的 <c>LangVersion = 9.0</c>，别用文件作用域命名空间 / record。
    /// </summary>
    /// <typeparam name="TState">该敌人的状态枚举，例如 <c>EnemyMeleeAI.EState</c>。</typeparam>
    public sealed class EnemyStateMachine<TState> : EnemyFsmBase where TState : struct, Enum
    {
        //三张表：进入 / 每帧 / 离开（与 PlayerFSM 的 _enter / _update / _exit 一一对应）
        private readonly Dictionary<TState, Action> _enter = new Dictionary<TState, Action>();
        private readonly Dictionary<TState, Action> _update = new Dictionary<TState, Action>();
        private readonly Dictionary<TState, Action> _exit = new Dictionary<TState, Action>();

        private readonly string _ownerName;
        private bool _running;
        private bool _locked;

        /// <param name="ownerName">打印日志时用的名字，通常传敌人名字。</param>
        public EnemyStateMachine(string ownerName = null)
        {
            _ownerName = string.IsNullOrEmpty(ownerName) ? "Enemy" : ownerName;
        }

        /// <summary>当前状态。</summary>
        public TState State { get; private set; }

        /// <summary>上一个状态（首次进入初始状态时等于初始状态）。</summary>
        public TState PreviousState { get; private set; }

        public override bool IsRunning { get { return _running; } }

        /// <summary>是否已锁死（死亡后为 true）。控制器用它判断「已被打死，不再做受击反应」。</summary>
        public override bool IsLocked { get { return _locked; } }

        public override string CurrentStateName { get { return State.ToString(); } }

        // ==================== 建表 ====================

        /// <summary>
        /// 注册一个状态。**只传用得到的表项**即可，没传的留空
        /// （例如 Death 通常只需要一个 enter）。
        /// ⚠️ 必须在 <see cref="Start"/> 之前调用。
        /// </summary>
        public void Register(TState state, Action enter = null, Action update = null, Action exit = null)
        {
            if (enter != null) _enter[state] = enter;
            if (update != null) _update[state] = update;
            if (exit != null) _exit[state] = exit;
        }

        /// <summary>某个状态是否注册过 update 表项（自测 / 调试用）。</summary>
        public bool HasUpdate(TState state)
        {
            return _update.ContainsKey(state);
        }

        // ==================== 迁移 ====================

        /// <summary>
        /// 进入首个状态并启动派发。
        /// ⚠️ 与 <see cref="Change"/> 不同，**起始状态的 enter 表项会被执行** ——
        ///    <see cref="Change"/> 遇到同状态会提前返回，走它反而会让起始状态的 enter 永远不执行。
        /// </summary>
        public void Start(TState initial)
        {
            State = initial;
            PreviousState = initial;
            _running = true;

            Log("起始状态 " + initial);

            _enter.GetValueOrDefault(State)?.Invoke();
        }

        /// <summary>每帧派发。控制器每帧调一次。</summary>
        public override void Tick()
        {
            if (!_running) return;
            _update.GetValueOrDefault(State)?.Invoke();
        }

        /// <summary>切换状态（同状态直接忽略；未启动或已锁死时忽略一切）。</summary>
        public void Change(TState next)
        {
            if (!_running) return;
            if (_locked) return;
            if (Is(next)) return;

            _exit.GetValueOrDefault(State)?.Invoke();

            PreviousState = State;
            State = next;

            Log(PreviousState + " → " + State);

            _enter.GetValueOrDefault(State)?.Invoke();
        }

        /// <summary>死状态专用：锁死后 <see cref="Change"/> 一律失效。</summary>
        public override void LockStateMachine()
        {
            _locked = true;
        }

        /// <summary>
        /// 当前是否处于某个状态。
        /// ⚠️ 泛型枚举不能用 <c>==</c> 比较，必须走 EqualityComparer
        ///    （内部按整型比较，没有装箱开销）。
        /// </summary>
        public bool Is(TState state)
        {
            return EqualityComparer<TState>.Default.Equals(State, state);
        }

        private void Log(string message)
        {
            if (!LogTransitions) return;
            if (Logger == null) return;

            Logger("[" + _ownerName + "] " + message);
        }
    }
}
