using Fx;
using UnityEngine;
using Game.Core;

namespace Game.Fx
{
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
            _sparks = new ObjectPool<HitSpark>(sparkPrefab, 10, sparkParent);
            _popups = new ObjectPool<DamagePopup>(popupPrefab, 8, popupParent);
        }

        public void Play(Vector3 point,int damage)
        {
            var spark = _sparks.Get();
            spark.Show(point, _sparks.Release);

            var popup = _popups.Get();
            popup.Show(damage.ToString(), point, _popups.Release);
        }
    }
}