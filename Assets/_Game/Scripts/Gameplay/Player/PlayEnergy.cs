using System;
using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    public class PlayerEnergy : MonoBehaviour
    {
        public event Action<float, float> OnEnergyChanged;

        [SerializeField] private PlayerConfig _config;

        public float MaxEnergy { get; private set; }
        public float CurEnergy { get; private set; }

        private void Start()
        {
            MaxEnergy = _config.maxEnergy;
            CurEnergy = MaxEnergy;
            Broadcast();
        }

        public bool TrySpend(float cost)
        {
            if (CurEnergy < cost) return false;
            CurEnergy -= cost;
            Broadcast();
            return true;
        }

        private void Broadcast() => OnEnergyChanged?.Invoke(CurEnergy, MaxEnergy);
    }
}