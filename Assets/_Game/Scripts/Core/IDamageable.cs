using UnityEngine;
namespace Game.Core
{
    /// <summary>可受击目标。玩家、敌人、测试靶子均实现它。</summary>
    public interface IDamageable
    {
        void TakeDamage(int dmg);
    }
}
