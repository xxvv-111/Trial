using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 顿帧（hitstop / hitlag）—— 命中瞬间把游戏「钉住」几十毫秒。
    ///
    /// **为什么它是打击感的核心**：命中那一帧画面完全静止，玩家的注意力被强行拉到命中点上；
    /// 再叠加震屏 / 音效 / 受击动作，就构成「打在实体上」的触感。缺了它，攻击会像划过空气。
    ///
    /// 实现：把 <c>Time.timeScale</c> 压到 0，过一小段**真实时间**后恢复。
    ///   · 冻结的是**所有** deltaTime 驱动的逻辑（动画 / 位移 / AI 计时）—— 这正是我们要的效果。
    ///   · ⚠️ 超时判断必须用 `Time.unscaledTime`：`timeScale = 0` 时 `Time.time` **不再前进**，
    ///     用它当计时器会**永远恢复不了**（项目里 GameManager / PauseMenu 也踩过同一类坑）。
    ///   · ⚠️ 顿帧期间 `Update` 仍每帧执行（本项目已多次依赖这条事实）。
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Tooltip("总开关。关掉 = 完全不顿帧（便于 A/B 对比手感）。")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("普通命中的顿帧时长（秒，真实时间）。动作游戏常用 0.04 ~ 0.08。")]
        [SerializeField] private float _normalDuration = 0.06f;

        [Tooltip("重击的顿帧时长（伤害 ≥ 下一项阈值时用）。连段末段 / 火球这类应该更「重」。")]
        [SerializeField] private float _heavyDuration = 0.09f;

        [Tooltip("判定为「重击」的伤害阈值。")]
        [SerializeField] private int _heavyDamageThreshold = 15;

        /// <summary>恢复时间点（unscaled 时间）。&lt; 0 表示当前没有顿帧。</summary>
        private float _resumeAt = -1f;

        /// <summary>冻结前的 timeScale —— 恢复时还原它，而不是硬写 1。</summary>
        private float _frozenFrom = 1f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            //⚠️ 防御：若在顿帧中途销毁（退出 Play / 切换场景），必须还原 timeScale，否则游戏会永久卡死
            if (_resumeAt >= 0f) Time.timeScale = _frozenFrom > 0f ? _frozenFrom : 1f;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 请求一次顿帧。多次请求会**叠加取最晚结束时刻** —— 连段连续命中时不会互相把对方的顿帧截断。
        /// </summary>
        public void Request(float duration)
        {
            if (!_enabled || duration <= 0f) return;

            //别人冻的（暂停菜单 / 结算）→ 不插手，否则会把自己的「恢复值」写坏
            if (Time.timeScale <= 0f && _resumeAt < 0f) return;

            if (_resumeAt < 0f) _frozenFrom = Time.timeScale;//只在「由我们开始冻」的那一刻记录

            float until = Time.unscaledTime + duration;
            if (until > _resumeAt) _resumeAt = until;//⭐ 取 max：上一发还没结束又来一发就延长

            Time.timeScale = 0f;
        }

        /// <summary>按伤害量自动选时长（给 <see cref="HitboxController"/> 用的便捷入口）。</summary>
        public void RequestForDamage(int damage)
        {
            Request(damage >= _heavyDamageThreshold ? _heavyDuration : _normalDuration);
        }

        private void Update()
        {
            if (_resumeAt < 0f) return;
            if (Time.unscaledTime < _resumeAt) return;

            Time.timeScale = _frozenFrom > 0f ? _frozenFrom : 1f;
            _resumeAt = -1f;
        }
    }
}
