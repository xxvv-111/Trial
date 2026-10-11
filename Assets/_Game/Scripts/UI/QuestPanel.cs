using System.Text;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 任务面板。**本轮只做占位** —— 用写死的假任务把
    /// "靠近告示栏 → 按 F → 打开面板 → 关闭并恢复操作"整条链路跑通。
    ///
    /// ⚠️ **TODO（留档）· 接入任务系统**：等 `QuestService` / `QuestConfig` 落地后，
    ///    把 <see cref="BuildPlaceholder"/> 整段删掉，改为订阅 `GameEvents` 的任务进度事件、
    ///    在变化时重建条目列表。
    ///    ⭐ 关键是 **改动只发生在本类内部** ——
    ///    交互层（<see cref="Gameplay.Interactable"/> / <see cref="Gameplay.InteractionDetector"/>）
    ///    与 UI 基类（<see cref="UIWindow"/>）都不用动。这就是分层的意义。
    /// </summary>
    public class QuestPanel : UIWindow
    {
        [Header("占位内容（接入 QuestService 后整段删除）")]
        [Tooltip("用来显示占位任务的 TMP 文本。留空则不填。")]
        [SerializeField] private TextMeshProUGUI _placeholderText;

        [Tooltip("占位任务条目，一行一条。")]
        [SerializeField]
        private string[] _placeholderQuests =
        {
            "Explore the castle",
            "Defeat the Boss",
        };

        /// <summary>
        /// ⚠️ 用 <c>OnEnable</c> 而不是 <c>Start</c>：面板通常在场景里被预设为 inactive，
        ///    那种物体**不会执行 Start**，用 Start 会导致第一次打开时文本是空的。
        ///    OnEnable 在每次被激活时都会跑，稳定。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            BuildPlaceholder();
        }

        /// <summary>⚠️ 占位实现 —— 接入 QuestService 后整段删除。</summary>
        private void BuildPlaceholder()
        {
            if (_placeholderText == null || _placeholderQuests == null) return;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _placeholderQuests.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(i + 1).Append(". ").Append(_placeholderQuests[i]);
            }
            _placeholderText.text = sb.ToString();
        }
    }
}
