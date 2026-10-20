using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 一键把 MainMenu 重建成「以实拍截图为背景」的电影感主菜单。
    ///
    /// <para>菜单：<c>Tools/UI/生成主菜单界面 (MainMenu)</c></para>
    ///
    /// <para>设计思路：背景是主体，UI 只做排版 —— 不加面板、不加边框、不加材质。
    /// 三个按钮是**纯文字 + 一条细线**，靠一层 scrim（遮罩）保证可读性。
    /// 这是《Elden Ring》《Dark Souls》这类主菜单的做法。</para>
    ///
    /// <para>⚠️ 可重复执行：所有节点都是「有就改、没有就建」。</para>
    /// </summary>
    public static class MainMenuUIBuilder
    {
        const string ArtDir = "Assets/_Game/Art/UI/MainMenu";
        const string ScenePath = "Assets/Scenes/MainMenu.unity";

        const float BtnW = 240f;
        const float BtnH = 56f;
        const float BtnGap = 260f;      // 三个按钮的水平间距
        const float RowW = 760f;        // 键位面板左右两列的总宽

        // 文字 / 细线配色（暖白 + 冷灰，取自背景截图）
        static readonly Color TextNormal = new Color(0.90f, 0.87f, 0.80f, 1f);
        static readonly Color TextDisabled = new Color(0.42f, 0.42f, 0.40f, 1f);
        static readonly Color LineNormal = new Color(0.55f, 0.52f, 0.47f, 1f);
        static readonly Color LineDisabled = new Color(0.28f, 0.28f, 0.27f, 1f);

        // 键位面板：延续 v4 语言 —— 没有面板框，只有「全屏暗场 + 两条细线 + 两列文字」
        //⚠️ 遮罩 alpha 定 0.93：0.86 时身后的「开始游戏 / 退出游戏」仍很抢眼，会和面板正文抢焦点
        static readonly Color PanelBg = new Color(0.012f, 0.014f, 0.016f, 0.93f);
        static readonly Color KeyColor = new Color(0.72f, 0.69f, 0.63f, 1f);   // 右侧键名：略暗于正文
        static readonly Color GroupColor = new Color(0.66f, 0.62f, 0.55f, 1f);
        static readonly Color HairLine = new Color(0.55f, 0.52f, 0.47f, 0.30f);

        [MenuItem("Tools/UI/生成主菜单界面 (MainMenu)", false, 10)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("先退出 Play 模式",
                    "Play 模式下改场景不会被保存。请先停止运行，再执行一次。", "好");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.name != "MainMenu")
            {
                if (!EditorUtility.DisplayDialog("切到 MainMenu 场景？",
                        "当前场景是「" + scene.name + "」，这个工具只应作用在 MainMenu 上。\n\n要不要先打开 MainMenu.unity？",
                        "打开并继续", "取消"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            ApplyImportSettings();

            var canvasGo = FindOrCreateCanvas();
            if (canvasGo == null)
            {
                Debug.LogError("[MainMenuUI] 找不到也没法创建 Canvas，中止。");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvasGo, "Build Main Menu");

            SetupCanvasScaler(canvasGo);
            SetupCamera();
            BuildBackdrop(canvasGo.transform);
            BuildScrim(canvasGo.transform);
            BuildLogo();
            SetupLineButton("Button_Start", "开 始 游 戏", new Vector2(-BtnGap, -390f), false);
            BuildSettingsButton(canvasGo.transform, new Vector2(0f, -390f));
            SetupLineButton("Button_Quit", "退 出 游 戏", new Vector2(BtnGap, -390f), false);
            BuildFooter(canvasGo.transform);
            var keybinds = BuildKeybindPanel(canvasGo.transform);
            WireSettingsButton(canvasGo, keybinds);
            RemoveLegacyDividers(canvasGo.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasGo;
            SceneView.RepaintAll();

            Debug.Log("[MainMenuUI] 重建完成。请按 Ctrl+S 保存场景，然后 Play 检查。");
        }

        // ================================================================
        //  键位面板（只做展示，改键功能后续再做）
        // ================================================================

        /// <summary>
        /// 只生成 / 更新「按键说明」面板，并顺手把设置按钮点亮接线。
        ///
        /// <para>为什么单独一个入口：整包 <see cref="Build"/> 会重建背景 / Logo，
        /// 而 <c>Art/UI/MainMenu</c> 下的 <c>ui_bg_photo</c> / <c>ui_logo_trial</c> / <c>ui_bg_scrim</c>
        /// 目前**还没导出**（只有 <c>ui_bg_mainmenu.png</c>）—— 跑整包会把现有背景刷成空白。
        /// 只想改键位面板时用这个入口，一个字节都不碰背景。</para>
        ///
        /// <para>⚠️ 可重复执行：节点「有就改、没有就建」，按钮监听会先清空再挂，不会叠加。</para>
        /// </summary>
        [MenuItem("Tools/UI/生成键位面板 (MainMenu)", false, 11)]
        public static void BuildKeybindsOnly()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("先退出 Play 模式",
                    "Play 模式下改场景不会被保存。请先停止运行，再执行一次。", "好");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.name != "MainMenu")
            {
                if (!EditorUtility.DisplayDialog("切到 MainMenu 场景？",
                        "当前场景是「" + scene.name + "」，这个工具只应作用在 MainMenu 上。\n\n要不要先打开 MainMenu.unity？",
                        "打开并继续", "取消"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            var canvasGo = FindOrCreateCanvas();
            if (canvasGo == null)
            {
                Debug.LogError("[Keybinds] 找不到也没法创建 Canvas，中止。");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvasGo, "Build Keybind Panel");

            SetupCanvasScaler(canvasGo);
            var panel = BuildKeybindPanel(canvasGo.transform);
            BuildSettingsButton(canvasGo.transform, new Vector2(0f, -390f));//以「启用态」重建一次
            WireSettingsButton(canvasGo, panel);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = panel;
            SceneView.RepaintAll();

            Debug.Log("[Keybinds] 键位面板已生成（预设 inactive）；设置按钮已启用并接到 KeybindPanel.Open。" +
                      "请按 Ctrl+S 保存场景，然后 Play → 点「设置」检查。");
        }

        /// <summary>生成「按键说明」面板，返回面板根节点（结束时**设为 inactive**）。</summary>
        static GameObject BuildKeybindPanel(Transform canvas)
        {
            var root = EnsureUIObject(canvas, "Panel_Keybinds");
            Stretch(root.GetComponent<RectTransform>());

            var img = root.GetComponent<Image>();
            if (img == null) img = root.AddComponent<Image>();
            img.sprite = null;                  // 纯色，不用九宫格图
            img.type = Image.Type.Simple;
            img.color = PanelBg;
            //⭐ 全屏吃射线：这一层就是「模态」—— 身后的开始 / 退出按钮自然点不到
            img.raycastTarget = true;

            root.transform.SetAsLastSibling();

            var content = EnsureUIObject(root.transform, "Content");
            Place(content.GetComponent<RectTransform>(), Vector2.zero, new Vector2(900f, 800f));

            var title = MakeText(content.transform, "Text_PanelTitle", "按键说明", 38f, TextNormal, TextAlignmentOptions.Center);
            title.characterSpacing = 14f;
            Place(title.rectTransform, new Vector2(0f, 340f), new Vector2(900f, 56f));

            var sub = MakeText(content.transform, "Text_PanelSub", "CONTROLS", 15f, KeyColor, TextAlignmentOptions.Center);
            sub.characterSpacing = 18f;
            Place(sub.rectTransform, new Vector2(0f, 298f), new Vector2(900f, 22f));

            BuildHLine(content.transform, "Line_Head", 268f);

            // —— 行动 ——
            BuildGroupLabel(content.transform, "Text_GroupAction", "行动", 222f);
            BuildKeyRow(content.transform, "Move", "移动", "W A S D", 178f);
            BuildKeyRow(content.transform, "Sprint", "奔跑", "Shift（按住）", 134f);
            BuildKeyRow(content.transform, "Jump", "跳跃", "空格", 90f);
            BuildKeyRow(content.transform, "Dash", "翻滚", "鼠标右键", 46f);
            BuildKeyRow(content.transform, "Look", "视角", "鼠标移动", 2f);
            BuildKeyRow(content.transform, "Zoom", "镜头远近", "鼠标滚轮", -42f);

            // —— 战斗 ——
            BuildGroupLabel(content.transform, "Text_GroupCombat", "战斗", -92f);
            BuildKeyRow(content.transform, "Attack", "普通攻击", "鼠标左键", -136f);
            BuildKeyRow(content.transform, "Special", "火球", "1", -180f);
            BuildKeyRow(content.transform, "Interact", "交互", "F", -224f);
            BuildKeyRow(content.transform, "Pause", "暂停", "Esc", -268f);

            BuildHLine(content.transform, "Line_Foot", -306f);

            var close = CreateLineButton(content.transform, "Button_Close");
            SetupLineButton(close, "返 回", Vector2.zero, false);
            //SetupLineButton 会强制把锚点改成屏幕中央，这里再摆到面板底部
            Place(close.GetComponent<RectTransform>(), new Vector2(0f, -354f), new Vector2(BtnW, BtnH));

            //⚠️ 预设 inactive：否则运行时第一帧会闪一下（UI-PLAN §4.3 的约定）
            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 把设置按钮接到 <see cref="KeybindPanel.Open"/>、面板里的「返回」接到 <c>Close</c>。
        ///
        /// <para>用 <see cref="UnityEventTools.AddPersistentListener"/> 挂**持久化**监听
        /// —— 场景里能看到、打包后也有效；运行时 <c>AddListener</c> 不会写进场景。</para>
        /// </summary>
        static void WireSettingsButton(GameObject canvasGo, GameObject panel)
        {
            var settingsGo = GameObject.Find("Button_Settings");
            if (settingsGo == null || panel == null)
            {
                Debug.LogWarning("[Keybinds] 设置按钮或面板缺失，跳过接线。");
                return;
            }

            var openBtn = settingsGo.GetComponent<Button>();
            if (openBtn == null)
            {
                Debug.LogWarning("[Keybinds] Button_Settings 上没有 Button 组件。");
                return;
            }

            var comp = canvasGo.GetComponent<KeybindPanel>();
            if (comp == null) comp = canvasGo.AddComponent<KeybindPanel>();//挂在常驻的 Canvas 上，ESC 才有人听

            var so = new SerializedObject(comp);
            var prop = so.FindProperty("_panel");
            if (prop == null)
            {
                Debug.LogError("[Keybinds] KeybindPanel 上找不到 _panel 字段（脚本可能还没编译完，稍后重跑一次）。");
                return;
            }
            prop.objectReferenceValue = panel;
            so.ApplyModifiedProperties();

            HookClick(openBtn, comp, "Open");

            var closeT = panel.transform.Find("Content/Button_Close");
            if (closeT != null) HookClick(closeT.GetComponent<Button>(), comp, "Close");
        }

        /// <summary>清空按钮原有的持久化监听，再挂上 <paramref name="method"/>（无参 void）。</summary>
        static void HookClick(Button btn, Object target, string method)
        {
            if (btn == null || target == null) return;

            //先清空 —— 否则重复执行本工具会挂出 N 个监听，点一下开 N 次
            var so = new SerializedObject(btn);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls != null)
            {
                calls.ClearArray();
                so.ApplyModifiedProperties();
            }

            var mi = target.GetType().GetMethod(method, System.Type.EmptyTypes);
            if (mi == null)
            {
                Debug.LogError("[Keybinds] " + target.GetType().Name + " 上没有无参方法 " + method + "()。");
                return;
            }

            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, mi);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // ================================================================
        //  资源导入
        // ================================================================
        static void ApplyImportSettings()
        {
            AssetDatabase.Refresh();

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[MainMenuUI] " + ArtDir + " 下没有贴图。");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                // 实拍背景是 2095px 宽，放开到 4096 否则会被压到 2048 损失细节
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.spriteBorder = Vector4.zero;
                ti.SaveAndReimport();
            }

            AssetDatabase.Refresh();
            Debug.Log("[MainMenuUI] 已刷新 " + guids.Length + " 张贴图。");
        }

        /// <summary>
        /// 取 TMP 文字要用的字体：**统一返回 `<see cref="TMP_Settings.defaultFontAsset"/>`（= `LiberationSans SDF`）**，
        /// 中文交给它的 fallback 链（→ `LiberationSans SDF - Fallback` → `sarasa-gothic-sc-regular SDF`）。
        ///
        /// <para>⚠️⚠️ **不要直接返回 sarasa SDF**（2026-10-20 实测踩坑，被这个坑咬了两次）：</para>
        /// <list type="bullet">
        ///   <item>把 <c>lbl.font</c> 直指 `sarasa-gothic-sc-regular SDF` ⇒ 整段中文**渲染成实心方块**，
        ///        但同一条文字挂 `LiberationSans` 走 fallback 就完全正常。</item>
        ///   <item>字形本身没问题：字符表 174 条 / 字形表 174 条一一对应，图集（2048² Alpha8，占用 27.4%）
        ///        里 `按 / 键 / 说 / 明` 都有 6000+ 个带 alpha 的像素，`textInfo.isVisible` 也是 true
        ///        —— 也就是说**数据全对，是渲染端画不出来**。</item>
        ///   <item>差别在材质：sarasa 自带的 `sarasa-gothic-sc-regular Atlas Material` 用的 shader 是
        ///        `Assets/0_Script/ThirdParty/Battlehub/ThirdParty/TextMesh Pro/Resources/Shaders/TMP_SDF.shader`
        ///        （第三方那份，**非 Mobile** 的 `TextMeshPro/Distance Field` 变体，guid `1383d132…`）；
        ///        而项目里能正常出字的材质（`LiberationSans SDF`）用的是 `Mobile/Distance Field` 变体。
        ///        两份 shader **同名不同文件**，URP 下非 Mobile 那份画不出字形。</item>
        /// </list>
        ///
        /// <para>⇒ 结论：全项目 TMP **一律用默认字体 + fallback**，不要为了"少一层 fallback"去直指 sarasa。
        /// 想根治得先把 sarasa 材质的 shader 换成 `Mobile/Distance Field` 并重设材质属性，风险大于收益。</para>
        /// </summary>
        static TMP_FontAsset LoadCNFont()
        {
            var def = TMP_Settings.defaultFontAsset;
            if (def == null)
            {
                Debug.LogError("[MainMenuUI] TMP_Settings.defaultFontAsset 为空，文字会缺字体。");
                return null;
            }
            return def;
        }

        static Sprite LoadSprite(string file)
        {
            var path = ArtDir + "/" + file + ".png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sp = asset as Sprite;
                if (sp != null) return sp;
            }
            var fallback = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (fallback == null) Debug.LogError("[MainMenuUI] 缺贴图：" + path);
            return fallback;
        }

        // ================================================================
        //  Canvas / 相机
        // ================================================================
        static GameObject FindOrCreateCanvas()
        {
            var go = GameObject.Find("Canvas");
            if (go != null) return go;

            go = new GameObject("Canvas", typeof(RectTransform));
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static void SetupCanvasScaler(GameObject canvasGo)
        {
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.016f, 0.02f, 1f);
        }

        // ================================================================
        //  界面元素
        // ================================================================
        static void BuildBackdrop(Transform canvas)
        {
            var go = EnsureUIObject(canvas, "BG_Backdrop");
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = LoadSprite("ui_bg_photo");
            img.type = Image.Type.Simple;
            img.preserveAspect = false;     // 轻微拉伸比留黑边好
            img.raycastTarget = false;
            img.color = Color.white;

            Stretch(go.GetComponent<RectTransform>());
            go.transform.SetAsFirstSibling();
        }

        static void BuildScrim(Transform canvas)
        {
            var go = EnsureUIObject(canvas, "BG_Scrim");
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = LoadSprite("ui_bg_scrim");
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.color = Color.white;

            Stretch(go.GetComponent<RectTransform>());
            go.transform.SetSiblingIndex(1);
        }

        static void BuildLogo()
        {
            var go = GameObject.Find("Text_Title");
            if (go == null)
            {
                Debug.LogWarning("[MainMenuUI] 场景里没有 Text_Title，跳过 Logo。");
                return;
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null) Object.DestroyImmediate(tmp);

            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = LoadSprite("ui_logo_trial");
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(660f, 366f);
            rt.anchoredPosition = new Vector2(0f, 340f);
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 纯文字按钮：透明 Image（吃射线）+ TMP + 底部 1px 细线。
        /// 细线作为 targetGraphic，靠 ColorTint 在 hover 时提亮 —— 不需要额外脚本。
        /// </summary>
        static GameObject SetupLineButton(string goName, string label, Vector2 pos, bool disabled)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning("[MainMenuUI] 找不到按钮 " + goName + "，跳过。");
                return null;
            }

            return SetupLineButton(go, label, pos, disabled);
        }

        /// <summary>同上，直接接受对象 —— 面板里的按钮是 inactive 的，<c>GameObject.Find</c> 找不到。</summary>
        static GameObject SetupLineButton(GameObject go, string label, Vector2 pos, bool disabled)
        {
            if (go == null) return null;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(BtnW, BtnH);
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;

            // 透明底，只为接收点击
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = null;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = !disabled;

            // 文字
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null)
            {
                lbl.text = label;
                lbl.fontSize = 26f;
                lbl.characterSpacing = 8f;
                lbl.color = disabled ? TextDisabled : TextNormal;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.raycastTarget = false;
                lbl.font = LoadCNFont();

                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(0f, 6f);
                lrt.offsetMax = new Vector2(0f, 6f);
                lrt.localScale = Vector3.one;
            }

            // 细线
            var lineGo = EnsureUIObject(go.transform, "Line");
            var lrt2 = lineGo.GetComponent<RectTransform>();
            lrt2.anchorMin = new Vector2(0f, 0f);
            lrt2.anchorMax = new Vector2(1f, 0f);
            lrt2.pivot = new Vector2(0.5f, 0f);
            lrt2.sizeDelta = new Vector2(0f, 2f);
            lrt2.anchoredPosition = new Vector2(0f, 0f);
            lrt2.localScale = Vector3.one;

            var limg = lineGo.GetComponent<Image>();
            if (limg == null) limg = lineGo.AddComponent<Image>();
            limg.sprite = null;
            limg.color = disabled ? LineDisabled : LineNormal;
            limg.raycastTarget = false;

            // Button：把细线当 targetGraphic
            var btn = go.GetComponent<Button>();
            if (btn == null) return go;
            btn.targetGraphic = limg;
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
            btn.interactable = !disabled;

            return go;
        }

        /// <summary>
        /// 设置按钮：2026-10-20 起**启用** —— 点开「按键说明」面板（<see cref="KeybindPanel"/>）。
        /// 只做键位展示，改键功能后续再接。
        /// </summary>
        static void BuildSettingsButton(Transform canvas, Vector2 pos)
        {
            var go = GameObject.Find("Button_Settings");
            if (go == null)
            {
                var src = GameObject.Find("Button_Quit");
                if (src == null)
                {
                    Debug.LogWarning("[MainMenuUI] 没有 Button_Quit 可复制，跳过设置按钮。");
                    return;
                }

                go = Object.Instantiate(src, src.transform.parent);
                go.name = "Button_Settings";
                Undo.RegisterCreatedObjectUndo(go, "Create Button_Settings");

                var srcBtn = go.GetComponent<Button>();
                if (srcBtn != null) srcBtn.onClick = new Button.ButtonClickedEvent();
                go.transform.SetSiblingIndex(src.transform.GetSiblingIndex());
            }

            SetupLineButton(go, "设　　置", pos, false);
        }

        static void BuildFooter(Transform canvas)
        {
            var hint = EnsureUIObject(canvas, "ControlHint");
            var ht = hint.GetComponent<TextMeshProUGUI>();
            if (ht == null) ht = hint.AddComponent<TextMeshProUGUI>();
            ht.text = "WASD 移动　·　鼠标 视角　·　左键 攻击　·　1 火球　·　空格 跳跃　·　Shift 跑　·　右键 翻滚　·　F 交互　·　ESC 暂停";
            ht.fontSize = 16f;
            ht.characterSpacing = 1f;
            ht.color = new Color32(158, 152, 140, 190);
            ht.alignment = TextAlignmentOptions.Center;
            ht.raycastTarget = false;
            ht.font = LoadCNFont();

            var hrt = hint.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(1500f, 26f);
            hrt.anchoredPosition = new Vector2(0f, -462f);

            var ver = EnsureUIObject(canvas, "VersionText");
            var vt = ver.GetComponent<TextMeshProUGUI>();
            if (vt == null) vt = ver.AddComponent<TextMeshProUGUI>();
            vt.text = "v0.1   ·   课程设计作品   ·   Unity 6";
            vt.fontSize = 16f;
            vt.color = new Color32(126, 122, 112, 170);
            vt.alignment = TextAlignmentOptions.BottomRight;
            vt.raycastTarget = false;
            vt.font = LoadCNFont();

            var vrt = ver.GetComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = new Vector2(1f, 0f);
            vrt.pivot = new Vector2(1f, 0f);
            vrt.sizeDelta = new Vector2(620f, 26f);
            vrt.anchoredPosition = new Vector2(-44f, 30f);

            var cr = EnsureUIObject(canvas, "CopyrightText");
            var ct = cr.GetComponent<TextMeshProUGUI>();
            if (ct == null) ct = cr.AddComponent<TextMeshProUGUI>();
            ct.text = "© 2026  试炼 TRIAL";
            ct.fontSize = 16f;
            ct.color = new Color32(126, 122, 112, 170);
            ct.alignment = TextAlignmentOptions.BottomLeft;
            ct.raycastTarget = false;
            ct.font = LoadCNFont();

            var crt = cr.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0f);
            crt.pivot = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(620f, 26f);
            crt.anchoredPosition = new Vector2(44f, 30f);
        }

        /// <summary>v3 留下的装饰节点，v4 不需要。</summary>
        static void RemoveLegacyDividers(Transform canvas)
        {
            var legacy = canvas.Find("Divider");
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
        }

        // ================================================================
        //  工具
        // ================================================================
        static GameObject EnsureUIObject(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>按「中心锚点 + 绝对偏移」摆位（键位面板内部所有节点都这么摆）。</summary>
        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 建 / 改一段 TMP 文字。
        /// ⚠️ 字体**直接指定** sarasa SDF，不走 <c>LiberationSans</c> 的 fallback 链（见 UI-STYLE §7.3）。
        /// </summary>
        static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
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
            tmp.font = LoadCNFont();

            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return tmp;
        }

        /// <summary>
        /// 从零建一个「纯文字 + 细线」按钮的骨架（透明 Image 吃射线 + Button + TMP 子节点）。
        /// 场景里原有的三个按钮是手搭的，面板里的「返回」没有父模板可复制，只能这样建。
        /// 建完仍需调 <see cref="SetupLineButton(GameObject, string, Vector2, bool)"/> 填文字与细线。
        /// </summary>
        static GameObject CreateLineButton(Transform parent, string name)
        {
            var go = EnsureUIObject(parent, name);

            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            //子节点名字与场景里其它按钮一致：「Text (TMP)」
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

        /// <summary>1px 细线（2px 高，参考分辨率 1080 缩到 720 时不会消失）。</summary>
        static void BuildHLine(Transform parent, string name, float y)
        {
            var go = EnsureUIObject(parent, name);
            Place(go.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(RowW, 2f));

            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = null;
            img.color = HairLine;
            img.raycastTarget = false;
        }

        /// <summary>分组小标题（左对齐、大字距、低对比）。</summary>
        static void BuildGroupLabel(Transform parent, string name, string text, float y)
        {
            var tmp = MakeText(parent, name, text, 18f, GroupColor, TextAlignmentOptions.Left);
            tmp.characterSpacing = 8f;
            Place(tmp.rectTransform, new Vector2(0f, y), new Vector2(RowW, 26f));
        }

        /// <summary>
        /// 一行键位：左边动作名（暖白、左对齐），右边键名（冷灰、右对齐）。
        /// ⚠️ 节点名 <c>Row_&lt;id&gt;</c> + 子节点 <c>Label</c> / <c>Key</c> 是**约定**
        /// （<c>KeybindPanel.FindKeyLabel</c> 靠它定位），改名前先搜代码。
        /// </summary>
        static void BuildKeyRow(Transform parent, string id, string action, string keys, float y)
        {
            var rowGo = EnsureUIObject(parent, "Row_" + id);
            Place(rowGo.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(RowW, 40f));

            var label = MakeText(rowGo.transform, "Label", action, 22f, TextNormal, TextAlignmentOptions.Left);
            label.characterSpacing = 4f;
            Stretch(label.rectTransform);

            var key = MakeText(rowGo.transform, "Key", keys, 22f, KeyColor, TextAlignmentOptions.Right);
            key.characterSpacing = 2f;
            Stretch(key.rectTransform);
        }
    }
}
