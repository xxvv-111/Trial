using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Data
{
    /// <summary>
    /// **单个场景的展示信息**。由 <see cref="SceneCatalog"/> 统一登记。
    /// </summary>
    [Serializable]
    public class SceneEntry
    {
        [Tooltip("场景文件名（不含 .unity），必须与 Build Settings 里完全一致。\n" +
                 "⭐ 这是**稳定 key**：任务里的「地点」、大门的「目标场景」都引用它。")]
        public string sceneName;

        [Tooltip("显示名。暂停面板的「当前位置」用它（例：酒馆）。")]
        public string displayName;

        [Tooltip("副标题，一般写「英文名 · 区域类型」（例：TAVERN　·　安全区域）。")]
        public string subtitle;

        [Tooltip("是否安全区（酒馆）。留给以后：切换按键提示 / 存档规则 / 是否允许脱战回血等。")]
        public bool isSafeZone;
    }

    /// <summary>
    /// ⭐ **场景注册表** —— 「场景 → 展示信息」的**单一真相源**。
    ///
    /// <para><b>为什么要有它</b>（2026-10-22 定）：暂停面板已经抽成共用预制体
    /// <c>PausePanel.prefab</c>，三个场景共用一份 ⇒ **在场景里手改文本会串到所有场景**。
    /// 所以「当前位置」这类随场景变化的内容必须**运行时按当前场景查表**得到。</para>
    ///
    /// <para><b>为什么是 ScriptableObject 而不是「每场景放一个组件」</b>：</para>
    /// <list type="number">
    ///   <item>任务的「地点」需要一个**能被数据资产引用**的标识 —— 场景里的组件是场景对象，
    ///         <c>QuestDefinition.asset</c> 引用不了它，只能用字符串，于是变成两份数据；</item>
    ///   <item>暂停面板（当前位置）、大门（目标地点）、任务面板（任务地点）三处需要**同一份**名称映射；</item>
    ///   <item>可以写编辑器校验：「Build Settings 里每个场景都登记了吗」。散在场景里漏了就静默显示空。</item>
    /// </list>
    ///
    /// <para>⚠️ **新增场景记得来登记**。查不到时 <see cref="SceneInfoDisplay"/> 会退回显示原始
    /// <c>sceneName</c> 并打一条 warning，不至于白屏。</para>
    ///
    /// <para>⚠️ 本表只放**静态**信息。任务的「当前进行中」属于运行时状态，应该放
    /// <c>QuestService</c>（跨场景单例），不要塞进这里。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SceneCatalog", menuName = "Game/SceneCatalog")]
    public class SceneCatalog : ScriptableObject
    {
        [Tooltip("本项目所有场景的展示信息。新增场景必须在这里登记一条。")]
        [SerializeField] private SceneEntry[] _entries = new SceneEntry[0];

        /// <summary>全部条目（只读，给编辑器校验 / 以后做关卡选择用）。</summary>
        public IReadOnlyList<SceneEntry> All { get { return _entries; } }

        /// <summary>按场景文件名查（忽略大小写）。找不到返回 null。</summary>
        public SceneEntry Find(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _entries == null) return null;

            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (e != null && !string.IsNullOrEmpty(e.sceneName)
                    && string.Equals(e.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            return null;
        }

        /// <summary>查**当前活动场景**的条目。找不到返回 null（调用方自行兜底）。</summary>
        public SceneEntry FindCurrent()
        {
            return Find(SceneManager.GetActiveScene().name);
        }

        /// <summary>按场景文件名取显示名；查不到就退回原始场景名（不返回空字符串）。</summary>
        public string DisplayNameOf(string sceneName)
        {
            var e = Find(sceneName);
            return e != null && !string.IsNullOrEmpty(e.displayName) ? e.displayName : sceneName;
        }

        /// <summary>按场景文件名取副标题；查不到返回空串。</summary>
        public string SubtitleOf(string sceneName)
        {
            var e = Find(sceneName);
            return e != null ? e.subtitle : string.Empty;
        }
    }
}
