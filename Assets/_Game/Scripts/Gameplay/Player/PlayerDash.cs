using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 冲刺：位移 + 无敌帧，是主要的闪避手段（GDD §4.4）。
    /// ⚠️ 2026-10-06 起**冲刺不消耗任何资源**（原体力消耗已删除）；
    /// 本类只负责位移与无敌计时。
    ///
    /// ⚠️ **位移必须经 <see cref="PlayerMotor"/>**（内部走 `CharacterController.Move`）。
    /// 2026-10-06 修的 bug：原实现在这里直接写 `transform.position +=`，
    /// **绕过了 CharacterController 的碰撞检测 → 冲刺能穿墙**。
    /// 现在改成"只有一个位置写入者"，见 `PlayerMotor.StepMoveGrounded`。
    /// </summary>
    public class PlayerDash : MonoBehaviour
    {
        /// <summary>冲刺开始。（当前无订阅者，保留作扩展点）</summary>
        public event Action DashStarted;

        [SerializeField] private PlayerConfig config;

        //配置副本
        private float dashSpeed;
        private float dashDelay;
        private float dashDuration;
        private float iFrameDuration;

        //运行时计时
        private float _dashTimer;//剩余冲刺时间
        private float _iFrameTimer;//剩余无敌时间

        private PlayerMotor _motor;//位移出口（它持有 CharacterController）

        public bool IsDashing => _dashTimer > 0f;
        public bool IsInvulnerable => _iFrameTimer > 0f;

        private void Awake()
        {
            dashSpeed = config.dashSpeed;
            dashDelay = config.dashDelay;
            dashDuration = config.dashTimer;
            iFrameDuration = config.iFrameTime;

            _motor = GetComponent<PlayerMotor>();
            if (_motor == null)
                Debug.LogWarning("[PlayerDash] 同物体上没有 PlayerMotor —— 冲刺位移无法执行（需要它来走 CharacterController）。", this);

            //⚠️ 这里必须初始化为 0。原实现把无敌计时直接初始化成配置值（0.5s），
            //   而 Update 每帧递减 → 开局会白送玩家 0.5 秒无敌。
            _dashTimer = 0f;
            _iFrameTimer = 0f;
        }

        private void Update()
        {
            if (_dashTimer > 0f) _dashTimer -= Time.deltaTime;
            if (_iFrameTimer > 0f) _iFrameTimer -= Time.deltaTime;
        }

        private void LateUpdate()
        {
            if (!IsDashing) return;

            //起手 dashDelay 之后才开始位移（保留原有的"起手停顿"手感）
            if (_dashTimer >= (dashDuration - dashDelay)) return;
            if (_motor == null) return;

            //⚠️ 走 CharacterController（经 PlayerMotor），**不要**改回 transform.position ——
            //   直接写坐标会绕过碰撞体，冲刺又会穿墙。
            _motor.StepMoveGrounded(transform.forward * (dashSpeed * Time.deltaTime));
        }

        /// <summary>由 PlayerFSM 在进入 Dash 状态时调用。</summary>
        public void BeginDash()
        {
            _dashTimer = dashDuration;
            _iFrameTimer = iFrameDuration;
            DashStarted?.Invoke();
        }

        /// <summary>由 PlayerFSM 在退出 Dash 状态时调用。</summary>
        public void EndDash()
        {
            _dashTimer = 0f;
            _iFrameTimer = 0f;
        }
    }
}
