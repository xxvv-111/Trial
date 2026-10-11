using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 挂**玩家**身上的交互检测器，负责两件事：
    ///   1. **探测** —— 用触发器事件收集身边的可交互物，周期性地选出"唯一目标"
    ///   2. **调度** —— 目标非空且按下交互键时，把按键转交给目标（<see cref="IInteractable.Interact"/>）
    ///
    /// ⚠️ **为什么用触发器而不是每帧 OverlapSphere**：
    ///    球形扫描每帧要分配结果数组、遍历全部命中物；交互物一多就是纯浪费。
    ///    触发器只在"进 / 出"时产生事件，平时开销为零。
    ///    （交互物只有两三个时两者都行，但触发器方案能一直用到 NPC 密集的场景。）
    ///
    /// ⚠️ **本组件依赖玩家的 Collider 会真的移动**：<c>CharacterController</c> 只在被
    ///    <c>Move()</c> 驱动时才产生 <c>OnTriggerEnter</c>，原地摆着不动不会触发。
    ///    本项目的 <c>PlayerMotor</c> 每帧都在 Move，所以没问题
    ///    （<see cref="RoomTrigger"/> 用的就是同一套机制，已验收）。
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionDetector : MonoBehaviour
    {
        /// <summary>
        /// 目标变化时触发（参数 = 新目标；离开范围时参数为 null）。
        /// ⭐ UI **订阅这个事件**，不要每帧去读 <see cref="CurrentTarget"/> —— 那样每帧都要刷新文本，持续产生 GC。
        /// </summary>
        public event Action<IInteractable> TargetChanged;

        /// <summary>当前唯一目标。没有则为 null。</summary>
        public IInteractable CurrentTarget { get; private set; }

        [Tooltip("多久重新挑选一次目标（秒）。0.1 足够跟手，又不必每帧排序。")]
        [SerializeField] private float _scanInterval = 0.1f;

        /// <summary>一个候选 + 它当前被多少个 Collider 覆盖。</summary>
        private class Overlap
        {
            public IInteractable Target;
            public int Count;
        }

        private readonly List<Overlap> _overlaps = new List<Overlap>();
        private float _nextScanTime;

        // ==================== 触发器：只做收集 ====================

        private void OnTriggerEnter(Collider other)
        {
            IInteractable it = Resolve(other);
            if (it == null) return;

            Overlap entry = Find(it);
            if (entry == null) _overlaps.Add(new Overlap { Target = it, Count = 1 });
            else entry.Count++;

            _nextScanTime = 0f;//⭐ 立即重挑：否则要等下一个扫描周期，提示会慢 0.1 s
        }

        private void OnTriggerExit(Collider other)
        {
            IInteractable it = Resolve(other);
            if (it == null) return;

            Overlap entry = Find(it);
            if (entry == null) return;

            //⚠️ 用"计数"而不是"直接删"：同一个物体可能挂着多个 Collider
            //   （玩家身上就有 CharacterController + 攻击 Hitbox），
            //   若直接删，任意一个 Collider 离开都会让目标提前消失。
            entry.Count--;
            if (entry.Count <= 0) _overlaps.Remove(entry);

            _nextScanTime = 0f;
        }

        // ==================== 每帧：挑选 + 消费按键 ====================

        private void Update()
        {
            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + _scanInterval;
                Rescan();
            }

            if (CurrentTarget == null) return;

            InputService input = InputService.Instance;
            if (input == null) return;

            //⚠️ 面板打开时 InputService 会整体关掉游戏性输入，本判断自然为 false，
            //   所以不会出现"面板开着还能再触发一次交互"。
            if (input.InteractPressedThisFrame)
                CurrentTarget.Interact(gameObject);
        }

        // ==================== 内部 ====================

        /// <summary>
        /// 把一个 Collider 解析成可交互物。
        /// ⚠️ 要往父级找：触发器 Collider 常挂在子物体上（如门的触发器是门下的空物体）。
        /// </summary>
        private static IInteractable Resolve(Collider other)
        {
            if (other == null) return null;
            return other.GetComponentInParent<IInteractable>();
        }

        private Overlap Find(IInteractable it)
        {
            for (int i = 0; i < _overlaps.Count; i++)
                if (ReferenceEquals(_overlaps[i].Target, it)) return _overlaps[i];
            return null;
        }

        /// <summary>
        /// 从候选里挑出唯一目标：**可用的**之中先比 <see cref="IInteractable.Priority"/>（大者优先），
        /// 优先级相同再比距离（近者优先）。
        /// </summary>
        private void Rescan()
        {
            Vector3 self = transform.position;

            IInteractable best = null;
            int bestPriority = int.MinValue;
            float bestSqrDistance = float.MaxValue;

            for (int i = _overlaps.Count - 1; i >= 0; i--)
            {
                IInteractable candidate = _overlaps[i].Target;

                //⚠️⚠️ 这里必须转成 UnityEngine.Object 再比 null！
                //   Unity 重载了 UnityEngine.Object 的 `==`，让"已销毁的对象 == null"成立；
                //   但**接口类型的 `== null` 是纯引用比较**，对已销毁的 MonoBehaviour 会返回 false。
                //   不这么写，物体在玩家怀里被销毁时会留下永久幽灵候选。
                if ((candidate as UnityEngine.Object) == null)
                {
                    _overlaps.RemoveAt(i);
                    continue;
                }

                //⭐ 不可交互者整体排除 —— 不能出现"提示显示的是灰掉的那个"。
                if (!candidate.CanInteract) continue;

                Component comp = candidate as Component;
                if (comp == null) continue;

                float sqr = (comp.transform.position - self).sqrMagnitude;
                if (candidate.Priority > bestPriority ||
                    (candidate.Priority == bestPriority && sqr < bestSqrDistance))
                {
                    best = candidate;
                    bestPriority = candidate.Priority;
                    bestSqrDistance = sqr;
                }
            }

            if (!ReferenceEquals(best, CurrentTarget))
            {
                CurrentTarget = best;
                TargetChanged?.Invoke(best);
            }
        }
    }
}
