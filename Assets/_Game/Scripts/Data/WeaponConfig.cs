using UnityEngine;

namespace Game.Date
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Game/WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        public string weaponName = "赤手空拳";
        public int damage = 15;
        public float attackRange = 2.2f;
        public float comboInterval = 0.5f;//连击间隔
    }
}