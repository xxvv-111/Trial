using System.Collections.Generic;

namespace Game.Gameplay
{
    /// <summary>
    /// 「玩家是否处于战斗」的全局查询点 —— **靠敌人状态判断，不靠伤害事件计时**。
    ///
    /// <para>规则：场景里**只要有任何一个敌人**处于「已警觉 / 交战中」，就算玩家在战斗中。
    /// 待机（`Idle` / `NIdle`）与死亡（`Death`）不算；Boss 的 `Return`（脱战走回原位）也不算。</para>
    ///
    /// <para>━━ 为什么不用「最近 N 秒受过伤」这种计时器判据 ━━</para>
    /// <list type="bullet">
    ///   <item>挨过打之后固定冷却一段时间，但玩家可能早就跑远了，那段时间白禁；</item>
    ///   <item>反过来「怪已经发现你、但还没打到你」这种**最该禁交互**的时刻，计时器判不出来；</item>
    ///   <item>敌人状态是**当下的事实**，不需要猜。</item>
    /// </list>
    ///
    /// <para>━━ 登记方式 ━━</para>
    /// <para>由 <see cref="EnemyAIController"/> 的 `OnEnable` / `OnDisable` 自动登记与注销，
    /// 本类只管查询。遍历成本 = 场景里活着的敌人数（几十个），可以忽略。</para>
    /// </summary>
    public static class CombatRegistry
    {
        private static readonly List<EnemyAIController> Active = new List<EnemyAIController>();

        /// <summary>敌人启用时登记（由基类 <see cref="EnemyAIController"/> 调用）。</summary>
        public static void Register(EnemyAIController enemy)
        {
            if (enemy == null) return;
            if (Active.Contains(enemy)) return;   // 幂等：重复 SetActive(true) 不会重复登记
            Active.Add(enemy);
        }

        /// <summary>敌人停用时注销。</summary>
        public static void Unregister(EnemyAIController enemy)
        {
            if (enemy == null) return;
            Active.Remove(enemy);
        }

        /// <summary>场景里是否有敌人正在警觉或交战。</summary>
        public static bool AnyEngaged
        {
            get
            {
                // 倒序遍历：顺手清掉「已销毁但没来得及走 OnDisable」的残留条目
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    var e = Active[i];
                    if (e == null) { Active.RemoveAt(i); continue; }   // Unity 重载了 ==，已销毁对象判 null 成立
                    if (e.IsEngaged) return true;
                }
                return false;
            }
        }

        /// <summary>当前登记了多少个敌人（调试 / 自检用）。</summary>
        public static int Count { get { return Active.Count; } }
    }
}
