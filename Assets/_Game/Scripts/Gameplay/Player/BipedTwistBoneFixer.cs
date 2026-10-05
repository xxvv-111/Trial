using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 修复 3ds Max Biped **扭转辅助骨**在 Humanoid 下不被驱动、导致手腕/前臂网格撕裂的问题。
    ///
    /// 病因（2026-10-05 实测）
    /// ------------------------------------------------------------------
    /// Roskva 的 `Upper Body` 网格蒙皮依赖 4 根 Biped 扭转骨：
    ///
    ///     Bip001-L/R-ForeTwist    父级 = Bip001-L/R-UpperArm   总权重 57.5（329 顶点）
    ///     Bip001-L/R-ForeTwist1   父级 = Bip001-*-ForeTwist    总权重 20.3（228 顶点）
    ///
    /// ⚠️ 注意它们挂的是 **UpperArm**，是 `Bip001-*-Forearm` 的**兄弟而非子级**。
    /// 原本在 3ds Max 里由 Orientation Constraint 跟随前臂拧动；转成 Humanoid 后
    /// 这 4 根骨**没有任何一条 human 映射**，而 Humanoid 的 Animator **只驱动映射过的骨**
    /// → 前臂转 14.87° 时它们原地不动 → 手腕区域被撕扯
    /// （实测最大位移 45 mm，而手腕半径仅约 30 mm）。
    ///
    /// ⚠️ 不要试图用「把扭转骨填进 Avatar 的 Left/Right Lower Arm Twist 槽位」来解决 ——
    /// 实测**无效**（填了之后 ForeTwist 依然是 0.00°）：动画是在没有扭转骨的骨架上做的，
    /// muscle 里根本没有扭转数据，Unity 不会凭映射凭空造出运动。只能在这里手动补驱动。
    ///
    /// 修法
    /// ------------------------------------------------------------------
    /// 让每根扭转骨**刚性跟随对应前臂**，保持绑定姿态下的相对旋转：
    ///
    ///     扭转骨.rotation = 前臂.rotation * offset
    ///     offset = rot(.bindpose 前臂)⁻¹ * rot(.bindpose 扭转骨)
    ///
    /// ⚠️ `offset` 从 `SkinnedMeshRenderer` 的 **bindpose** 反算，而不是在 `Awake` 里
    /// 读当时的 `rotation` —— 后者依赖「唤醒时 Animator 还没播过动画」这个时序假设，
    /// 一旦 Animator 先跑过一帧，读到就是动画姿态，offset 会算错。
    /// bindpose 是资产里的常量，与运行时无关。**在绑定姿态下本组件是严格的 no-op。**
    ///
    /// **位置不用管**：扭转骨与前臂同为 `UpperArm` 的子级、`localPosition` 恒定，
    /// UpperArm 一转两者刚性跟随，相对位置永远正确 —— 只有旋转会脱节。
    ///
    /// ⚠️ 必须在 **LateUpdate** 写（Animator 之后），否则会被动画覆盖。
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class BipedTwistBoneFixer : MonoBehaviour
    {
        /// <summary>扭转骨名字里必须出现的片段。</summary>
        const string TWIST_TOKEN = "ForeTwist";

        /// <summary>
        /// Blender 导出时自动生成的叶子骨后缀。⚠️ 持剑版 FBX 里存在
        /// `Bip001-R-ForeTwist1_end` / `_end_end` —— 名字里也含 "ForeTwist"，
        /// 但蒙皮权重为 0、纯属导出残留，必须排除（否则持剑版会被匹配成 8 对而非 4 对）。
        /// </summary>
        const string END_SUFFIX = "_end";

        const string FOREARM_FMT = "Bip001-{0}-Forearm";

        [Tooltip("收集到的骨骼对数量，只读，供排查用。")]
        [SerializeField] private int _pairCount;

        [Tooltip("勾上则在 Rebuild 时打印每对骨骼的名字与 offset 角度。")]
        [SerializeField] private bool _logOnRebuild;

        /// <summary>被驱动的扭转骨。</summary>
        private Transform[] _twists;
        /// <summary>对应的前臂（驱动源）。</summary>
        private Transform[] _forearms;
        /// <summary>绑定姿态下「前臂 → 扭转骨」的相对旋转。</summary>
        private Quaternion[] _offsets;

        private void Awake() { Rebuild(); }

        private void LateUpdate() { ApplyFix(); }

        // ==================================================================
        //  收集
        // ==================================================================
        /// <summary>重新收集「前臂 → 扭转骨」对并计算绑定偏移。模型换掉后可再调一次。</summary>
        public void Rebuild()
        {
            _twists = null;
            _forearms = null;
            _offsets = null;
            _pairCount = 0;

            var all = GetComponentsInChildren<Transform>(true);
            var twists = new List<Transform>();
            var forearms = new List<Transform>();
            var offsets = new List<Quaternion>();
            var warned = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                var tw = all[i];
                if (!IsTwistBone(tw.name)) continue;

                string side = SideOf(tw.name);
                if (side == null) { warned.Add(tw.name + "(分不出左右)"); continue; }

                var fa = FindByName(all, string.Format(FOREARM_FMT, side));
                if (fa == null) { warned.Add(tw.name + "(找不到 " + side + " 侧前臂)"); continue; }

                Quaternion off;
                if (!TryGetBindOffset(fa, tw, out off))
                {
                    warned.Add(tw.name + "(bindpose 里查不到这对骨)");
                    continue;
                }

                twists.Add(tw);
                forearms.Add(fa);
                offsets.Add(off);
            }

            _twists = twists.ToArray();
            _forearms = forearms.ToArray();
            _offsets = offsets.ToArray();
            _pairCount = _twists.Length;

            if (_logOnRebuild)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[BipedTwistBoneFixer] " + name + " 收集到 " + _pairCount + " 对:");
                for (int i = 0; i < _pairCount; i++)
                    sb.AppendLine("  " + _twists[i].name + "  ←  " + _forearms[i].name +
                                  "   offset=" + Quaternion.Angle(Quaternion.identity, _offsets[i]).ToString("F2") + "°");
                for (int i = 0; i < warned.Count; i++) sb.AppendLine("  跳过: " + warned[i]);
                Debug.Log(sb.ToString());
            }
        }

        // ==================================================================
        //  施加
        // ==================================================================
        /// <summary>把每根扭转骨对齐到"刚性跟随对应前臂"的姿态。绑定姿态下是 no-op。</summary>
        public void ApplyFix()
        {
            if (_twists == null || _twists.Length == 0) return;
            for (int i = 0; i < _twists.Length; i++)
            {
                var tw = _twists[i];
                var fa = _forearms[i];
                // 骨架可能在运行中被替换
                if (tw == null || fa == null) { Rebuild(); return; }
                tw.rotation = fa.rotation * _offsets[i];
            }
        }

        // ==================================================================
        //  工具
        // ==================================================================
        /// <summary>是不是真正要驱动的扭转骨（排除 Blender 的 `_end` 叶子骨）。</summary>
        static bool IsTwistBone(string boneName)
        {
            if (boneName.IndexOf(TWIST_TOKEN, System.StringComparison.Ordinal) < 0) return false;
            // `_end_end` 也以 `_end` 结尾，一次判断即可
            return !boneName.EndsWith(END_SUFFIX, System.StringComparison.Ordinal);
        }

        /// <summary>从名字里判断左右（只认 Biped 的 `-L-` / `-R-` 形式）。</summary>
        static string SideOf(string boneName)
        {
            if (boneName.IndexOf("-L-", System.StringComparison.Ordinal) >= 0) return "L";
            if (boneName.IndexOf("-R-", System.StringComparison.Ordinal) >= 0) return "R";
            return null;
        }

        static Transform FindByName(Transform[] all, string boneName)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == boneName) return all[i];
            return null;
        }

        /// <summary>
        /// 由 bindpose 反算「前臂 → 扭转骨」的绑定相对旋转。
        ///
        /// bindpose[i] = 该骨的 worldToLocal（在渲染器空间），故其逆即绑定姿态下的 localToWorld。
        /// 取旋转并对两者作相对：<c>offset = rot(bindpose[前臂]⁻¹)⁻¹ * rot(bindpose[扭转骨]⁻¹)</c>。
        /// ⚠️ 渲染器自身的 localToWorld 在相对量里会约掉，所以**不需要**引入 renderer 变换。
        /// </summary>
        static bool TryGetBindOffset(Transform forearm, Transform twist, out Quaternion offset)
        {
            offset = Quaternion.identity;
            var root = forearm.root;
            if (root == null) return false;

            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int s = 0; s < smrs.Length; s++)
            {
                var mesh = smrs[s].sharedMesh;
                if (mesh == null) continue;
                var bones = smrs[s].bones;
                var bindposes = mesh.bindposes;
                if (bindposes == null || bindposes.Length != bones.Length) continue;

                int ia = IndexOf(bones, forearm);
                int ib = IndexOf(bones, twist);
                if (ia < 0 || ib < 0) continue;

                Quaternion ra = bindposes[ia].inverse.rotation.normalized;
                Quaternion rb = bindposes[ib].inverse.rotation.normalized;
                offset = Quaternion.Inverse(ra) * rb;
                return true;
            }
            return false;
        }

        static int IndexOf(Transform[] bones, Transform target)
        {
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] == target) return i;
            return -1;
        }
    }
}
