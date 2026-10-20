using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 酒馆 / 场景内 UI 的构建基座：配色、字体、建节点的公共方法。
    ///
    /// <para>⚠️ 与 <c>MainMenuUIBuilder</c> 里的同名方法是**同源副本**（那边为了不动已验证的主菜单，
    /// 暂时没合并过来）。改配色 / 改线条粗细时，**两处都要改**。</para>
    ///
    /// <para>配色沿用 <c>Docs/UI-STYLE.md</c> v4：暖白 <c>#EDE8DE</c> + 冷灰 <c>#B6AC9A</c>，
    /// **不用金色**。游戏内场景与主菜单的唯一差别是：多了"分栏"和"信息层"，
    /// 但**依然没有面板框 / 没有圆角 / 没有材质装饰**。</para>
    /// </summary>
    public static class UIBuilderKit
    {
        // ==================== 配色 ====================
        public static readonly Color TextNormal = new Color(0.90f, 0.87f, 0.80f, 1f);   // 暖白
        public static readonly Color TextDim = new Color(0.58f, 0.55f, 0.50f, 1f);      // 冷灰（次要信息）
        public static readonly Color KeyColor = new Color(0.72f, 0.69f, 0.63f, 1f);     // 键名 / 右列
        public static readonly Color GroupColor = new Color(0.66f, 0.62f, 0.55f, 1f);   // 分组小标题
        public static readonly Color LineNormal = new Color(0.55f, 0.52f, 0.47f, 1f);
        public static readonly Color LineDim = new Color(0.28f, 0.28f, 0.27f, 1f);
        public static readonly Color HairLine = new Color(0.55f, 0.52f, 0.47f, 0.30f);
        public static readonly Color MaskBg = new Color(0.012f, 0.014f, 0.016f, 0.93f); // 全屏遮罩
        public static readonly Color PromptBg = new Color(0.020f, 0.018f, 0.016f, 0.55f);// 交互提示底（很淡的扁条）

        // ==================== 尺寸 ====================
        public const float BtnW = 240f;
        public const float BtnH = 56f;

        // ==================== 字体 ====================
        /// <summary>
        /// 统一用 <see cref="TMP_Settings.defaultFontAsset"/>（LiberationSans SDF），中文交给它的 fallback 链。
        /// ⚠️ **不要直指 sarasa SDF** —— 那套字体自带的材质用的是第三方非 Mobile 的 TMP_SDF shader，
        /// URP 下整段渲染成实心方块（2026-10-20 实测，详见 Docs/UI-STYLE.md §7.3）。
        /// </summary>
        public static TMP_FontAsset Font()
        {
            var def = TMP_Settings.defaultFontAsset;
            if (def == null) Debug.LogError("[UI] TMP_Settings.defaultFontAsset 为空。");
            return def;
        }

        // ==================== 建节点 ====================
        public static GameObject EnsureUIObject(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        /// <summary>铺满父节点。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>按中心锚点摆位（这套 UI 内部统一用绝对坐标）。</summary>
        public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
        }

        /// <summary>建 / 改一段 TMP 文字（字体走 <see cref="Font"/>）。</summary>
        public static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
                                               Color color, TextAlignmentOptions align)
        {
            var go = EnsureUIObject(parent, name);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();

            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.font = Font();

            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return tmp;
        }

        /// <summary>水平细线（2px 高，缩到 720p 不会消失）。</summary>
        public static Image HLine(Transform parent, string name, float y, float width, Color color)
        {
            var go = EnsureUIObject(parent, name);
            Place(go.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(width, 2f));
            var img = GetImage(go);
            img.sprite = null;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>竖直细线。</summary>
        public static Image VLine(Transform parent, string name, float x, float y, float height, Color color)
        {
            var go = EnsureUIObject(parent, name);
            Place(go.GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(2f, height));
            var img = GetImage(go);
            img.sprite = null;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image GetImage(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            return img;
        }

        // ==================== 按钮 ====================
        /// <summary>从零建一个「纯文字 + 细线」按钮的骨架（透明 Image 吃射线 + Button + TMP 子节点）。</summary>
        public static GameObject CreateTextButton(Transform parent, string name)
        {
            var go = EnsureUIObject(parent, name);
            GetImage(go);
            if (go.GetComponent<Button>() == null) go.AddComponent<Button>();

            if (go.transform.Find("Text (TMP)") == null)
            {
                var t = new GameObject("Text (TMP)", typeof(RectTransform));
                t.layer = 5;
                t.transform.SetParent(go.transform, false);
                Undo.RegisterCreatedObjectUndo(t, "Create Text");
                t.AddComponent<TextMeshProUGUI>();
            }
            return go;
        }

        /// <summary>
        /// 配置文字按钮。
        /// <para><paramref name="underline"/> = true：底部 2px 细线，文字居中（弹窗里的「关闭 / 继续」）。</para>
        /// <para><paramref name="underline"/> = false：左侧 2px 竖线，文字左对齐（暂停菜单的竖排列表）。</para>
        /// </summary>
        public static GameObject SetupTextButton(GameObject go, string label, Vector2 pos, Vector2 size,
                                                 bool underline, float fontSize = 26f)
        {
            if (go == null) return null;

            var rt = go.GetComponent<RectTransform>();
            Place(rt, pos, size);

            var bg = GetImage(go);
            bg.sprite = null;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;

            var lbl = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null)
            {
                lbl.text = label;
                lbl.fontSize = fontSize;
                lbl.characterSpacing = underline ? 8f : 4f;
                lbl.color = TextNormal;
                lbl.alignment = underline ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
                lbl.raycastTarget = false;
                lbl.font = Font();

                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                //竖线版左侧留 22px 给那条线
                lrt.offsetMin = new Vector2(underline ? 0f : 22f, underline ? 6f : 0f);
                lrt.offsetMax = new Vector2(0f, underline ? 6f : 0f);
                lrt.localScale = Vector3.one;
            }

            Image line;
            if (underline)
            {
                var lineGo = EnsureUIObject(go.transform, "Line");
                var lrt2 = lineGo.GetComponent<RectTransform>();
                lrt2.anchorMin = new Vector2(0f, 0f);
                lrt2.anchorMax = new Vector2(1f, 0f);
                lrt2.pivot = new Vector2(0.5f, 0f);
                lrt2.sizeDelta = new Vector2(0f, 2f);
                lrt2.anchoredPosition = Vector2.zero;
                lrt2.localScale = Vector3.one;
                line = GetImage(lineGo);
            }
            else
            {
                var lineGo = EnsureUIObject(go.transform, "LeftLine");
                var lrt2 = lineGo.GetComponent<RectTransform>();
                lrt2.anchorMin = new Vector2(0f, 0.5f);
                lrt2.anchorMax = new Vector2(0f, 0.5f);
                lrt2.pivot = new Vector2(0f, 0.5f);
                lrt2.sizeDelta = new Vector2(2f, 30f);
                lrt2.anchoredPosition = new Vector2(0f, 0f);
                lrt2.localScale = Vector3.one;
                line = GetImage(lineGo);
            }

            line.sprite = null;
            line.color = LineNormal;
            line.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = line;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.80f, 1.70f, 1.55f, 1f);
            cb.pressedColor = new Color(0.72f, 0.70f, 0.66f, 1f);
            cb.selectedColor = new Color(1.35f, 1.30f, 1.20f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.12f;
            btn.colors = cb;
            btn.interactable = true;

            return go;
        }

        /// <summary>清空按钮原有持久化监听，再挂上 <paramref name="method"/>（无参 void）。</summary>
        public static void HookClick(Button btn, Object target, string method)
        {
            if (btn == null || target == null)
            {
                Debug.LogWarning("[UI] HookClick 参数缺失：btn=" + (btn != null) + " target=" + (target != null) + " method=" + method);
                return;
            }

            var so = new SerializedObject(btn);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls != null) { calls.ClearArray(); so.ApplyModifiedProperties(); }

            var mi = target.GetType().GetMethod(method, System.Type.EmptyTypes);
            if (mi == null)
            {
                Debug.LogError("[UI] " + target.GetType().Name + " 上没有无参方法 " + method + "()。");
                return;
            }

            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, mi);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        /// <summary>全屏遮罩（模态）：吃掉射线，挡住身后的 HUD。</summary>
        public static Image BuildMask(Transform parent, string name)
        {
            var go = EnsureUIObject(parent, name);
            Stretch(go.GetComponent<RectTransform>());
            var img = GetImage(go);
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = MaskBg;
            img.raycastTarget = true;//⭐ 这一层就是"模态"
            return img;
        }

        /// <summary>设 CanvasScaler 为 1920×1080 / Match 0.5（与主菜单一致）。</summary>
        public static void SetupCanvasScaler(Canvas canvas)
        {
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
