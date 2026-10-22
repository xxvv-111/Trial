using Game.Core;
using Game.Data;
using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 敌人血量。实现 IDamageable，死亡时广播 <see cref="Died"/> 供房间计数。
    ///
    /// ⚠️ **T7**：<see cref="Died"/> 是 **static 事件**（所有敌人共用一个订阅点），
    ///   跨场景重开理论上存在重复订阅风险。RoomController 已用 OnEnable/OnDisable 成对订阅兜住，
    ///   风险实际较低；若要彻底稳妥可改为实例事件。
    ///
    /// ⚠️ **T21**：本类受击时**只扣血、不通知 AI** —— 导致**小怪挨打毫无反应**（连闪白都不触发）。
    ///   按 GDD §6.1「小怪会被打断」，M1.2 需要在这里通知 AI 进入 Hit 硬直 + 闪白；
    ///   而 Boss 侧要保证不被此逻辑打断。
    ///
    /// ✅ **M3.6（2026-10-07）**：死亡不再"瞬间消失"。此前 <c>Die()</c> 直接
    ///   <c>gameObject.SetActive(false)</c>，敌人被打死的下一帧物体就没了 ⇒ **死亡动画一帧都播不出来**。
    ///   现在改为：先广播 <see cref="Damaged"/>（AI 借此切进 Death 状态、设下 <c>IsDead</c> 参数）
    ///   → 再按 <see cref="_deathHideDelay"/> 延迟隐藏，把时间让给死亡动画。
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static event Action<EnemyHealth> Died;

        /// <summary>
        /// 受击（扣血前广播，便于 AI 立刻反应）。参数 = (自身, 本次伤害)。
        ///
        /// ⚠️ 与 <see cref="Died"/> 不同，这是**实例事件** —— 订阅/退订与自身绑定，
        ///    不会出现"跨场景重开重复订阅"的问题（T7 的隐患只存在于 static 事件）。
        ///
        /// 这就是 **T21** 的接通点：此前 <c>TakeDamage</c> 只扣血、从不通知 AI，
        /// 导致小怪挨打毫无反应（连闪白都没有）。
        /// </summary>
        public event Action<EnemyHealth, int> Damaged;

        /// <summary>
        /// 血量变化通知。参数 = (当前血量, 血量上限)。
        ///
        /// ✅ **2026-10-15 新增**：血条 UI 用的就是它 ——
        ///   小怪头顶血条（<c>Game.UI.EnemyHealthBar</c>）与 Boss 屏幕血条
        ///   （<c>Game.UI.BossHealthBar</c>）都订阅这个事件。
        ///
        /// 为什么不用 <see cref="Damaged"/> 顶替：<c>Damaged</c> 只在「挨打」时广播，
        /// 而血条还需要在 **启用/满血重置**（<see cref="OnEnable"/>）时同步一次 —— 敌人被
        /// SetActive 复用时（房间重开 / 对象池），血条必须跟着回到满格，否则会停在上一轮的空血。
        /// </summary>
        public event Action<int, int> HpChanged;

        [SerializeField] private EnemyAIConfig _config;

        [Tooltip("死亡后延迟多少秒才隐藏物体（留时间给死亡动画播完）。\n" +
                 "0 = 立刻隐藏（M3.6 之前的旧行为，死亡动画看不到）。\n" +
                 "参考时长：Monster1 的 death 1.667 s、Monster2 的 deathspecial 2.2 s。")]
        [SerializeField] private float _deathHideDelay = 2.4f;

        public int MaxHp { get; private set; }
        public int CurHp { get; private set; }

        /// <summary>
        /// 配置里的**稳定 id**（任务系统按它统计击杀，例 <c>swordsman</c> / <c>caster</c> / <c>boss_crichton</c>）。
        /// 空字符串 = 配置没填 <see cref="EnemyAIConfig.enemyId"/>，击杀不会被任务统计（<see cref="Die"/> 会打 warning）。
        /// </summary>
        public string ConfigId => _config != null ? _config.enemyId : string.Empty;

        public bool IsDead { get { return CurHp <= 0; } }

        /// <summary>
        /// **受到的伤害倍率**（1 = 全额承受，0.1 = 只吃 10%）。
        ///
        /// <para>⭐ 2026-10-21 新增：Boss 在**非战斗状态**下由 <c>BossController</c> 设成 0.1（90% 免伤），
        /// 防止玩家站在脱战距离外一路偷血把它打死。小怪不设，保持默认 1。</para>
        ///
        /// <para>⚠️ 取整用 <see cref="Mathf.RoundToInt"/>：小额伤害（≤ 4）四舍五入后是 0 ⇒
        /// <see cref="TakeDamage"/> 直接返回，**不刷血条、不触发受击闪白、不累计削韧**
        /// —— 表现就是"打上去没反应"，这正是 90% 免伤想要的效果。</para>
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        //死亡延迟隐藏（用 unscaledTime 计时，理由见 Die()）
        private bool _hidingScheduled;
        private float _hideAtUnscaled;

        //死亡只广播一次：防止极少数情况下 Die() 被重复进入导致任务进度 +2（同类问题见 T7）
        private bool _killedBroadcast;

        private void OnEnable()
        {
            //重置于满血：让被复用的敌人（场景重开 / 对象池）状态干净
            MaxHp = _config.hp;
            CurHp = MaxHp;

            _hidingScheduled = false;//被复用复活时，别继承上一次的隐藏倒计时
            _hideAtUnscaled = 0f;
            _killedBroadcast = false;//被复用复活时，允许重新广播一次击杀

            RaiseHpChanged();//血条跟着回到满格（复用复活的关键一步）
        }

        /// <summary>
        /// **回血**（Boss 脱战恢复用）。
        ///
        /// <para>⚠️ 不会复活：<c>CurHp &lt;= 0</c> 时直接忽略 —— 复活走 <see cref="OnEnable"/> 里的重置。</para>
        /// <para>⚠️ 传的是**整数点**：需要「每秒百分比」这种小数速率时，
        ///    调用方（如 <c>BossController.RecoverHpWhileOutOfCombat</c>）自己攒够 1 点再调。</para>
        /// </summary>
        public void Restore(int amount)
        {
            if (amount <= 0) return;
            if (CurHp <= 0) return;//已死 → 不回血（避免被当成复活）
            if (CurHp >= MaxHp) return;

            CurHp = Mathf.Min(MaxHp, CurHp + amount);
            RaiseHpChanged();//血条跟着涨
        }

        public void TakeDamage(int damage)
        {
            if (CurHp <= 0) return;//已死 → 忽略后续伤害（判定体可能还开着）

            //⭐ 免伤结算（Boss 脱战 90% 免伤走这里）。四舍五入后为 0 ⇒ 整次命中作废。
            damage = ScaleDamage(damage);
            if (damage <= 0) return;

            CurHp -= damage;
            if (CurHp < 0) CurHp = 0;

            RaiseHpChanged();//先刷新血条，再走受击/死亡流程

            //先通知受击（AI 要立刻进入硬直/闪白），再判断死亡。
            //⚠️ 传的是**实际扣掉的**伤害（已乘倍率）—— 削韧按它算，脱战期的削韧也就跟着打折。
            if (Damaged != null) Damaged(this, damage);

            if (CurHp <= 0) Die();
        }

        /// <summary>把原始伤害乘上 <see cref="DamageMultiplier"/> 并取整。0 表示这次命中被完全挡下。</summary>
        private int ScaleDamage(int damage)
        {
            if (damage <= 0) return 0;
            if (Mathf.Approximately(DamageMultiplier, 1f)) return damage;//常态零开销
            return Mathf.RoundToInt(damage * Mathf.Max(0f, DamageMultiplier));
        }

        private void RaiseHpChanged()
        {
            if (HpChanged != null) HpChanged(CurHp, MaxHp);
        }

        private void Die()
        {
            if (_killedBroadcast) return;//一帧内被反复判定死亡时，只算一次击杀
            _killedBroadcast = true;

            //⭐ 任务系统的击杀统计入口（QUEST-SYSTEM.md §3.4 / §5.1）
            var enemyId = ConfigId;
            if (string.IsNullOrEmpty(enemyId))
            {
                Debug.LogWarning($"[Quest] 敌人「{name}」的配置没填 enemyId，这次击杀不会被任何任务统计", this);
            }
            else
            {
                // 验收用日志：杀一只怪应该看到这一行
                Debug.Log($"[EnemyKilled] {enemyId}（{(_config != null ? _config.displayName : name)}）@ {transform.position}");
            }
            GameEvents.RaiseEnemyKilled(enemyId, transform.position);

            if (Died != null) Died(this);

            if (_deathHideDelay <= 0f)
            {
                gameObject.SetActive(false);//旧行为
                return;
            }

            //⚠️ 用 unscaledTime 而不是协程 + WaitForSeconds：
            //   命中顿帧（HitStop）会把 timeScale 压到 0，缩放时间的等待会被额外拉长，
            //   和 InputService 的输入缓冲 / HitStop 自己的超时判断是同一套理由。
            _hidingScheduled = true;
            _hideAtUnscaled = Time.unscaledTime + _deathHideDelay;
        }

        private void Update()
        {
            if (!_hidingScheduled) return;
            if (Time.unscaledTime < _hideAtUnscaled) return;

            _hidingScheduled = false;
            gameObject.SetActive(false);
        }
    }
}
