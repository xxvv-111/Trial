using TMPro;
using UnityEngine;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 交互提示条：在屏幕上显示"按 F 查看告示栏"。
    ///
    /// ⚠️ **正文是事件驱动，不是轮询**：订阅 <see cref="InteractionDetector.TargetChanged"/>，
    ///    只在**目标变化的那一帧**改文字。若改成每帧 `text = ...`，会持续产生字符串分配
    ///    （GC），这是新手最常见的性能问题来源。
    ///
    /// ⚠️ <see cref="Update"/> 里只做一件很轻的事：**面板打开时把提示条藏起来**
    ///    —— 否则暂停菜单 / 任务面板背后会露出"按 F 查看告示栏"，很出戏。
    ///    判据用 `InputService.GameplayInputEnabled`：暂停面板与 UIWindow 打开时都会把它关掉，
    ///    一处判据覆盖所有弹窗。**只在状态变化的那一帧 SetActive**，不产生 GC。
    ///
    /// 挂法：挂在提示条 Panel 自己身上即可，<c>_panel</c> 留空 = 用自身。
    /// ⚠️ **该 Panel 必须保持 active**（否则本脚本的 Start 不执行、收不到事件、提示条永不出现）。
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

        [Tooltip("目标**不可交互但仍要说明原因**时（例：大门锁着 → 暂无任务地点）的暗色文字。\n" +
                 "可交互时自动用 TMP 上原本的颜色。见 IInteractableHint。")]
        [SerializeField] private Color _disabledColor = new Color(0.56f, 0.51f, 0.44f, 1f);

        private InteractionDetector _detector;

        /// <summary>TMP 上原本的文字颜色（运行时记下，解锁后复原用）。</summary>
        private Color _normalColor = Color.white;

        /// <summary>当前交互目标（null = 附近没有可交互物）。</summary>
        private IInteractable _target;

        /// <summary>上一次刷进提示条时目标的可用状态（见 <see cref="RefreshIfChanged"/>）。</summary>
        private bool _shownUsable;

        /// <summary>上一次刷进提示条的动作文字（按**引用**比较，由 SetPrompt 换新实例）。</summary>
        private string _shownAction;

        private GameObject Panel { get { return _panel != null ? _panel : gameObject; } }

        private void Start()
        {
            if (_text != null) _normalColor = _text.color;

            // ⚠️ 用 FindFirstObjectByType 而不是 FindObjectOfType：后者在 Unity 6 已标记过时。
            //    只找一次 —— 检测器是场景里的常驻对象，不需要每帧找。
            _detector = FindFirstObjectByType<InteractionDetector>();
            if (_detector != null)
            {
                _detector.TargetChanged += OnTargetChanged;

                //⚠️⚠️ **订阅后必须主动同步一次当前目标**（2026-10-20 补）：
                //   检测器可能在**本脚本 Start 之前**就已经 Rescan 过一次（进场时玩家就站在触发器里），
                //   那次 `TargetChanged` 没有订阅者 ⇒ 事件丢失 ⇒ 提示条永远不出现。
                //   直接读一次 `CurrentTarget` 补上，成本为零。
                Refresh(_detector.CurrentTarget);
            }
            else
            {
                Debug.LogWarning("[InteractPromptUI] 场景里没有 InteractionDetector，提示条不会显示。");
                Refresh(null);//初始隐藏
            }
        }

        private void Update()
        {
            //只在"面板开关"状态变化的那一帧改 SetActive，稳定零分配
            InputService input = InputService.Instance;
            bool blocked = input != null && !input.GameplayInputEnabled;

            bool want = _target != null && !blocked;
            GameObject panel = Panel;
            if (panel != null && panel.activeSelf != want) panel.SetActive(want);

            //⭐ 目标没换、但**它自己变了**：接取 / 交付任务会让大门上锁或解锁、
            //   文案从「暂无任务地点」变成「前往覆雪林地」。InteractionDetector 只在
            //   "选中的对象发生更换"时发事件，这种情况收不到通知，只能自己比一次。
            //   ⚠️ 比较的是 bool 与字符串**引用**（SetPrompt 换的才是新实例），不产生 GC。
            if (!blocked && _text != null && _target != null) RefreshIfChanged(_target);
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

        /// <summary>目标的可用性 / 文案变了才重刷，避免每帧改 TMP 产生 GC。</summary>
        private void RefreshIfChanged(IInteractable target)
        {
            bool usable = target.CanInteract;
            string action = ActionOf(target);

            if (usable == _shownUsable && ReferenceEquals(action, _shownAction)) return;

            _shownUsable = usable;
            _shownAction = action;

            if (string.IsNullOrEmpty(action))
            {
                //不可交互又没有说明 ⇒ 不该显示（理论上检测器已排除，这里兜底）
                GameObject panel = Panel;
                if (panel != null && panel.activeSelf) panel.SetActive(false);
                return;
            }

            ApplyText(usable, action);
        }

        /// <summary>要显示的动作文字：可交互用物体自己的 <see cref="IInteractable.Prompt"/>，
        /// 锁着的用 <see cref="IInteractableHint.DisabledHint"/>（例："暂无任务地点"）。</summary>
        private static string ActionOf(IInteractable target)
        {
            if (target.CanInteract) return target.Prompt;

            IInteractableHint hint = target as IInteractableHint;
            return hint != null ? hint.DisabledHint : null;
        }

        private void Refresh(IInteractable target)
        {
            _target = target;

            bool usable = target != null && target.CanInteract;
            string action = target != null ? ActionOf(target) : null;
            _shownUsable = usable;
            _shownAction = action;

            GameObject panel = Panel;
            if (panel != null) panel.SetActive(target != null && IsGameplayInputOn());

            if (_text == null || string.IsNullOrEmpty(action)) return;

            ApplyText(usable, action);
        }

        private void ApplyText(bool usable, string action)
        {
            if (_text == null) return;

            _text.text = _format
                .Replace("{key}", _keyLabel)
                .Replace("{action}", action);

            //可交互 = 正常色；锁着 = 暗色（文案本身已说明原因）
            _text.color = usable ? _normalColor : _disabledColor;
        }

        private static bool IsGameplayInputOn()
        {
            InputService input = InputService.Instance;
            return input == null || input.GameplayInputEnabled;
        }
    }
}
