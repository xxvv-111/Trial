using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay;
using Game.Save;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 屏幕左侧的「任务追踪」：随时能看到手上有哪些任务、各自做到哪一步。
    ///
    /// <para>━━ 显示哪些 ━━</para>
    /// <list type="bullet">
    ///   <item><c>Active</c>（进行中）—— 正常显示；</item>
    ///   <item><c>ReadyToTurnIn</c>（可交付）—— **必须显示**，否则玩家打满了却不知道该回酒馆交；</item>
    ///   <item><c>Available</c> / <c>Locked</c> 不显示 —— "能接什么"是告示板面板的职责，
    ///         常驻追踪只回答"我正在干什么"。</item>
    /// </list>
    ///
    /// <para>━━ 为什么每条任务只用一个 TMP + 富文本 ━━</para>
    /// <para>追踪条的内容就是"标题 + 若干目标"，为它单独建 prefab 反而多一层维护
    /// （改个字号要开 prefab、脚本字段还要对上）。这里直接在 <see cref="Rebuild"/> 里拼字符串，
    /// 样式全部集中在 Inspector 的四个字段上。</para>
    ///
    /// <para>⚠️ 刷新是**事件驱动**（<see cref="GameEvents.QuestsChanged"/>），不是每帧。
    /// 每帧拼字符串会产生持续 GC，这是新手最常见的性能问题。</para>
    ///
    /// <para>挂法：挂在追踪面板根节点上，<c>_root</c> 留空 = 用自身，<c>_listParent</c> 指向
    /// 带 <c>VerticalLayoutGroup</c> 的容器。</para>
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [Tooltip("整个追踪面板。没有可显示的任务时整块隐藏。留空 = 本脚本所在物体。")]
        [SerializeField] private GameObject _root;

        [Tooltip("条目容器（建议挂 VerticalLayoutGroup + childControlHeight）。")]
        [SerializeField] private RectTransform _listParent;

        [Header("样式")]
        [Tooltip("任务标题字号。")]
        [SerializeField] private float _titleSize = 19f;

        [Tooltip("目标行字号。")]
        [SerializeField] private float _goalSize = 15f;

        [Tooltip("任务标题颜色（默认 #EDE8DE 暖白）。")]
        [SerializeField] private Color _titleColor = new Color(0.929f, 0.909f, 0.871f);

        [Tooltip("目标行颜色（默认 #B6AC9A 冷灰）。")]
        [SerializeField] private Color _goalColor = new Color(0.714f, 0.675f, 0.604f);

        [Tooltip("可交付任务的标题颜色（比普通标题亮一点，作为「该回酒馆了」的提示）。")]
        [SerializeField] private Color _readyColor = new Color(1f, 0.965f, 0.886f);

        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly StringBuilder _sb = new StringBuilder(256);

        private GameObject Root { get { return _root != null ? _root : gameObject; } }

        private void OnEnable()
        {
            //⚠️ 成对订阅/退订：静态事件不随场景销毁（历史坑 T7）
            GameEvents.QuestsChanged += Rebuild;
        }

        private void OnDisable()
        {
            GameEvents.QuestsChanged -= Rebuild;
        }

        private void Start()
        {
            Rebuild();
        }

        /// <summary>按当前任务状态重建整块追踪。订阅 <c>QuestsChanged</c> 后自动调用。</summary>
        public void Rebuild()
        {
            if (_listParent == null) return;

            var svc = QuestService.Instance;
            if (svc == null || !svc.IsReady) return;   // 服务还没就绪：等下一次事件

            ClearRows();

            int shown = 0;
            var all = svc.All;
            for (int i = 0; i < all.Count; i++)
            {
                var st = all[i];
                if (st.Status != QuestStatus.Active && st.Status != QuestStatus.ReadyToTurnIn) continue;
                AddRow(st);
                shown++;
            }

            Root.SetActive(shown > 0);
        }

        private void AddRow(QuestState st)
        {
            var go = new GameObject("QuestEntry", typeof(RectTransform));
            go.transform.SetParent(_listParent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;   //⚠️ 不要直接引 sarasa（见 MEMORY 里的中文字体坑）
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.text = BuildText(st);

            _rows.Add(go);
        }

        private string BuildText(QuestState st)
        {
            bool ready = st.Status == QuestStatus.ReadyToTurnIn;

            _sb.Length = 0;

            // 标题行
            _sb.Append("<size=").Append(_titleSize.ToString("F0")).Append("><color=#")
               .Append(ColorUtility.ToHtmlStringRGB(ready ? _readyColor : _titleColor)).Append('>')
               .Append(st.Title);
            if (ready) _sb.Append("  ·  可交付");
            _sb.Append("</color></size>");

            // 目标行
            var objs = st.Objectives;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                _sb.Append('\n')
                   .Append("<size=").Append(_goalSize.ToString("F0")).Append("><color=#")
                   .Append(ColorUtility.ToHtmlStringRGB(_goalColor)).Append('>')
                   .Append("  ").Append(string.IsNullOrEmpty(o.Hint) ? o.ObjectiveId : o.Hint)
                   .Append("  ").Append(o.Progress).Append('/').Append(o.Need);
                if (o.IsDone) _sb.Append("  ✓");
                _sb.Append("</color></size>");
            }

            return _sb.ToString();
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;
                //⚠️ 先 SetActive(false) 再 Destroy：Destroy 要到帧末才生效，
                //   同帧内两次 Rebuild 会让旧条目继续挂在父节点上 ⇒ 列表翻倍（第 5 步踩过）
                _rows[i].SetActive(false);
                Destroy(_rows[i]);
            }
            _rows.Clear();
        }
    }
}
