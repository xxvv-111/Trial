using UnityEngine;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 生命条 HUD。订阅 <see cref="PlayerHealth.OnHpChanged"/>，用锚点拉伸填充块。
    ///
    /// ⚠️ 本组件的 UI 已抽成**共用预制体** <c>Prefabs/UI/HUD_Common.prefab</c>。
    ///    预制体资产**不能保存对场景里玩家实例的引用**（Castle 用 Player2、Forest 用 Player3），
    ///    所以 <c>_player</c> 在预制体里必须留空 —— 运行时由 <see cref="Start"/> 自动查找。
    ///    确实需要手动指定（多玩家 / 分屏）时，在场景实例上 override 即可。
    /// </summary>
    public class HPBar : MonoBehaviour
    {
        [Tooltip("留空 = 运行时自动找场景里的 PlayerHealth（共用预制体请留空）。")]
        [SerializeField] private PlayerHealth _player;
        [SerializeField] private RectTransform _fill;//实际血条

        private void OnEnable()
        {
            ResolvePlayer();
            if (_player != null) _player.OnHpChanged += OnHp;
        }

        private void OnDisable()
        {
            if (_player != null) _player.OnHpChanged -= OnHp;
        }

        /// <summary>
        /// ⚠️ 两道保险，缺一不可：
        ///   1. <c>OnEnable</c> 跑在所有 <c>Awake</c> 之前，玩家若由别处实例化，那时还查不到；
        ///   2. <c>PlayerHealth.Start</c> 会广播一次初值 —— 必须已经订阅上才收得到，
        ///      所以这里「先退订再订阅」（保证不重复订阅）+ 主动拉一次当前值。
        /// </summary>
        private void Start()
        {
            ResolvePlayer();
            if (_player == null) return;

            _player.OnHpChanged -= OnHp;
            _player.OnHpChanged += OnHp;
            OnHp(_player.CurHp, _player.MaxHp);
        }

        private void ResolvePlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerHealth>();
        }

        private void OnHp(int cur, int max)
        {
            SetFill(max <= 0 ? 0f : cur / (float)max);
        }

        /// <summary>fraction ∈ [0,1]，用锚点拉伸填充块。</summary>
        private void SetFill(float fraction)
        {
            if (_fill == null) return;

            fraction = Mathf.Clamp01(fraction);
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(fraction, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }
    }
}
