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
        [Tooltip("单块文本容器。⚠️ 分栏布局下**留空**（改用下面的 _entryLabels / _detailBody）。")]
        [SerializeField] private TextMeshProUGUI _placeholderText;

        [Header("分栏布局（2026-10-20 重构）")]
        [Tooltip("左侧任务条目的文本，按顺序填（第 1 个对应 _placeholderQuests[0]）。留空则退回单块文本模式。")]
        [SerializeField] private TextMeshProUGUI[] _entryLabels;

        [Tooltip("右侧详情：任务标题。")]
        [SerializeField] private TextMeshProUGUI _detailTitle;

        [Tooltip("右侧详情：任务描述。")]
        [SerializeField] private TextMeshProUGUI _detailBody;

        [Tooltip("右侧详情：目标一行。")]
        [SerializeField] private TextMeshProUGUI _detailGoal;

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
            if (_placeholderQuests == null || _placeholderQuests.Length == 0) return;

            // ===== 分栏模式：左列条目 + 右侧详情 =====
            if (_entryLabels != null && _entryLabels.Length > 0)
            {
                for (int i = 0; i < _entryLabels.Length; i++)
                {
                    if (_entryLabels[i] == null) continue;
                    _entryLabels[i].text = i < _placeholderQuests.Length ? _placeholderQuests[i] : string.Empty;
                }

                if (_detailTitle != null) _detailTitle.text = _placeholderQuests[0];
            }

            if (_detailBody != null)
                _detailBody.text = "钟楼已经三天没有敲响。守夜人说，\n"
                                 + "城堡深处有点不对劲 —— 先去看看。";

            if (_detailGoal != null)
                _detailGoal.text = "目标　抵达城堡大厅";

            // ===== 兼容旧结构：单块文本 =====
            if (_placeholderText == null) return;

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
