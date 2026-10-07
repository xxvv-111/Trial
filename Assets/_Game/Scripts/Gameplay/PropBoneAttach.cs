using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 把「道具骨」约束到「目标骨」上 —— 用来让 Boss 的剑跟着右手动。
    ///
    /// <para><b>为什么需要这个组件</b></para>
    /// <para>
    /// Boss_Crichton 是 FBX 模型，Unity 内部把模型资产当作**预制体**
    /// （<c>PrefabUtility.GetPrefabAssetType</c> 返回 <c>Model</c>），
    /// 所以模型实例的子物体**不允许重排** —— 在 Hierarchy 里把 <c>Bip001-Prop1</c>
    /// 拖到 <c>Bip001-R-Hand</c> 下会报
    /// 「Children of a Prefab instance cannot be moved, and components cannot be reordered」。
    /// </para>
    /// <para>
    /// 而 <c>Bip001-Prop1</c> 的父级是 <c>Bip001</c>（Unity 自动映射成的 <c>Hips</c>），
    /// 剑的网格又是 100% 蒙在这根骨上的；<c>Prop1</c> <b>不在</b> Humanoid 的 39 条映射里，
    /// 所以重定向来的动画**不会驱动它** —— 结果就是「手挥出去了，剑还留在骨盆旁边」。
    /// </para>
    ///
    /// <para><b>本组件的做法</b></para>
    /// <para>
    /// **不改层级**，只在 <see cref="LateUpdate"/> 里把道具骨的世界位置/旋转写成
    /// 「目标骨 × 偏移」。视觉效果与改父级等价，且**删掉组件即可完全还原**。
    /// </para>
    ///
    /// <para><b>⚠️ 注意事项</b></para>
    /// <list type="bullet">
    /// <item>这里写的是**骨骼**的世界变换（纯视觉子级），**不涉及角色根节点位置** ——
    /// 角色移动仍然只有 <c>PlayerMotor</c> 一处写入者，<c>CharacterController</c> 不受影响。</item>
    /// <item>必须用 <see cref="LateUpdate"/> 而不是 <c>Update</c>：Animator 在 Update 阶段写骨骼，
    /// 我们要排在它后面才能覆盖。</item>
    /// <item>勾了 <c>applyInEditMode</c> 时，编辑模式也会写道具骨的局部变换，
    /// 保存场景会把该值烘进场景。想还原用右键菜单「③ 还原剑骨原始局部变换」。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class PropBoneAttach : MonoBehaviour
    {
        [Header("骨骼")]
        [Tooltip("要跟随的道具骨：Bip001-Prop1（剑就蒙在这根骨上）")]
        public Transform propBone;

        [Tooltip("目标骨：Bip001-R-Hand")]
        public Transform targetBone;

        [Header("偏移（相对目标骨的局部空间）")]
        [Tooltip("位置偏移，单位米")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("角度偏移，单位度")]
        public Vector3 eulerOffset = Vector3.zero;

        [Header("调试")]
        [Tooltip("勾上：编辑模式也生效，方便在 Scene 视图里实时调；只在运行时生效就取消勾选")]
        public bool applyInEditMode = true;

        [Tooltip("原始局部变换（自动记录，供还原用；不要手改）")]
        [SerializeField] private Transform _origParent;
        [SerializeField] private Vector3 _origLocalPos;
        [SerializeField] private Vector3 _origLocalEuler;
        [SerializeField] private Vector3 _origLocalScale = Vector3.one;
        [SerializeField] private int _origSiblingIndex;
        [SerializeField] private bool _origCaptured;

        private void OnEnable()
        {
            if (_origCaptured || propBone == null)
            {
                return;
            }

            _origParent = propBone.parent;
            _origLocalPos = propBone.localPosition;
            _origLocalEuler = propBone.localRotation.eulerAngles;
            _origLocalScale = propBone.localScale;
            _origSiblingIndex = propBone.GetSiblingIndex();
            _origCaptured = true;
        }

        private void LateUpdate()
        {
            if (propBone == null || targetBone == null)
            {
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            propBone.SetPositionAndRotation(
                targetBone.TransformPoint(positionOffset),
                targetBone.rotation * Quaternion.Euler(eulerOffset));
        }

        [ContextMenu("① 从当前姿态取偏移（剑不跳位）")]
        private void CaptureOffsetFromCurrentPose()
        {
            if (propBone == null || targetBone == null)
            {
                Debug.LogWarning("[PropBoneAttach] 请先指定 propBone 与 targetBone", this);
                return;
            }

            positionOffset = targetBone.InverseTransformPoint(propBone.position);
            eulerOffset = (Quaternion.Inverse(targetBone.rotation) * propBone.rotation).eulerAngles;
            Debug.Log("[PropBoneAttach] 已取当前偏移：position = " + positionOffset.ToString("F4")
                      + "  euler = " + eulerOffset.ToString("F2"), this);
        }

        [ContextMenu("② 偏移归零（剑吸附到目标骨原点）")]
        private void ZeroOffset()
        {
            positionOffset = Vector3.zero;
            eulerOffset = Vector3.zero;
        }

        [ContextMenu("③ 还原剑骨原始局部变换")]
        private void RestoreOriginalLocal()
        {
            if (propBone == null || !_origCaptured)
            {
                Debug.LogWarning("[PropBoneAttach] 没有记录到剑骨的原始局部变换", this);
                return;
            }

            if (_origParent != null && propBone.parent != _origParent)
            {
                propBone.SetParent(_origParent, false);
            }

            propBone.localPosition = _origLocalPos;
            propBone.localRotation = Quaternion.Euler(_origLocalEuler);
            propBone.localScale = _origLocalScale;
            propBone.SetSiblingIndex(_origSiblingIndex);
            Debug.Log("[PropBoneAttach] 已还原剑骨原始局部变换", this);
        }

        private void OnValidate()
        {
            if (propBone == null || !_origCaptured)
            {
                return;
            }

            if (propBone.parent != _origParent)
            {
                Debug.LogWarning("[PropBoneAttach] 检测到 propBone 的父级已被改动，"
                                 + "如需回到原始层级请用右键菜单「③ 还原剑骨原始局部变换」", this);
            }
        }
    }
}
