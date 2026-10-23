using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay;
using Game.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 任务面板。左边是**动态列表**（条目数随任务数变），右边是详情 + 目标清单 + 一个随状态变化的按钮。
    ///
    /// <para>数据全部来自 <see cref="QuestService"/>，本类**不做任何规则判断** ——
    /// 能不能接、能不能交，都由服务决定，这里只负责"把状态画出来、把点击转过去"。</para>
    ///
    /// <para>⚠️ 用 <c>OnEnable</c> 而不是 <c>Start</c>：面板在场景里是 inactive 的，
    /// inactive 物体不会执行 Start，用 Start 会导致第一次打开时一片空白。</para>
    ///
    /// <para>⚠️ 订阅 `QuestsChanged` 必须成对（OnEnable 订 / OnDisable 退）。
    /// 面板每次打开都会走一遍 OnEnable，只在 Awake 订会漏刷新。</para>
    /// </summary>
    public class QuestPanel : UIWindow
    {
        [Header("列表（动态生成）")]
        [Tooltip("行的父物体，必须是带 VerticalLayoutGroup 的容器。")]
        [SerializeField] private RectTransform _rowParent;

        [Tooltip("行预制体：Prefabs/UI/Row_Quest.prefab。")]
        [SerializeField] private GameObject _rowPrefab;

        [Header("右侧详情")]
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailBody;
        [SerializeField] private TextMeshProUGUI _detailGoal;

        [Header("操作按钮")]
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        [Header("旧结构（会被自动隐藏，留字段只是为了不丢引用）")]
        [SerializeField] private TextMeshProUGUI[] _entryLabels;

        static readonly Color ColorText = new Color(0.929f, 0.909f, 0.871f);   // #EDE8DE
        static readonly Color ColorMuted = new Color(0.714f, 0.675f, 0.604f);  // #B6AC9A

        readonly List<GameObject> _rows = new List<GameObject>();
        string _selectedId;

        protected override void OnEnable()
        {
            base.OnEnable();

            HideLegacyLabels();
            GameEvents.QuestsChanged += Rebuild;   // 任务数据一变就刷新
            Rebuild();
        }

        private void OnDisable()
        {
            GameEvents.QuestsChanged -= Rebuild;
        }

        // ───────────────────────── 列表 ─────────────────────────

        void Rebuild()
        {
            ClearRows();

            var service = QuestService.Instance;
            if (service == null || !service.IsReady)
            {
                ShowDetailNone("任务系统还未就绪");
                return;
            }

            if (service.All.Count == 0)
            {
                ShowDetailNone("暂无任务");
                return;
            }

            // 选中项：保持原选择；原来的没有了就选第一条
            if (string.IsNullOrEmpty(_selectedId) || service.Find(_selectedId) == null)
                _selectedId = service.All[0].QuestId;

            if (_rowPrefab != null && _rowParent != null)
            {
                for (int i = 0; i < service.All.Count; i++)
                    CreateRow(service.All[i]);
            }

            RefreshDetail();
        }

        void CreateRow(QuestState state)
        {
            var go = Instantiate(_rowPrefab, _rowParent);
            go.name = "Row_" + state.QuestId;
            _rows.Add(go);

            var title = go.transform.Find("Text_RowTitle") != null
                ? go.transform.Find("Text_RowTitle").GetComponent<TextMeshProUGUI>() : null;
            var stateText = go.transform.Find("Text_RowState") != null
                ? go.transform.Find("Text_RowState").GetComponent<TextMeshProUGUI>() : null;
            var line = go.transform.Find("Line_Row") != null
                ? go.transform.Find("Line_Row").GetComponent<Image>() : null;

            bool selected = state.QuestId == _selectedId;
            bool dim = state.Status == QuestStatus.Locked;

            if (title != null)
            {
                title.text = (state.IsMainQuest ? "【主线】" : string.Empty) + state.Title;
                title.color = dim ? ColorMuted : ColorText;
                title.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }

            if (stateText != null)
            {
                stateText.text = StatusText(state.Status);
                stateText.color = dim ? new Color(ColorMuted.r, ColorMuted.g, ColorMuted.b, 0.6f) : ColorMuted;
            }

            if (line != null)
            {
                // 选中：细线提亮；锁定：更暗；其余：默认
                var a = selected ? 0.95f : (dim ? 0.10f : 0.28f);
                line.color = new Color(ColorText.r, ColorText.g, ColorText.b, a);
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                var id = state.QuestId;
                button.onClick.AddListener(() => Select(id));
            }
        }

        void ClearRows()
        {
            // ⚠️ Destroy 要到帧末才真正移除，同一帧内若 Rebuild 被调用两次（Open + QuestsChanged），
            //    旧行仍然挂在父节点上 ⇒ 列表会瞬间翻倍。所以先 SetActive(false) 让它立刻退出布局。
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;
                _rows[i].SetActive(false);
                Destroy(_rows[i]);
            }
            _rows.Clear();
        }

        void Select(string questId)
        {
            _selectedId = questId;
            Rebuild();   // 行数很少，直接重建，避免维护"只改颜色"的第二套逻辑
        }

        // ───────────────────────── 详情 ─────────────────────────

        void RefreshDetail()
        {
            var service = QuestService.Instance;
            var state = service != null ? service.Find(_selectedId) : null;
            if (state == null) { ShowDetailNone("暂无任务"); return; }

            if (_detailTitle != null)
            {
                _detailTitle.text = state.Title;
                _detailTitle.color = ColorText;
            }

            if (_detailBody != null)
            {
                var sb = new StringBuilder();
                if (state.Config != null && !string.IsNullOrEmpty(state.Config.description))
                    sb.Append(state.Config.description).Append("\n\n");

                for (int i = 0; i < state.Objectives.Count; i++)
                {
                    var o = state.Objectives[i];
                    sb.Append(o.IsDone ? "✓ " : "· ").Append(o.Hint)
                      .Append("　").Append(o.Progress).Append('/').Append(o.Need);
                    if (i < state.Objectives.Count - 1) sb.Append('\n');
                }
                _detailBody.text = sb.ToString();
            }

            if (_detailGoal != null)
            {
                var sb = new StringBuilder();
                sb.Append("奖励　").Append(RewardText(state.Config));
                sb.Append('\n').Append("状态　").Append(StatusText(state.Status));

                if (state.Status == QuestStatus.Locked)
                {
                    var missing = MissingPrerequisitesText(state.Config);
                    if (!string.IsNullOrEmpty(missing)) sb.Append('\n').Append("需先完成　").Append(missing);
                }
                _detailGoal.text = sb.ToString();
            }

            RefreshActionButton(state);
        }

        void ShowDetailNone(string message)
        {
            if (_detailTitle != null) _detailTitle.text = string.Empty;
            if (_detailBody != null) _detailBody.text = message;
            if (_detailGoal != null) _detailGoal.text = string.Empty;

            if (_actionButton != null) _actionButton.gameObject.SetActive(false);
        }

        void RefreshActionButton(QuestState state)
        {
            if (_actionButton == null) return;

            _actionButton.gameObject.SetActive(true);
            _actionButton.onClick.RemoveAllListeners();

            switch (state.Status)
            {
                case QuestStatus.Available:
                    SetAction("接取", true);
                    _actionButton.onClick.AddListener(() => QuestService.Instance.Accept(state.QuestId));
                    break;

                case QuestStatus.ReadyToTurnIn:
                    SetAction("交付", true);
                    _actionButton.onClick.AddListener(() => QuestService.Instance.TurnIn(state.QuestId));
                    break;

                case QuestStatus.Active:
                    SetAction("进行中", false);
                    break;

                case QuestStatus.Completed:
                    SetAction("已完成", false);
                    break;

                default:
                    SetAction("未解锁", false);
                    break;
            }
        }

        void SetAction(string label, bool interactable)
        {
            if (_actionLabel != null) _actionLabel.text = label;
            _actionButton.interactable = interactable;
        }

        // ───────────────────────── 文案 ─────────────────────────

        static string StatusText(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Available: return "可接取";
                case QuestStatus.Active: return "进行中";
                case QuestStatus.ReadyToTurnIn: return "可交付";
                case QuestStatus.Completed: return "已完成";
                default: return "未解锁";
            }
        }

        static string RewardText(QuestConfig config)
        {
            if (config == null || config.rewards == null || config.rewards.Count == 0) return "无";

            var sb = new StringBuilder();
            for (int i = 0; i < config.rewards.Count; i++)
            {
                var r = config.rewards[i];
                if (r == null || r.amount <= 0) continue;
                if (sb.Length > 0) sb.Append("　");

                switch (r.kind)
                {
                    case RewardKind.Gold: sb.Append(r.amount).Append(" 金币"); break;
                    case RewardKind.MagicCrystal: sb.Append(r.amount).Append(" 魔晶"); break;
                    case RewardKind.Spell: sb.Append("法术 ").Append(r.id); break;
                    case RewardKind.Equipment: sb.Append("装备 ").Append(r.id).Append(" +").Append(r.amount); break;
                }
            }
            return sb.Length == 0 ? "无" : sb.ToString();
        }

        string MissingPrerequisitesText(QuestConfig config)
        {
            if (config == null || config.prerequisites == null) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < config.prerequisites.Count; i++)
            {
                var id = config.prerequisites[i];
                if (string.IsNullOrEmpty(id)) continue;

                var service = QuestService.Instance;
                var state = service != null ? service.Find(id) : null;
                if (state != null && state.Status == QuestStatus.Completed) continue;

                if (sb.Length > 0) sb.Append("、");
                sb.Append(state != null ? state.Title : id);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 隐藏旧的固定条目文本。它们已经被动态列表取代，但引用还留在场景里 ——
        /// 不隐藏的话会和生成的行走重叠。
        /// </summary>
        void HideLegacyLabels()
        {
            if (_entryLabels == null) return;
            for (int i = 0; i < _entryLabels.Length; i++)
                if (_entryLabels[i] != null) _entryLabels[i].gameObject.SetActive(false);
        }
    }
}
