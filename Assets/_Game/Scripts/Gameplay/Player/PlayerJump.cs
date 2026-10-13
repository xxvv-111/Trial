using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 跳跃：结构照 <see cref="PlayerDash"/> —— 计时器驱动 + LateUpdate 里位移。
    /// （2026-10-13 新增，RPG 层的第一个动作。）
    ///
    /// ⭐ **逻辑与冲刺逐条对应**：
    /// <list type="table">
    ///   <item>冲刺：<c>_motor.enabled = false</c>，位移由本组件 LateUpdate 调
    ///         <see cref="PlayerMotor.StepMoveGrounded"/>（带贴地，因为是地面动作）；</item>
    ///   <item>跳跃：<c>_motor.enabled = false</c>，位移由本组件 LateUpdate 调
    ///         <see cref="PlayerMotor.StepMove"/>（**不贴地**——贴地下压会把人拽回地面，跳跃就废了）。</item>
    /// </list>
    ///
    /// ⚠️ **位移 100% 由代码驱动**（2026-10-12 根运动已删）：
    ///    动画 `P1/P2Kimodo_SOMA_jump` 自带的 RootT.y 起伏（+0.6 m）在关掉根运动后**不会生效**，
    ///    所以垂直轨迹由本类用正弦曲线补上 —— 峰值 = <c>jumpHeight</c>，时长 = <c>jumpTime</c>。
    ///
    /// 垂直位移走 `CharacterController`（真离地）而不是偏移骨骼根：
    ///    · 碰撞/判定盒/阴影都与视觉一致（骨骼根偏移是纯视觉的，阴影会露馅）；
    ///    · 将来接「重力 / 掉落」（PHASE1 的 RPG 层）时可以从这里平滑演进。
    ///
    /// ℹ️ 跳跃**没有无敌帧**（与冲刺的区别）：冲刺是闪避技、跳跃是移动技；
    ///    空中被击会正常进 Hit（<see cref="PlayerFSM"/> 的 <c>_exit[Jump]</c> 会把计时器清掉）。
    ///
    /// ⭐ **两段跳跃动画共用一个 <see cref="PlayerState.Jump"/> 状态**（2026-10-13）：
    /// <list type="bullet">
    ///   <item>静止 / 走路起跳 → <c>Kimodo_jump</c>（站跳），Animator trigger = <c>Jump</c>；</item>
    ///   <item>跑步起跳 → <c>P1Kimodo_run_jump</c>（跑跳），Animator trigger = <c>RunJump</c>。</item>
    /// </list>
    /// 挑哪一段由 <see cref="PlayerFSM"/> 读 <see cref="PlayerMotor.IsRunning"/> 决定 ——
    /// 本类**不关心**当前播的是哪段动画，只按 <see cref="BeginJump"/> 收到的速度算位移。
    /// </summary>
    public class PlayerJump : MonoBehaviour
    {
        /// <summary>起跳。（当前无订阅者，保留作扩展点 —— 例如接音效/尘土特效。）</summary>
        public event Action JumpStarted;

        [SerializeField] private PlayerConfig config;

        //配置副本
        private float jumpTime;
        private float jumpHeight;
        private float jumpAirSpeedMax;//空中水平速度的**上限**（配置而来；实际速度还要看起跳瞬间的移动档位）
        private float landRecoverTime;//落地后摇时长（秒）—— 触地后完全定身的时间

        //运行时状态
        private float _jumpTimer;//剩余**滞空**时间
        private float _yDone;//已完成的垂直位移（米）——「目标 − 已完成」差量用，保证轨迹精确

        /// <summary>
        /// 剩余**落地后摇**时间。触地（<see cref="_jumpTimer"/> 归零）时由 <see cref="landRecoverTime"/> 装载，
        /// 归零后跳跃才真正结束。⚠️ 这段时间角色**完全定身**，位移只保留贴地下压。
        /// </summary>
        private float _landRecoverTimer;

        /// <summary>
        /// 本次跳跃的**空中水平速度**（米/秒）。起跳瞬间锁定 = <c>min(起跳瞬间的移动速度, 上限)</c>。
        /// 0 = 原地垂直跳（静止起跳就是这种）。
        /// ⚠️ 与 <see cref="_airDir"/> 配套：速度为 0 时方向无所谓，速度为 0 之外时方向一定非零。
        /// </summary>
        private float _airSpeed;

        /// <summary>空中航向（水平方向，世界空间）。起跳瞬间锁定：有输入 = 输入方向，无输入 = 零（原地跳）。</summary>
        private Vector3 _airDir;

        private PlayerMotor _motor;//位移出口（它持有 CharacterController）

        /// <summary>是否**还在空中**（滞空段进行中）。落地后摇期间为 <c>false</c>。</summary>
        public bool IsJumping => _jumpTimer > 0f;

        /// <summary>是否处于**落地后摇**（已触地、但还没站定）。这期间角色完全定身。</summary>
        public bool IsLanding => _jumpTimer <= 0f && _landRecoverTimer > 0f;

        /// <summary>
        /// ⭐ 跳跃是否**尚未结束**（滞空 **或** 落地后摇）—— <see cref="PlayerFSM"/> 只认这一个。
        ///
        /// 为什么要单独一个属性：跳跃现在有**两段**，而对状态机来说两段是同一件事 ——
        /// 「跳起来这一整套动作还没做完，别放玩家走」。
        /// <list type="bullet">
        ///   <item><see cref="PlayerState.Jump"/> 的退出判据用本属性 ⇒ 后摇走完才回 Idle/Run；</item>
        ///   <item><see cref="PlayerFSM.TryJump"/> 的准入判据也用本属性 ⇒ **后摇期间不能再起跳**（否则后摇形同虚设）。</item>
        /// </list>
        /// ⚠️ 只能替代 <see cref="IsJumping"/> 用于上面两处；要判断"人在空中吗"仍须用 <see cref="IsJumping"/>。
        /// </summary>
        public bool Busy => _jumpTimer > 0f || _landRecoverTimer > 0f;

        private void Awake()
        {
            jumpTime = Mathf.Max(0.1f, config.jumpTime);
            jumpHeight = Mathf.Max(0f, config.jumpHeight);
            jumpAirSpeedMax = Mathf.Max(0f, config.jumpAirSpeed);
            landRecoverTime = Mathf.Max(0f, config.landRecoverTime);//⚠️ 允许 0 = 关掉后摇（退回旧行为）

            _motor = GetComponent<PlayerMotor>();
            if (_motor == null)
                Debug.LogWarning("[PlayerJump] 同物体上没有 PlayerMotor —— 跳跃位移无法执行（需要它来走 CharacterController）。", this);

            _jumpTimer = 0f;
            _landRecoverTimer = 0f;
        }

        private void LateUpdate()
        {
            if (!Busy) return;
            if (_motor == null) return;

            // ==================== 落地后摇：只贴地，绝不走位 ====================
            //⭐ 这一段对应动画的「落地缓冲 → 起身站直」，角色必须钉在原地才不显得滑。
            //   ⚠️ 此时宿主 PlayerMotor 是 **disabled** 的 —— 它唯一做贴地下压的地方（自己的 Update）不跑，
            //      所以这里必须显式补一次，否则后摇期间角色不会贴合地面（斜坡 / 台阶边缘上会浮起来）。
            if (_jumpTimer <= 0f)
            {
                _motor.StepStickToGround();
                return;
            }

            //已过时间（0 → jumpTime）
            float elapsed = jumpTime - _jumpTimer;
            float tn = Mathf.Clamp01(elapsed / jumpTime);

            // ---------------- Y 轴：正弦轨迹（起终为 0，峰值 = jumpHeight） ----------------
            //⭐ 「目标 − 已完成」差量（与 PlayerMotor.ComputeCodeLunge 同一口径）：
            //   任一瞬间的离地高度都精确等于曲线读数，不受帧率影响。
            float yTarget = jumpHeight * Mathf.Sin(Mathf.PI * tn);
            float yDelta = yTarget - _yDone;
            _yDone = yTarget;

            // ---------------- 水平：沿起跳时锁定的航向匀速移动 ----------------
            //⭐ 速度 = 起跳瞬间锁定的 `_airSpeed`（静止起跳为 0 → 人只在原地上下，不横移）。
            Vector3 move = _airDir * (_airSpeed * Time.deltaTime);
            move.y = yDelta;

            //⚠️ 走 CharacterController（经 PlayerMotor），**不要**改回 transform.position。
            //   ⚠️ 用 StepMove（不贴地）—— 跳跃期间贴地下压必须停，否则人上不去。
            var flags = _motor.StepMove(move);

            // ---------------- 落地提前结束 ----------------
            //下落段（过峰值）若 CharacterController 报告撞到地面 → 视为落地。
            //正常情况曲线终点回到 0、计时器自然到期；这一道是「跳过矮障碍物 / 跳到低处提前着地」的兜底。
            //⚠️ 提前落地也要进**落地后摇**（不是直接结束）—— 否则从矮台上跳下来会"啪"地就能跑，
            //   恰好是玩家最容易察觉"没有落地动作"的场景。
            if (tn > 0.5f && (flags & CollisionFlags.Below) != 0)
                EnterLandRecover();
        }

        /// <summary>
        /// 触地：结束滞空段、进入**落地后摇**。**幂等** —— 重复调用不会把后摇时间叠起来。
        ///
        /// ⚠️ 后摇结束后跳跃才算真正结束（<see cref="Busy"/> 变 false，状态机才放行）。
        /// ℹ️ <see cref="landRecoverTime"/> 配成 0 时，本方法等于"直接结束"，即退回旧行为。
        /// </summary>
        private void EnterLandRecover()
        {
            _jumpTimer = 0f;
            _yDone = 0f;//垂直位移的差量账目结清，避免下次起跳带着上一次的余量
            _landRecoverTimer = landRecoverTime;
        }

        /// <summary>
        /// 只推进两个计时器（位移在 <see cref="LateUpdate"/> 里按计时器算）。
        /// <list type="bullet">
        ///   <item>滞空计时走完 → <see cref="EnterLandRecover"/>（触地，开始后摇）；</item>
        ///   <item>落地后摇计时走完 → <see cref="Busy"/> 变 false → 状态机下一帧放行。</item>
        /// </list>
        /// </summary>
        private void Update()
        {
            if (_jumpTimer > 0f)
            {
                _jumpTimer -= Time.deltaTime;
                //⭐ 滞空走完 = 正弦位移已回到 0（触地）⇒ 转进落地后摇，而不是直接结束。
                if (_jumpTimer <= 0f) EnterLandRecover();
            }
            else if (_landRecoverTimer > 0f)
            {
                _landRecoverTimer -= Time.deltaTime;
                if (_landRecoverTimer < 0f) _landRecoverTimer = 0f;
            }
        }

        /// <summary>
        /// 由 PlayerFSM 在进入 Jump 状态时调用。
        ///
        /// ⭐ 两件事在这里**一次锁定**，起飞后不再改变：
        /// <list type="number">
        ///   <item><b>水平速度</b> = <paramref name="airSpeed"/>（上层传的是**起跳瞬间的移动速度**，
        ///         见 <see cref="PlayerMotor.CurrentSpeed"/>），再钳进配置上限。
        ///         <b>传 0 = 原地垂直跳</b>（静止起跳就是这个）。</item>
        ///   <item><b>空中航向</b> = 起跳瞬间的**角色正面**（<c>transform.forward</c>，去掉俯仰分量）。
        ///         仅在水平速度 &gt; 0 时取；静止起跳 = 原地上下。</item>
        /// </list>
        ///
        /// ⚠️ 2026-10-13 由无参改成带 <paramref name="airSpeed"/>：
        ///    原来水平速度一律取配置的固定值，导致"原地跳也会往前飘"。现在速度跟着档位走
        ///    ——静止跳不位移、走路跳按走速、跑步跳按跑速（还能被配置上限截短）。
        /// ⚠️ 同日第二轮：航向由"输入方向"改成"**角色正面**"，与 <see cref="PlayerMotor"/> 的位移口径统一。
        /// </summary>
        /// <param name="airSpeed">起跳瞬间的水平速度（米/秒）。静止起跳传 0；自动钳到 [0, config.jumpAirSpeed]。</param>
        public void BeginJump(float airSpeed)
        {
            _jumpTimer = jumpTime;
            _yDone = 0f;
            _airSpeed = Mathf.Clamp(airSpeed, 0f, jumpAirSpeedMax);
            _landRecoverTimer = 0f;//新一次跳跃：清掉可能残留的后摇（正常情况下已经走完了）

            //⭐⭐ 空中航向 = **起跳瞬间的角色正面**。（2026-10-13 改；原来是"输入方向"）
            //
            //   为什么必须跟着改：走路 / 跑步的位移方向已统一为"朝角色正面"（见 PlayerMotor.Update），
            //   跳跃若还沿用"输入方向"，转身途中起跳就会朝输入方向飞、而角色面朝另一边 ——
            //   等于把倒滑的观感原样搬到了空中。
            //
            //   ⚠️ 起跳这一帧 PlayerFSM 已经把 `_motor.enabled = false`，朝向**已经定格**，
            //      所以这里读到的 forward 就是本帧转完之后的结果，与走路时的位移方向同源。
            //   ℹ️ 静止起跳（`_airSpeed` = 0）用不到方向，留 zero 即可。
            _airDir = Vector3.zero;
            if (_airSpeed > 0.01f)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;//去掉俯仰：角色在坡上有倾角，不做水平投影会让空中航向偏斜
                _airDir = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.zero;
            }

            JumpStarted?.Invoke();
        }

        /// <summary>由 PlayerFSM 在退出 Jump 状态时调用（正常落地 / 被打断都走这里）。</summary>
        public void EndJump()
        {
            _jumpTimer = 0f;
            _yDone = 0f;
            _airSpeed = 0f;
            _landRecoverTimer = 0f;//⚠️ 被受击 / 死亡打断时也要清 —— 残留后摇会让"下一次跳跃"开局就被定身
        }
    }
}
