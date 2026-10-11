using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 可交互物的统一契约：任何"玩家靠近后按键能触发"的东西都实现它。
    ///
    /// ⚠️ **为什么用接口而不是基类**：交互物之间没有共同的"是什么"，
    ///    只有共同的"能做什么"——告示栏是块木板、门是一扇门、NPC 是一个人，
    ///    它们的继承链各不相同。用接口可以让这些物件各自继承自己的类，
    ///    只额外实现这一个契约，而检测器只认接口、不认具体类型。
    ///
    /// 参考：`Docs/PHASE1-DESIGN.md` §4.2（NPC 交互框架）。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 提示文案，例如"查看告示栏"。
        /// ⚠️ **只写动作，不要写键名** —— 键名由 <c>InteractPromptUI</c> 统一拼上去，
        ///    这样将来换键或适配手柄时只改一处。
        /// </summary>
        string Prompt { get; }

        /// <summary>
        /// 当前是否可交互。返回 false 时该物**完全不参与选中**（不显示提示、按键也无效），
        /// 不会出现"提示指向一个灰掉的物体"。
        /// 典型用法：魔晶不够的升级台、已领奖的任务 NPC。
        /// </summary>
        bool CanInteract { get; }

        /// <summary>
        /// 同屏有多个可交互物时的排序权重，**大的优先**；相同则比距离（近的优先）。
        /// 默认 0。用于"门口同时站着 NPC 时，门优先"这类需求。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 真正执行交互。<paramref name="actor"/> = 发起交互的对象（本项目里是玩家的 GameObject）。
        /// </summary>
        void Interact(GameObject actor);
    }
}
