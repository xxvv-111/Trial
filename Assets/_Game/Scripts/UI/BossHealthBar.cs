using Game.Gameplay;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Boss **固定在屏幕上**的生命条（血条 + 韧性条）。
    ///
    /// <para>和小怪头顶血条的区别：小怪血条是 3D 世界里的物体（跟着敌人跑），
    /// Boss 血条是 <b>Screen Space Overlay</b> 的 UI —— 不管 Boss 跑到哪里、镜头怎么转，
    /// 它永远钉在屏幕固定位置（这里是顶部居中）。</para>
    ///
    /// <para>⭐ 2026-10-21 起：血条**不再挂在主 HUD 的 Canvas 下**，
    /// 而是独立成 <c>Prefabs/UI/BossBar.prefab</c> 并**嵌在 <c>Boss_Crichton.prefab</c> 内部**
    /// —— 把 Boss 拖进任何场景就自带血条，且血条自成一个 Screen Space Overlay Canvas
    /// （sortingOrder = -1，让 HUD 里的暂停 / 结算面板能盖住它）。结构：</para>
    /// <code>
    /// Boss_Crichton (BossController + EnemyHealth)
    /// └── BossBar            ← 本脚本（永远 active，负责收事件）+ Canvas(Overlay, -1) + CanvasScaler
    ///     └── Panel          ← 拖给 _panel，显示/隐藏靠它
    ///         ├── Name       ← TextMeshProUGUI
    ///         ├── HpBack
    ///         │   └── HpFill     ← 拖给 _hpFill
    ///         └── PoiseBack
    ///             └── PoiseFill  ← 拖给 _poiseFill
    /// </code>
    ///
    /// <para>⚠️ 为什么脚本不直接挂在 Panel 上、用 <c>gameObject.SetActive(false)</c> 隐藏？
    /// 因为那样会把脚本自己一起停掉，<c>OnDisable</c> 里退订了事件就再也收不到
    /// 「Boss 进战斗」的通知，血条永远显示不出来。所以：<b>脚本挂在父节点，控制子节点显隐</b>。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealthBar : MonoBehaviour
    {
        // ==================== 数据源 ====================

        [Header("数据源（留空会自动在场景里找）")]
        [Tooltip("Boss 的行为脚本（提供韧性 / 战斗状态）。")]
        [SerializeField] private BossController _boss;

        [Tooltip("Boss 的血量组件。留空 = 从 _boss 所在物体上取。")]
        [SerializeField] private EnemyHealth _health;

        // ==================== UI 引用 ====================

        [Header("UI 引用")]
        [Tooltip("整个面板的根节点（子物体）。⚠️ 不要指向本脚本所在的物体，见类注释。")]
        [SerializeField] private GameObject _panel;

        [Tooltip("血条的红色填充块。")]
        [SerializeField] private RectTransform _hpFill;

        [Tooltip("韧性条的黄色填充块。不需要韧性条就留空。")]
        [SerializeField] private RectTransform _poiseFill;

        // ==================== 行为 ====================

        [Header("行为")]
        [Tooltip("没进战斗时隐藏面板（推荐）。关掉的话 Boss 血条会一直挂在屏幕上。")]
        [SerializeField] private bool _hideWhenNotInCombat = true;

        [Tooltip("Boss 死后延迟多少秒收起面板（留点时间让玩家看到被打空的血条）。用 unscaledTime 计时，不受顿帧影响。")]
        [SerializeField] private float _deathHideDelay = 2f;

        [Tooltip("血条 / 韧性条的追赶速度（每秒多少比例）。0 = 瞬时到位。\n" +
                 "给 2~4 会有明显的\"血条往下滑\"的反馈，打击感更好。")]
        [SerializeField] private float _fillLerpSpeed = 3f;

        // ==================== 运行时 ====================

        private float _hpTarget = 1f, _hpShown = 1f;
        private float _poiseTarget, _poiseShown;
        private float _hideAtUnscaled = -1f;

        private void OnEnable()
        {
            //OnEnable 里订阅而不是 Start：本物体可能被反复启停，
            //成对订阅/退订才不会漏（这是本工程一贯的写法，见 GameEvents 的注释）。
            if (_boss != null) _boss.PoiseChanged += OnPoiseChanged;
            if (_boss != null) _boss.CombatChanged += OnCombatChanged;
            if (_health != null) _health.HpChanged += OnHpChanged;
        }

        private void OnDisable()
        {
            if (_boss != null) _boss.PoiseChanged -= OnPoiseChanged;
            if (_boss != null) _boss.CombatChanged -= OnCombatChanged;
            if (_health != null) _health.HpChanged -= OnHpChanged;
        }

        private void Start()
        {
            ResolveRefs();

            if (_boss == null || _health == null)
            {
                Debug.LogWarning("[BossHealthBar] 场景里没找到 BossController / EnemyHealth，Boss 血条不会工作。" +
                                 "请在 Inspector 里手动把引用拖上。", this);
                SetPanel(false);
                return;
            }

            //⚠️ 订阅必须在「引用已解析」之后。上面 OnEnable 可能跑在 _boss 还是 null 的时候
            //  （如果 Inspector 没拖引用、靠自动查找），所以这里补订一次；
            //  先用 -= 再 += 保证不会重复订阅。
            _boss.PoiseChanged -= OnPoiseChanged;
            _boss.CombatChanged -= OnCombatChanged;
            _health.HpChanged -= OnHpChanged;
            _boss.PoiseChanged += OnPoiseChanged;
            _boss.CombatChanged += OnCombatChanged;
            _health.HpChanged += OnHpChanged;

            //同步初值（事件是"变化时"才发，开局要先自己拉一次）
            OnHpChanged(_health.CurHp, _health.MaxHp);
            OnPoiseChanged(_boss.Poise01, 1f);
            _hpShown = _hpTarget;
            _poiseShown = _poiseTarget;
            ApplyFills();

            //开局显隐：没在战斗就藏起来
            bool show = !_hideWhenNotInCombat || _boss.InCombat;
            SetPanel(show);
            _hideAtUnscaled = -1f;
        }

        private void ResolveRefs()
        {
            //⭐ 2026-10-21：血条已嵌在 Boss 预制体内部，`_boss` / `_health` 在预制体里**硬拖好了**
            //   （指向 Boss 预制体根自身），所以这里不再做全场景查找。
            //   ⚠️ 原来那句 `FindFirstObjectByType<BossController>()` 是**有害**的：
            //      场景里出现第二只 Boss、或血条被误留在没有 Boss 的场景时，
            //      它会静默绑到"随便哪一只"上去，比不绑定还难排查。
            //   保留父链兜底（1 行、零成本）：把 BossBarCanvas 挂到任何 Boss 底下都能自愈，不用手拖。
            if (_boss == null) _boss = GetComponentInParent<BossController>();
            if (_health == null && _boss != null) _health = _boss.GetComponent<EnemyHealth>();
        }

        private void Update()
        {
            //---- 平滑追赶 ----
            if (_hpFill != null && !Mathf.Approximately(_hpShown, _hpTarget))
            {
                _hpShown = _fillLerpSpeed > 0f
                    ? Mathf.MoveTowards(_hpShown, _hpTarget, _fillLerpSpeed * Time.deltaTime)
                    : _hpTarget;
                StretchFill(_hpFill, _hpShown);
            }

            if (_poiseFill != null && !Mathf.Approximately(_poiseShown, _poiseTarget))
            {
                _poiseShown = _fillLerpSpeed > 0f
                    ? Mathf.MoveTowards(_poiseShown, _poiseTarget, _fillLerpSpeed * Time.deltaTime)
                    : _poiseTarget;
                StretchFill(_poiseFill, _poiseShown);
            }

            //---- 死亡后的延迟收起 ----
            //⚠️ 用 unscaledTime：命中顿帧会把 timeScale 压到 0，用 scaledTime 会被无限拉长。
            if (_hideAtUnscaled > 0f && Time.unscaledTime >= _hideAtUnscaled)
            {
                _hideAtUnscaled = -1f;
                SetPanel(false);
            }
        }

        // ==================== 事件 ====================

        private void OnHpChanged(int cur, int max)
        {
            _hpTarget = max <= 0 ? 0f : Mathf.Clamp01(cur / (float)max);
        }

        private void OnPoiseChanged(float cur, float max)
        {
            //Poise01 = 已累计的削韧量占比：条越长 = 越接近破韧。
            //想反过来显示「剩余韧性」的话，把下面这行改成 1f - 那个值即可。
            _poiseTarget = max <= 0f ? 0f : Mathf.Clamp01(cur / max);
        }

        private void OnCombatChanged(bool inCombat)
        {
            if (inCombat)
            {
                _hideAtUnscaled = -1f;
                SetPanel(true);
                return;
            }

            //退出战斗：死亡 → 延迟收起；只是脱战 → 立刻收起
            if (_health != null && _health.IsDead)
            {
                _hideAtUnscaled = Time.unscaledTime + _deathHideDelay;
                return;
            }

            if (_hideWhenNotInCombat) SetPanel(false);
        }

        // ==================== 工具 ====================

        private void ApplyFills()
        {
            StretchFill(_hpFill, _hpShown);
            StretchFill(_poiseFill, _poiseShown);
        }

        /// <summary>比例 → 拉伸填充块。和小怪血条 / 玩家血条同一套手法。</summary>
        private static void StretchFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;

            fraction = Mathf.Clamp01(fraction);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(fraction, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private void SetPanel(bool visible)
        {
            if (_panel == null) return;
            if (_panel.activeSelf != visible) _panel.SetActive(visible);
        }
    }
}
