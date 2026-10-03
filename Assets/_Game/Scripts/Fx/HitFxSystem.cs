using UnityEngine;
using Game.Core;

namespace Game.Fx
{
    /// <summary>打击特效总控：火花 + 伤害飘字，用对象池驱动（不要每次 Instantiate）。</summary>
    public class HitFxSystem : MonoBehaviour
    {
        [SerializeField] private HitSpark sparkPrefab;
        [SerializeField] private DamagePopup popupPrefab;
        [SerializeField] private Transform sparkParent;
        [SerializeField] private Transform popupParent;

        private ObjectPool<HitSpark> _sparks;
        private ObjectPool<DamagePopup> _popups;

        private void Awake()
        {
            //判空：预制体未连线时只警告，不抛 NRE（原实现会直接崩）
            if (sparkPrefab == null || popupPrefab == null)
            {
                Debug.LogWarning("[HitFxSystem] sparkPrefab / popupPrefab 未连线，打击特效将被跳过。", this);
                return;
            }

            _sparks = new ObjectPool<HitSpark>(sparkPrefab, 10, sparkParent);
            _popups = new ObjectPool<DamagePopup>(popupPrefab, 8, popupParent);
        }

        /// <summary>在世界坐标 point 处弹出火花与伤害飘字。</summary>
        public void Play(Vector3 point, int damage)
        {
            if (_sparks == null || _popups == null) return;//Awake 时未初始化

            var spark = _sparks.Get();
            spark.Show(point, _sparks.Release);

            var popup = _popups.Get();
            popup.Show(damage.ToString(), point, _popups.Release);
        }
    }
}
