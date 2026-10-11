using TMPro;
using UnityEngine;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 交互提示条：在屏幕上显示"按 F 查看告示栏"。
    ///
    /// ⚠️ **事件驱动，不是轮询**：订阅 <see cref="InteractionDetector.TargetChanged"/>，
    ///    只在**目标变化的那一帧**改文字。若改成每帧 `text = ...`，会持续产生字符串分配
    ///    （GC），这是新手最常见的性能问题来源。
    ///
    /// 挂法：挂在提示条 Panel 自己身上即可，<c>_panel</c> 留空 = 用自身。
    /// </summary>
    public class InteractPromptUI : MonoBehaviour
    {
        [Tooltip("提示条根节点。留空 = 本脚本所在物体。")]
        [SerializeField] private GameObject _panel;

        [Tooltip("显示文案的 TMP 文本。")]
        [SerializeField] private TextMeshProUGUI _text;

        // ==================== TODO（留档）· 键名写死 ====================
        // 目前键名硬编码成 "F"（对应 Keyboard & Mouse 的绑定）。
        // ⚠️ 手柄玩家会看到错误提示：手柄的交互键是 **北键**（Xbox 的 Y / PS 的 △），不是 F。
        //
        // 将来要做"输入图标自适应"（input glyph swapping）时，就在这里改：
        //   根据 InputSystem 当前使用的设备动态取显示名，而不是写死。
        //   可用的切入点：
        //     · InputUser.lastUsedDevice / PlayerInput.devices 判断是键盘还是手柄
        //     · 或读 `PlayerControls` 里 Interact 的 `.bindings[i].effectivePath` 反查
        //   本项目持 PlayerControls 的唯一地方是 `Game.Core.InputService`，
        //   所以正解是在 InputService 上开一个 `InteractKeyLabel` 属性，由本类读取。
        [Tooltip("键名（暂时写死）。⚠️ 见脚本内 TODO：将来改为输入图标自适应。")]
        [SerializeField] private string _keyLabel = "F";

        [Tooltip("文案模板。{key} = 键名，{action} = 交互物的 Prompt。")]
        [SerializeField] private string _format = "按 {key} {action}";

        private InteractionDetector _detector;

        private void Start()
        {
            // ⚠️ 用 FindFirstObjectByType 而不是 FindObjectOfType：后者在 Unity 6 已标记过时。
            //    只找一次 —— 检测器是场景里的常驻对象，不需要每帧找。
            _detector = FindFirstObjectByType<InteractionDetector>();
            if (_detector != null)
                _detector.TargetChanged += OnTargetChanged;
            else
                Debug.LogWarning("[InteractPromptUI] 场景里没有 InteractionDetector，提示条不会显示。");

            Refresh(null);//初始隐藏
        }

        private void OnDestroy()
        {
            //⚠️ 必须成对退订。否则切场景时，旧 UI 可能被尚未销毁的检测器回调（或反之）。
            if (_detector != null) _detector.TargetChanged -= OnTargetChanged;
        }

        private void OnTargetChanged(IInteractable target)
        {
            Refresh(target);
        }

        private void Refresh(IInteractable target)
        {
            GameObject panel = _panel != null ? _panel : gameObject;
            panel.SetActive(target != null);

            if (_text == null || target == null) return;

            _text.text = _format
                .Replace("{key}", _keyLabel)
                .Replace("{action}", target.Prompt);
        }
    }
}
