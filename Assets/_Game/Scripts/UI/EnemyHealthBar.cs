using Game.Gameplay;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 小怪**头顶悬浮**生命条。
    ///
    /// <para>用法：挂在敌人预制体下的一个 <b>World Space Canvas</b> 上，结构：</para>
    /// <code>
    /// Monster1
    /// └── HPBar      ← Canvas(World Space) + 本脚本
    ///     └── Back   ← Image（深色底），RectTransform 决定血条尺寸
    ///         └── Fill ← Image（红色），脚本会拉伸它的锚点
    /// </code>
    ///
    /// <para>三件事：</para>
    /// <list type="number">
    ///   <item><b>跟随</b>：血条是敌人的子物体，敌人走它就跟着走，不需要任何代码。</item>
    ///   <item><b>朝向相机</b>：LateUpdate 里把世界旋转直接设成相机的旋转（Billboard）。
    ///         ⚠️ 必须每帧覆盖 —— 因为敌人会自己转向，子物体会跟着转，血条会侧过去看不见。</item>
    ///   <item><b>填充</b>：改 Fill 的 <c>anchorMax.x</c>，和玩家血条 <c>HPBar.cs</c> 的手法完全一致。</item>
    /// </list>
    ///
    /// ⚠️ 血量数据来自 <see cref="EnemyHealth.HpChanged"/>（2026-10-15 新增的事件）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        // ==================== 引用 ====================

        [Tooltip("血量来源。留空会自动从父物体上找 EnemyHealth。")]
        [SerializeField] private EnemyHealth _health;

        [Tooltip("红色填充块的 RectTransform（通常是 Back 下的 Fill）。")]
        [SerializeField] private RectTransform _fill;

        [Tooltip("Billboard 用的相机。留空 = 自动用 Camera.main。")]
        [SerializeField] private Camera _camera;

        // ==================== 行为开关 ====================

        [Tooltip("血条是否始终正对相机（3D 游戏必须开，否则敌人转身血条会侧过去）。")]
        [SerializeField] private bool _faceCamera = true;

        [Tooltip("超过这个距离（米）就隐藏血条，省性能也免得远处糊成一片。0 = 不限制。")]
        [SerializeField] private float _visibleDistance = 30f;

        [Tooltip("满血时是否隐藏血条。true = 只有挨过打的小怪才显示血条（很多 ARPG 的做法）。")]
        [SerializeField] private bool _hideWhenFull = false;

        [Tooltip("死亡后立即隐藏血条（留尸体在原地播死亡动画时不显示空血条）。")]
        [SerializeField] private bool _hideOnDeath = true;

        [Tooltip("扣血时血条的**追赶速度**（每秒多少比例）。0 = 瞬时到位。\n" +
                 "给 3~8 会有明显的\"掉血\"手感，给 0 则是一格一格硬跳。")]
        [SerializeField] private float _fillLerpSpeed = 6f;

        // ==================== 运行时 ====================

        private Canvas _canvas;
        private float _target = 1f;   // 目标比例（0..1）
        private float _shown = 1f;    // 当前显示比例（平滑用）
        private bool _dead;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();

            if (_health == null) _health = GetComponentInParent<EnemyHealth>();
            if (_health == null)
            {
                Debug.LogWarning("[EnemyHealthBar] 找不到 EnemyHealth（自身和父物体上都没有），血条已自动停用。", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_health != null) _health.HpChanged += OnHpChanged;
            if (_camera == null) _camera = Camera.main;
            SyncNow();
        }

        private void OnDisable()
        {
            if (_health != null) _health.HpChanged -= OnHpChanged;
        }

        private void Start()
        {
            //⚠️ 为什么 OnEnable 里同步过一次，Start 还要再来一次？
            //   Unity 的执行顺序是「父物体的 OnEnable 早于子物体」。本血条是子物体，
            //   而 EnemyHealth 挂在**父物体**上 —— 所以本血条 OnEnable 时，EnemyHealth 可能
            //   还没跑自己的 OnEnable（还没把 CurHp 重置成满血），读到的会是 0。
            //   Start 在所有 Awake/OnEnable 之后执行，这里再同步一次就稳了。
            if (_camera == null) _camera = Camera.main;
            SyncNow();
        }

        /// <summary>主动拉一次当前血量并立刻刷新（不做插值）。</summary>
        private void SyncNow()
        {
            if (_health != null)
            {
                _target = _health.MaxHp <= 0 ? 0f : Mathf.Clamp01(_health.CurHp / (float)_health.MaxHp);
                _dead = _health.MaxHp > 0 && _health.CurHp <= 0;
            }
            _shown = _target;
            ApplyFill(_shown);
            ApplyVisibility();
        }

        private void OnHpChanged(int cur, int max)
        {
            _target = max <= 0 ? 0f : Mathf.Clamp01(cur / (float)max);
            _dead = max > 0 && cur <= 0;
            ApplyVisibility();
        }

        private void LateUpdate()
        {
            // ---- 1. Billboard：直接抄相机的世界旋转 ----
            //⚠️ 用 transform.rotation（世界旋转）而不是 localRotation：敌人自己会转身，
            //   子物体跟着转，只有覆盖世界旋转才能保证血条永远正对屏幕。
            if (_faceCamera)
            {
                if (_camera == null) _camera = Camera.main;
                if (_camera != null) transform.rotation = _camera.transform.rotation;
            }

            // ---- 2. 平滑追赶 ----
            if (!Mathf.Approximately(_shown, _target))
            {
                if (_fillLerpSpeed > 0f)
                    _shown = Mathf.MoveTowards(_shown, _target, _fillLerpSpeed * Time.deltaTime);
                else
                    _shown = _target;

                ApplyFill(_shown);
            }

            // ---- 3. 显隐（距离剔除 / 满血隐藏 / 死亡隐藏）----
            //⚠️ 必须**无条件**每帧算，不能只在"距离限定开着"时才算：
            //   · 玩家走远时血量没有任何变化，没有事件会来通知我们，只能每帧自己看；
            //   · _visibleDistance 一旦被改成 0（关掉距离限定），条件调用会导致血条
            //     停在"上一次被剔除"的隐藏状态再也回不来（2026-10-15 实测踩到过）。
            ApplyVisibility();
        }

        /// <summary>比例 → 拉伸填充块。手法与玩家血条 <c>HPBar.cs</c> 完全一致。</summary>
        private void ApplyFill(float fraction)
        {
            if (_fill == null) return;

            fraction = Mathf.Clamp01(fraction);
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(fraction, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }

        private void ApplyVisibility()
        {
            bool visible = true;

            if (_hideOnDeath && _dead) visible = false;
            if (_hideWhenFull && Mathf.Approximately(_target, 1f)) visible = false;

            if (visible && _visibleDistance > 0f)
            {
                if (_camera == null) _camera = Camera.main;
                if (_camera != null)
                {
                    Vector3 d = transform.position - _camera.transform.position;
                    if (d.sqrMagnitude > _visibleDistance * _visibleDistance) visible = false;
                }
            }

            if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
        }

        /// <summary>给外部（例如房间开局）强制刷新用。</summary>
        public void RefreshNow() { SyncNow(); }
    }
}
