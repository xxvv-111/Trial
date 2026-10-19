using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        // 文字 / 细线配色（暖白 + 冷灰，取自背景截图）
        static readonly Color TextNormal = new Color(0.90f, 0.87f, 0.80f, 1f);
        static readonly Color TextDisabled = new Color(0.42f, 0.42f, 0.40f, 1f);
        static readonly Color LineNormal = new Color(0.55f, 0.52f, 0.47f, 1f);
        static readonly Color LineDisabled = new Color(0.28f, 0.28f, 0.27f, 1f);

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
            RemoveLegacyDividers(canvasGo.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasGo;
            SceneView.RepaintAll();

            Debug.Log("[MainMenuUI] 重建完成。请按 Ctrl+S 保存场景，然后 Play 检查。");
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

        /// <summary>直接给 TMP 指定中文字体资产，不依赖 LiberationSans 的 fallback 链（更稳）。</summary>
        static TMP_FontAsset LoadCNFont()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/1_Asset/4_Font/sarasa-gothic-sc-regular SDF.asset");
            if (fa != null) return fa;
            Debug.LogWarning("[MainMenuUI] 没找到 Sarasa 字体资产，回退到 TMP 默认字体（中文可能显示为 □）。");
            return TMP_Settings.defaultFontAsset;
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
        static void SetupLineButton(string goName, string label, Vector2 pos, bool disabled)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning("[MainMenuUI] 找不到按钮 " + goName + "，跳过。");
                return;
            }

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
            if (btn == null) return;
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
        }

        /// <summary>设置按钮：功能未实现 ⇒ 禁用（灰字 + 灰线 + 不接射线）。</summary>
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

            SetupLineButton("Button_Settings", "设　　置", pos, true);
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
    }
}
