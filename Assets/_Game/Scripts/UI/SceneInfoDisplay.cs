using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 把**当前场景**的展示信息填到 UI 上（暂停面板的「当前位置」那一块）。
    ///
    /// <para>⭐ 挂在 <c>PausePanel.prefab</c> 的**根**上（和 <see cref="PauseMenu"/> 同一物体）。
    /// 面板是三场景共用的预制体，所以文本**不能在场景里手改** —— 必须运行时查
    /// <see cref="SceneCatalog"/>。</para>
    ///
    /// <para>⚠️ <c>_catalog</c> 是 **ScriptableObject 引用**，不是场景对象 ⇒
    /// 预制体里保存它是安全的（这正是选 SO 而不是"每场景放个组件"的关键原因之一）。</para>
    ///
    /// <para>刷新时机：<see cref="OnEnable"/> 一次即可 —— 换场景必然重建 HUD 层级，
    /// 一定会再走一遍 OnEnable。另外把 <see cref="Refresh"/> 开成 public，
    /// 以后"同一场景内目标会变"的场合（例如任务改了地点）可以主动再刷。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneInfoDisplay : MonoBehaviour
    {
        [Tooltip("场景注册表。⚠️ 必填：漏了就只能退回显示原始场景名。")]
        [SerializeField] private SceneCatalog _catalog;

        [Tooltip("暂停面板里的「酒馆」那行（PausePanel/Content/Text_InfoScene）。")]
        [SerializeField] private TextMeshProUGUI _sceneTitle;

        [Tooltip("暂停面板里的「TAVERN　·　安全区域」那行（PausePanel/Content/Text_InfoSub）。")]
        [SerializeField] private TextMeshProUGUI _sceneSubtitle;

        private bool _warned;

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>按当前场景重新填文本。可重复调用。</summary>
        public void Refresh()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            if (_catalog == null)
            {
                //没配表：不阻塞游戏，退回显示场景名，只提示一次
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[SceneInfoDisplay] 没有指定 SceneCatalog，" +
                                     "暂停面板会退化为显示场景文件名（" + sceneName + "）。", this);
                }
                Apply(sceneName, string.Empty);
                return;
            }

            var entry = _catalog.Find(sceneName);

            if (entry == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[SceneInfoDisplay] SceneCatalog 里没有登记场景 \"" + sceneName +
                                     "\" —— 请在 SceneCatalog.asset 里补一条（否则暂停面板显示的是英文场景名）。", this);
                }
                Apply(sceneName, string.Empty);
                return;
            }

            Apply(string.IsNullOrEmpty(entry.displayName) ? sceneName : entry.displayName, entry.subtitle);
        }

        private void Apply(string title, string subtitle)
        {
            if (_sceneTitle != null) _sceneTitle.text = title;
            if (_sceneSubtitle != null) _sceneSubtitle.text = subtitle;
        }
    }
}
