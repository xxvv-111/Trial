using UnityEngine;
using UnityEngine.Events;

namespace Game.Gameplay
{
    /// <summary>
    /// <see cref="IInteractable"/> 的通用实现：**行为完全在 Inspector 里配置**，
    /// 不必为每个交互物写一个类。
    ///
    /// 用法（三步）：
    ///   1. 把本组件挂在可交互物上
    ///   2. 填 <c>prompt</c>（只写动作，如"查看告示栏"）
    ///   3. 把"按下后要干什么"拖到 <c>onInteract</c> 列表里
    ///
    /// 本项目的两个用法：
    ///   · 告示栏 → onInteract 拖 <c>QuestPanel.Open</c>
    ///   · 门口   → onInteract 拖 <c>SceneGate.Go</c>
    /// 以后加第 3 个（NPC、宝箱、机关）**零代码**，这就是用 <see cref="UnityEvent"/>
    /// 而不是"给每种交互物写个子类"的收益。
    ///
    /// ⚠️ 本组件**不负责"靠近检测"** —— 那是玩家身上 <see cref="InteractionDetector"/> 的事。
    ///    但物体上**必须有一个 isTrigger 的 Collider**（Box 或 Sphere 都行），
    ///    否则检测器根本发现不了它。
    /// </summary>
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour, IInteractable
    {
        [Tooltip("提示文案，不含键名。例如：查看告示栏 / 前往城堡")]
        [SerializeField] private string prompt = "交互";

        [Tooltip("同屏多个可交互物时的优先度，数值大的先被选中（相同则比距离）。")]
        [SerializeField] private int priority = 0;

        [Tooltip("按下交互键后要执行什么。拖方法进来，例如 QuestPanel.Open。")]
        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        public string Prompt { get { return prompt; } }
        public int Priority { get { return priority; } }

        /// <summary>
        /// 默认只要物体是激活的就可交互。
        /// 需要条件判断时把本类当基类继承并 override（见 <see cref="CanInteract"/> 的用法说明）。
        /// </summary>
        public virtual bool CanInteract { get { return isActiveAndEnabled; } }

        public void Interact(GameObject actor)
        {
            // ⚠️ 调用方（InteractionDetector）已经判过 CanInteract，这里再判一次是防手滑：
            //    别人直接调 Interact 时也不会绕过条件。
            if (!CanInteract) return;

            // `?.` 防空：UnityEvent 字段被 Inspector 序列化成非 null，但代码 new 出来的可能为 null。
            onInteract?.Invoke();
        }
    }
}
