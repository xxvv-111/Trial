using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 场景里的「探查点」：玩家走近后**按交互键**调查，才上报 <see cref="GameEvents.PointReached"/>。
    /// 任务目标 <c>QuestObjectiveType.ReachPoint</c> 靠这个推进。
    ///
    /// <para>━━ 为什么是「交互」而不是「走进触发」 ━━</para>
    /// <para>
    /// 「探查上山通路」这种目标，语义上是**主动调查**，不是路过。
    /// 走进范围就自动完成会让玩家莫名其妙 —— 我明明是来打架的，怎么任务就完成了？
    /// 改成按 F 之后：走近 → 提示「按 F 调查」→ 按下 → 才算探查过。
    /// </para>
    ///
    /// <para>━━ 战斗时不能调查 ━━</para>
    /// <para>
    /// <see cref="CanInteract"/> 在场景里有敌人「已警觉 / 交战中」时返回 false
    /// （判据见 <see cref="CombatRegistry"/>）。于是一旦被怪盯上，调查提示会消失、按键也无效。
    /// </para>
    /// <list type="bullet">
    ///   <item>战斗**之前**就站在范围内：提示已经显示，敌人冲过来后仍然可以按 F
    ///         —— 所以 <see cref="Interact"/> 里**必须再判一次**，不能只靠进入时的筛选；
    ///         这条是唯一的兜底，别删。</item>
    ///   <item>战斗中走进来：检测器重新扫描时会因 <c>CanInteract == false</c> 直接忽略它，
    ///         提示不出现、按 F 也没反应。</item>
    /// </list>
    ///
    /// <para>━━ 怎么摆 ━━</para>
    /// <para>空物体 + <c>SphereCollider</c>（**必须 isTrigger**，检测器靠它发现目标）+ 本组件；
    /// <c>pointId</c> 填成任务配置里 <c>ReachPoint</c> 目标的 <c>targetId</c>，
    /// **两处必须逐字一致**，拼错的表现是"任务永远完不成"。</para>
    ///
    /// <para>⚠️ 层过滤不在这里做 —— <see cref="InteractionDetector"/> 只认玩家身上的触发器。</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestPointTrigger : MonoBehaviour, IInteractable
    {
        [Tooltip("探查点 id。⚠️ 必须与 QuestConfig 里 ReachPoint 目标的 targetId 逐字一致。")]
        [SerializeField] private string _pointId = "point_forest_scout";

        [Tooltip("交互提示里的动作文案（不含键名，键名由 InteractPromptUI 拼）。")]
        [SerializeField] private string _prompt = "调查";

        [Tooltip("同屏多个可交互物时的优先度，大的先被选中。")]
        [SerializeField] private int _priority = 0;

        [Tooltip("调试用：在 Scene 视图画出触发半径。")]
        [SerializeField] private bool _drawGizmo = true;

        /// <summary>本次进入场景后是否已经调查过（切换场景 / 复用后会重置）。</summary>
        private bool _done;

        public string Prompt { get { return _prompt; } }

        public int Priority { get { return _priority; } }

        /// <summary>
        /// 调查过、或场景里有敌人在战斗中时为 false。
        /// 返回 false 时本物**完全不参与选中**（见 <see cref="IInteractable.CanInteract"/> 的约定）。
        /// </summary>
        public bool CanInteract
        {
            get { return !_done && !CombatRegistry.AnyEngaged; }
        }

        public void Interact(GameObject actor)
        {
            //⭐ 必须在这里再判一次：玩家可能**先**站在范围内（提示已显示），**之后**敌人才冲过来。
            //  检测器不会因为敌人状态变化而重扫，所以进入时的筛选挡不住这种情况。
            if (!CanInteract) return;

            if (string.IsNullOrWhiteSpace(_pointId))
            {
                Debug.LogWarning($"[PointReached] 探查点 {name} 没填 pointId，本次不生效", this);
                return;
            }

            _done = true;
            Debug.Log($"[PointReached] {_pointId}（{name}）");
            GameEvents.RaisePointReached(_pointId);
        }

        private void Reset()
        {
            // 在 Inspector 里刚挂上组件时，顺手把碰撞体设成 Trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            _done = false;   // 场景重进 / 对象复用后允许再次调查
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmo) return;

            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);

            //⚠️ 原来只画 SphereCollider，选了 BoxCollider 时退化成"半径 1 的球"，
            //   画出来的范围和实际能触发的范围**不一致** —— 会让人误判"我明明站在框里"。
            //   统一改用 Collider.bounds：它已经是世界空间、且已经算进 localScale 的最终尺寸。
            var b = col.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
