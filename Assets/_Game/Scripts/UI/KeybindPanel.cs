using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 主菜单「按键说明」面板 —— 只负责**打开 / 关闭**，不做改键。
    ///
    /// <para>挂在 <c>Canvas</c> 上（必须常驻激活，否则 <see cref="Update"/> 不跑、ESC 关不掉），
    /// 面板本体由 <c>_panel</c> 指向 <c>Panel_Keybinds</c>，场景里**预设 inactive**。</para>
    ///
    /// <para>⚠️ 面板根节点铺满全屏且 <c>raycastTarget = true</c>，靠「最上层吃掉射线」挡住
    /// 身后的开始 / 退出按钮 —— 因此面板必须是 Canvas 的**最后一个子物体**
    /// （<see cref="Open"/> 里会再 <c>SetAsLastSibling</c> 一次，防止后来新增的节点压上去）。</para>
    ///
    /// <para>📌 后续做「改键」时的接口约定：每行结构固定为
    /// <c>Content/Row_&lt;Id&gt;/Label</c>（中文名）与 <c>Content/Row_&lt;Id&gt;/Key</c>（显示用键名），
    /// 行 Id 见下表 —— 改绑时只需要重写 <c>Key</c> 那一行的文字，不必重建面板。</para>
    /// <list type="table">
    ///   <item><term>Row_Move</term><description>移动（W A S D）</description></item>
    ///   <item><term>Row_Sprint</term><description>奔跑（Shift 按住）</description></item>
    ///   <item><term>Row_Jump</term><description>跳跃（空格）</description></item>
    ///   <item><term>Row_Dash</term><description>翻滚（鼠标右键）</description></item>
    ///   <item><term>Row_Look</term><description>视角（鼠标移动）</description></item>
    ///   <item><term>Row_Zoom</term><description>镜头远近（滚轮）</description></item>
    ///   <item><term>Row_Attack</term><description>普通攻击（鼠标左键）</description></item>
    ///   <item><term>Row_Special</term><description>火球（1）</description></item>
    ///   <item><term>Row_Interact</term><description>交互（F）</description></item>
    ///   <item><term>Row_Pause</term><description>暂停（Esc）</description></item>
    /// </list>
    ///
    /// <para>生成器：<c>Tools/UI/生成键位面板 (MainMenu)</c>（<c>MainMenuUIBuilder</c>）。</para>
    /// </summary>
    public class KeybindPanel : MonoBehaviour
    {
        [Tooltip("面板根节点（Panel_Keybinds）。场景里预设 inactive，由本类负责开关。")]
        [SerializeField] private GameObject _panel;

        [Tooltip("打开时是否允许 ESC 关闭。")]
        [SerializeField] private bool _closeOnEsc = true;

        /// <summary>面板当前是否打开。</summary>
        public bool IsOpen { get { return _panel != null && _panel.activeSelf; } }

        private void Start()
        {
            //⚠️ 双保险：生成器已经把它设成 inactive，这里再关一次，
            //   防止有人手动在 Inspector 里点亮导致进游戏闪一下。
            if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
        }

        private void Update()
        {
            if (!_closeOnEsc || !IsOpen) return;

            //⚠️ 用旧版 Input 而不是 InputSystem：与 PauseMenu 的处理方式保持一致
            //   （项目 Active Input Handling = Both，不必往 .inputactions 里加会跟 UI Cancel 撞车的映射）。
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>打开面板。挂到「设置」按钮的 OnClick 上。</summary>
        public void Open()
        {
            if (_panel == null)
            {
                Debug.LogWarning("[KeybindPanel] 没有指定 _panel，无法打开按键面板。", this);
                return;
            }

            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();//保证盖住主菜单按钮（含运行时新增的节点）

            //主菜单不锁指针，但从游戏里退回主菜单时指针可能还是 Locked（状态是全局的）
            //—— 这里顺手放一次，否则「面板看得见、按钮点不动」。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>关闭面板。挂到面板里「返回」按钮的 OnClick 上。</summary>
        public void Close()
        {
            if (_panel == null) return;
            _panel.SetActive(false);
        }

        /// <summary>开关切换（留给将来做快捷键用）。</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>
        /// 按行 Id 取「键名」那一行文字（例如 <c>FindKeyLabel("Move")</c>）。
        /// 现在没人调用，是给**将来的改键功能**准备的入口 —— 改绑成功后写回这一行即可。
        /// </summary>
        public TextMeshProUGUI FindKeyLabel(string rowId)
        {
            if (_panel == null || string.IsNullOrEmpty(rowId)) return null;

            var t = _panel.transform.Find("Content/Row_" + rowId + "/Key");
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
