using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 把 <c>Tavern</c> 的三块 UI 重建成 v4 电影感风格：
    /// <list type="number">
    ///   <item><b>交互提示条</b>（HUD，常驻但按需显示）</item>
    ///   <item><b>任务面板</b>（弹窗，左列条目 + 右侧详情）</item>
    ///   <item><b>暂停菜单</b>（弹窗，左列竖排按钮 + 右侧场景信息）</item>
    /// </list>
    ///
    /// <para>菜单：<c>Tools/UI/生成酒馆 UI（全部）</c>，以及三个分项。</para>
    ///
    /// <para><b>为什么"比主菜单复杂"</b>：主菜单只有一排按钮，游戏内 UI 必须分栏 ——
    /// 左侧是"选什么"、右侧是"它的内容"，中间用 1px 竖线切；顶部标题 + 两条横线夹住内容；
    /// 暂停菜单还要多一块"当前场景信息"。但**依然没有面板框 / 圆角 / 材质**，
    /// 只有遮罩、细线、文字 —— 这是 <c>Docs/UI-STYLE.md</c> 的底线。</para>
    ///
    /// <para>⚠️ 可重复执行：节点「有就改、没有就建」，按钮监听先清空再挂。</para>
    /// </summary>
    public static class TavernUIBuilder
    {
        const string ScenePath = "Assets/Scenes/Tavern.unity";
        const string CanvasName = "Canvas_InteractPromot";
        const int BaseOrder = 20;

        // ================================================================
        //  入口
        // ================================================================
        [MenuItem("Tools/UI/生成酒馆 UI（全部）", false, BaseOrder)]
        public static void BuildAll()
        {
            if (!Prepare()) return;

            var canvas = GameObject.Find(CanvasName).transform;
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Tavern UI");

            BuildInteractPrompt(canvas);
            BuildQuestPanel(canvas);
            BuildPausePanel(canvas);
            FixInteractablePrompts();
            SaveScene();

            Debug.Log("[TavernUI] 三块 UI 全部重建完成（提示条 / 任务面板 / 暂停菜单）。请 Play 检查。");
        }

        [MenuItem("Tools/UI/生成酒馆 UI/交互提示条", false, BaseOrder + 1)]
        public static void BuildPromptOnly() { BuildSingle(0); }

        [MenuItem("Tools/UI/生成酒馆 UI/任务面板", false, BaseOrder + 2)]
        public static void BuildQuestOnly() { BuildSingle(1); }

        [MenuItem("Tools/UI/生成酒馆 UI/暂停菜单", false, BaseOrder + 3)]
        public static void BuildPauseOnly() { BuildSingle(2); }

        static void BuildSingle(int which)
        {
            if (!Prepare()) return;

            var canvas = GameObject.Find(CanvasName).transform;
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Tavern UI");

            if (which == 0) BuildInteractPrompt(canvas);
            else if (which == 1) BuildQuestPanel(canvas);
            else if (which == 2) BuildPausePanel(canvas);

            SaveScene();
            Debug.Log("[TavernUI] 单项重建完成（" + which + "）。");
        }

        /// <summary>场景检查 + 切场景 + CanvasScaler。</summary>
        static bool Prepare()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("先退出 Play 模式",
                    "Play 模式下改场景不会被保存。请先停止运行，再执行一次。", "好");
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Tavern")
            {
                if (!EditorUtility.DisplayDialog("切到 Tavern 场景？",
                        "当前场景是「" + scene.name + "」，这个工具只应作用在 Tavern 上。\n\n要不要先打开 Tavern.unity？",
                        "打开并继续", "取消"))
                    return false;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            var canvasGo = GameObject.Find(CanvasName);
            if (canvasGo == null)
            {
                Debug.LogError("[TavernUI] 场景里找不到 " + CanvasName + "，中止。\n" +
                               "⚠️ 这个 Canvas 名字被 QuestPanel / InteractPromptUI 依赖（脚本按名字找），不要改成别的。");
                return false;
            }

            UIBuilderKit.SetupCanvasScaler(canvasGo.GetComponent<Canvas>());
            return true;
        }

        static void SaveScene()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        // ================================================================
        //  1. 交互提示条
        // ================================================================
        /// <summary>
        /// HUD 提示：<c>[F] 查看告示栏</c>，屏幕下方居中。
        ///
        /// <para>结构：一层很淡的扁条底（不是面板）+ 1px 键帽方框 + 文字。
        /// 用 <see cref="HorizontalLayoutGroup"/> + <see cref="ContentSizeFitter"/> 让**宽度跟着文案变**，
        /// 否则"前往城堡"和"查看告示栏"会一个偏左一个偏右。</para>
        ///
        /// <para>⚠️ 文字节点名必须是 <c>Text</c> —— <see cref="InteractPromptUI"/> 的 <c>_text</c> 指向它。</para>
        /// </summary>
        static void BuildInteractPrompt(Transform canvas)
        {
            var root = UIBuilderKit.EnsureUIObject(canvas, "Panel_InteractPrompt");
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -330f);
            rt.sizeDelta = new Vector2(280f, 64f);
            rt.localScale = Vector3.one;

            //⚠️ 第一版用过 HorizontalLayoutGroup + ContentSizeFitter 做自适应宽度，**不行**：
            //   旧的 Text 节点排在新建的 KeyCap 前面（EnsureUIObject 复用旧节点），
            //   布局结果变成「文字在左、键帽在右」，而且 TMP 的 preferredWidth 和固定 sizeDelta 打架。
            //   改成和项目其它面板一致的**绝对定位**：键帽固定在左，文字固定宽度左对齐。
            var oldLayout = root.GetComponent<HorizontalLayoutGroup>();
            if (oldLayout != null) Object.DestroyImmediate(oldLayout);
            var oldFitter = root.GetComponent<ContentSizeFitter>();
            if (oldFitter != null) Object.DestroyImmediate(oldFitter);

            // 扁条底：很淡，只保证 3D 背景上可读，不构成"面板"
            var bg = UIBuilderKit.GetImage(root);
            bg.sprite = null;
            bg.type = Image.Type.Simple;
            bg.color = UIBuilderKit.PromptBg;
            bg.raycastTarget = false;

            // ---- 键帽（固定在左侧）----
            var cap = UIBuilderKit.EnsureUIObject(root.transform, "KeyCap");
            UIBuilderKit.Place(cap.GetComponent<RectTransform>(), new Vector2(-106f, 0f), new Vector2(44f, 44f));
            BuildKeyCapFrame(cap.transform, 44f);

            var capTxt = UIBuilderKit.MakeText(cap.transform, "Text_Key", "F", 24f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Center);
            UIBuilderKit.Stretch(capTxt.rectTransform);

            // ---- 文案（左对齐，固定宽 250）----
            var text = UIBuilderKit.MakeText(root.transform, "Text", "查看告示栏", 24f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Left);
            text.characterSpacing = 3f;
            UIBuilderKit.Place(text.rectTransform, new Vector2(32f, 0f), new Vector2(200f, 32f));

            // 键帽必须在文字之前渲染（同级顺序影响叠放，虽然不重叠也统一一下）
            cap.transform.SetAsFirstSibling();

            // 接线
            var ui = root.GetComponent<InteractPromptUI>();
            if (ui == null) ui = root.AddComponent<InteractPromptUI>();
            var so = new SerializedObject(ui);
            SetRef(so, "_panel", null);
            SetRef(so, "_text", text);
            //⚠️ 文案模板只留 {action}：键名已经由左边的键帽画出来了，再拼一次 F 会重复
            var fmt = so.FindProperty("_format");
            if (fmt != null) fmt.stringValue = "{action}";
            var key = so.FindProperty("_keyLabel");
            if (key != null) key.stringValue = "F";
            so.ApplyModifiedProperties();

            //⚠️ **必须保持 active**：InteractPromptUI 就挂在这个物体上，
            //   物体 inactive ⇒ `Start` 根本不执行 ⇒ 不订阅 `TargetChanged` ⇒ 提示条永远不出现。
            //   Start 里会 Refresh(null) 立刻把自己隐藏，所以不会闪。
            root.SetActive(true);
        }

        /// <summary>键帽的 1px 方框（四条边）。</summary>
        static void BuildKeyCapFrame(Transform cap, float size)
        {
            float h = size * 0.5f;
            var c = UIBuilderKit.LineNormal;

            var top = UIBuilderKit.EnsureUIObject(cap, "Edge_Top");
            UIBuilderKit.Place(top.GetComponent<RectTransform>(), new Vector2(0f, h), new Vector2(size, 2f));
            PaintLine(top, c);

            var bottom = UIBuilderKit.EnsureUIObject(cap, "Edge_Bottom");
            UIBuilderKit.Place(bottom.GetComponent<RectTransform>(), new Vector2(0f, -h), new Vector2(size, 2f));
            PaintLine(bottom, c);

            var left = UIBuilderKit.EnsureUIObject(cap, "Edge_Left");
            UIBuilderKit.Place(left.GetComponent<RectTransform>(), new Vector2(-h, 0f), new Vector2(2f, size));
            PaintLine(left, c);

            var right = UIBuilderKit.EnsureUIObject(cap, "Edge_Right");
            UIBuilderKit.Place(right.GetComponent<RectTransform>(), new Vector2(h, 0f), new Vector2(2f, size));
            PaintLine(right, c);
        }

        static void PaintLine(GameObject go, Color color)
        {
            var img = UIBuilderKit.GetImage(go);
            img.sprite = null;
            img.color = color;
            img.raycastTarget = false;
        }

        // ================================================================
        //  2. 任务面板
        // ================================================================
        /// <summary>
        /// 弹窗：顶部标题 + 左列任务条目 + 中间 1px 竖线 + 右侧详情 + 底部关闭。
        ///
        /// <para>⚠️ 根节点名必须是 <c>Panel_QuestPanel</c>（<see cref="QuestPanel"/> 挂在它上面，<c>_panel</c> 留空 = 用自身）。</para>
        /// </summary>
        static void BuildQuestPanel(Transform canvas)
        {
            var root = UIBuilderKit.EnsureUIObject(canvas, "Panel_QuestPanel");
            UIBuilderKit.Stretch(root.GetComponent<RectTransform>());
            var bg = UIBuilderKit.GetImage(root);
            bg.sprite = null;
            bg.color = UIBuilderKit.MaskBg;
            bg.raycastTarget = true;//模态

            var content = UIBuilderKit.EnsureUIObject(root.transform, "Content");
            UIBuilderKit.Place(content.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1100f, 640f));

            var c = content.transform;
            const float leftCx = -250f;    // 左列中心（宽 460 → [-480,-20]）
            const float rightCx = 250f;    // 右列中心（宽 460 → [20,480]）
            const float colW = 460f;

            // 标题
            var title = UIBuilderKit.MakeText(c, "Text_PanelTitle", "任务", 34f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Center);
            title.characterSpacing = 14f;
            UIBuilderKit.Place(title.rectTransform, new Vector2(0f, 250f), new Vector2(960f, 44f));

            var sub = UIBuilderKit.MakeText(c, "Text_PanelSub", "QUESTS", 14f,
                UIBuilderKit.KeyColor, TextAlignmentOptions.Center);
            sub.characterSpacing = 18f;
            UIBuilderKit.Place(sub.rectTransform, new Vector2(0f, 212f), new Vector2(960f, 22f));

            UIBuilderKit.HLine(c, "Line_Head", 182f, 960f, UIBuilderKit.HairLine);

            // 列标题
            var listHead = UIBuilderKit.MakeText(c, "Text_ListHeader", "任务列表", 16f,
                UIBuilderKit.GroupColor, TextAlignmentOptions.Left);
            listHead.characterSpacing = 6f;
            UIBuilderKit.Place(listHead.rectTransform, new Vector2(leftCx, 146f), new Vector2(colW, 24f));

            var detailHead = UIBuilderKit.MakeText(c, "Text_DetailHeader", "详情", 16f,
                UIBuilderKit.GroupColor, TextAlignmentOptions.Left);
            detailHead.characterSpacing = 6f;
            UIBuilderKit.Place(detailHead.rectTransform, new Vector2(rightCx, 146f), new Vector2(colW, 24f));

            // 条目（静态两条；第一条为"当前选中"）
            BuildQuestEntry(c, "Row_Quest_1", "01", "前往城堡", "进行中", 92f, colW, leftCx, true);
            BuildQuestEntry(c, "Row_Quest_2", "02", "击败克里顿", "未开始", 48f, colW, leftCx, false);

            // 中间竖线
            UIBuilderKit.VLine(c, "Divider_Vertical", 0f, -30f, 300f, UIBuilderKit.HairLine);

            // 右侧详情
            var dTitle = UIBuilderKit.MakeText(c, "Text_DetailTitle", "前往城堡", 26f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Left);
            dTitle.characterSpacing = 3f;
            UIBuilderKit.Place(dTitle.rectTransform, new Vector2(rightCx, 96f), new Vector2(colW, 34f));

            var dBody = UIBuilderKit.MakeText(c, "Text_DetailBody", "", 18f,
                UIBuilderKit.TextDim, TextAlignmentOptions.TopLeft);
            dBody.lineSpacing = 12f;
            UIBuilderKit.Place(dBody.rectTransform, new Vector2(rightCx, 20f), new Vector2(colW, 120f));

            var dGoal = UIBuilderKit.MakeText(c, "Text_DetailGoal", "", 17f,
                UIBuilderKit.KeyColor, TextAlignmentOptions.TopLeft);
            UIBuilderKit.Place(dGoal.rectTransform, new Vector2(rightCx, -80f), new Vector2(colW, 60f));

            // 底部
            UIBuilderKit.HLine(c, "Line_Foot", -230f, 960f, UIBuilderKit.HairLine);

            var close = UIBuilderKit.CreateTextButton(c, "Button_Close");
            UIBuilderKit.SetupTextButton(close, "关 闭", new Vector2(0f, -270f), new Vector2(200f, 50f), true, 24f);

            var hint = UIBuilderKit.MakeText(c, "Text_Hint", "ESC 关闭", 15f,
                UIBuilderKit.TextDim, TextAlignmentOptions.Right);
            UIBuilderKit.Place(hint.rectTransform, new Vector2(340f, -270f), new Vector2(300f, 24f));

            // 接线
            var qp = root.GetComponent<QuestPanel>();
            if (qp == null) qp = root.AddComponent<QuestPanel>();
            var so = new SerializedObject(qp);
            SetRef(so, "_panel", null);
            SetRef(so, "_placeholderText", null);   // 分栏模式不用单块文本
            SetRef(so, "_detailTitle", dTitle);
            SetRef(so, "_detailBody", dBody);
            SetRef(so, "_detailGoal", dGoal);

            var entries = so.FindProperty("_entryLabels");
            if (entries != null)
            {
                entries.arraySize = 2;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = c.Find("Row_Quest_1/Label").GetComponent<TextMeshProUGUI>();
                entries.GetArrayElementAtIndex(1).objectReferenceValue = c.Find("Row_Quest_2/Label").GetComponent<TextMeshProUGUI>();
            }

            var quests = so.FindProperty("_placeholderQuests");
            if (quests != null)
            {
                quests.arraySize = 2;
                quests.GetArrayElementAtIndex(0).stringValue = "前往城堡";
                quests.GetArrayElementAtIndex(1).stringValue = "击败克里顿";
            }
            so.ApplyModifiedProperties();

            UIBuilderKit.HookClick(close.GetComponent<Button>(), qp, "Close");

            CleanupLegacy(root, "Text_Title", "Text_Content", "Button_Close");
            root.SetActive(false);//预设 inactive
        }

        /// <summary>一条任务条目：序号 + 标题 + 状态。选中态 = 暖白 + 左侧 2px 竖线。</summary>
        static void BuildQuestEntry(Transform parent, string name, string index, string label,
                                    string state, float y, float width, float centerX, bool selected)
        {
            var row = UIBuilderKit.EnsureUIObject(parent, name);
            UIBuilderKit.Place(row.GetComponent<RectTransform>(), new Vector2(centerX, y), new Vector2(width, 40f));

            if (selected)
            {
                var bar = UIBuilderKit.EnsureUIObject(row.transform, "SelBar");
                UIBuilderKit.Place(bar.GetComponent<RectTransform>(), new Vector2(-230f, 0f), new Vector2(2f, 32f));
                PaintLine(bar, UIBuilderKit.TextNormal);
            }

            var idx = UIBuilderKit.MakeText(row.transform, "Index", index, 16f,
                UIBuilderKit.TextDim, TextAlignmentOptions.Left);
            idx.characterSpacing = 2f;
            UIBuilderKit.Place(idx.rectTransform, new Vector2(-202f, 0f), new Vector2(40f, 24f));

            var lbl = UIBuilderKit.MakeText(row.transform, "Label", label, 22f,
                selected ? UIBuilderKit.TextNormal : UIBuilderKit.TextDim, TextAlignmentOptions.Left);
            lbl.characterSpacing = 2f;
            UIBuilderKit.Place(lbl.rectTransform, new Vector2(-60f, 0f), new Vector2(240f, 30f));

            var st = UIBuilderKit.MakeText(row.transform, "State", state, 15f,
                selected ? UIBuilderKit.KeyColor : UIBuilderKit.LineNormal, TextAlignmentOptions.Right);
            UIBuilderKit.Place(st.rectTransform, new Vector2(170f, 0f), new Vector2(120f, 24f));
        }

        // ================================================================
        //  3. 暂停菜单
        // ================================================================
        /// <summary>
        /// 弹窗：左列竖排按钮（左侧 2px 竖线 + 左对齐）+ 右侧"当前位置 / 操作提示"信息块。
        ///
        /// <para>⚠️ 根节点名必须是 <c>PausePanel</c>（<see cref="PauseMenu"/> 的 <c>_panel</c> 指向它）。</para>
        /// <para>⚠️ 三个按钮必须接 <b>PauseMenu</b> 上的方法，不能接 GameManager ——
        /// Tavern 里没有 GameManager，原先三个按钮全是 Missing（点了没反应）。</para>
        /// </summary>
        static void BuildPausePanel(Transform canvas)
        {
            var root = UIBuilderKit.EnsureUIObject(canvas, "PausePanel");
            UIBuilderKit.Stretch(root.GetComponent<RectTransform>());
            var bg = UIBuilderKit.GetImage(root);
            bg.sprite = null;
            bg.color = UIBuilderKit.MaskBg;
            bg.raycastTarget = true;

            var content = UIBuilderKit.EnsureUIObject(root.transform, "Content");
            UIBuilderKit.Place(content.GetComponent<RectTransform>(), Vector2.zero, new Vector2(900f, 620f));

            var c = content.transform;

            // 标题（左对齐，跟左侧按钮组对齐）
            var title = UIBuilderKit.MakeText(c, "PauseTitleText", "暂停", 34f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Left);
            title.characterSpacing = 14f;
            UIBuilderKit.Place(title.rectTransform, new Vector2(-250f, 240f), new Vector2(440f, 44f));

            var sub = UIBuilderKit.MakeText(c, "Text_PausedSub", "PAUSED", 14f,
                UIBuilderKit.KeyColor, TextAlignmentOptions.Left);
            sub.characterSpacing = 18f;
            UIBuilderKit.Place(sub.rectTransform, new Vector2(-250f, 202f), new Vector2(440f, 22f));

            UIBuilderKit.HLine(c, "Line_Head", 172f, 900f, UIBuilderKit.HairLine);

            // 左列：竖排按钮（左对齐 + 左侧细线）
            var resume = UIBuilderKit.CreateTextButton(c, "ResumeButton");
            UIBuilderKit.SetupTextButton(resume, "继续游戏", new Vector2(-250f, 110f), new Vector2(300f, 56f), false, 26f);

            var restart = UIBuilderKit.CreateTextButton(c, "RestartButton");
            UIBuilderKit.SetupTextButton(restart, "重新开始", new Vector2(-250f, 44f), new Vector2(300f, 56f), false, 26f);

            var mainMenu = UIBuilderKit.CreateTextButton(c, "MainMenuButton");
            UIBuilderKit.SetupTextButton(mainMenu, "返回主菜单", new Vector2(-250f, -22f), new Vector2(300f, 56f), false, 26f);

            // 中缝竖线
            UIBuilderKit.VLine(c, "Divider_Vertical", -70f, -20f, 300f, UIBuilderKit.HairLine);

            // 右列：场景信息
            var infoLabel = UIBuilderKit.MakeText(c, "Text_InfoLabel", "当前位置", 16f,
                UIBuilderKit.GroupColor, TextAlignmentOptions.Left);
            infoLabel.characterSpacing = 6f;
            UIBuilderKit.Place(infoLabel.rectTransform, new Vector2(180f, 130f), new Vector2(360f, 22f));

            var infoScene = UIBuilderKit.MakeText(c, "Text_InfoScene", "酒馆", 26f,
                UIBuilderKit.TextNormal, TextAlignmentOptions.Left);
            infoScene.characterSpacing = 6f;
            UIBuilderKit.Place(infoScene.rectTransform, new Vector2(180f, 96f), new Vector2(360f, 34f));

            var infoSub = UIBuilderKit.MakeText(c, "Text_InfoSub", "TAVERN　·　安全区域", 14f,
                UIBuilderKit.KeyColor, TextAlignmentOptions.Left);
            infoSub.characterSpacing = 2f;
            UIBuilderKit.Place(infoSub.rectTransform, new Vector2(180f, 62f), new Vector2(360f, 22f));

            UIBuilderKit.HLine(c, "Line_Mid", 32f, 360f, UIBuilderKit.HairLine);

            var tips = UIBuilderKit.MakeText(c, "Text_Tips",
                "WASD　　移动\nShift　　奔跑\n空格　　跳跃\n鼠标右键　翻滚\nF　　　 交互",
                15f, UIBuilderKit.TextDim, TextAlignmentOptions.TopLeft);
            tips.lineSpacing = 10f;
            UIBuilderKit.Place(tips.rectTransform, new Vector2(180f, -76f), new Vector2(360f, 140f));

            // 底部
            UIBuilderKit.HLine(c, "Line_Foot", -250f, 900f, UIBuilderKit.HairLine);

            var hint = UIBuilderKit.MakeText(c, "Text_Hint", "ESC 继续游戏", 15f,
                UIBuilderKit.TextDim, TextAlignmentOptions.Center);
            UIBuilderKit.Place(hint.rectTransform, new Vector2(0f, -285f), new Vector2(400f, 24f));

            // 接线（全部接 PauseMenu 自己）
            var pm = canvas.GetComponent<PauseMenu>();
            if (pm == null)
            {
                Debug.LogError("[TavernUI] Canvas 上没有 PauseMenu 组件，暂停菜单按钮无法接线。");
            }
            else
            {
                var so = new SerializedObject(pm);
                SetRef(so, "_panel", root);
                so.ApplyModifiedProperties();

                UIBuilderKit.HookClick(resume.GetComponent<Button>(), pm, "Resume");
                UIBuilderKit.HookClick(restart.GetComponent<Button>(), pm, "RestartScene");
                UIBuilderKit.HookClick(mainMenu.GetComponent<Button>(), pm, "BackToMainMenu");
            }

            CleanupLegacy(root, "PauseTitleText", "ResumeButton", "RestartButton", "MainMenuButton");
            root.SetActive(false);
        }

        // ================================================================
        //  交互文案本地化
        // ================================================================
        /// <summary>
        /// 把场景里 <see cref="Gameplay.Interactable"/> 的 prompt 从英文改成中文。
        /// 原值是从英文模板抄来的（<c>Check</c> / <c>Go</c>），配合中文格式串会变成"按 F Check"。
        /// </summary>
        static void FixInteractablePrompts()
        {
            var t = System.Type.GetType("Game.Gameplay.Interactable, Assembly-CSharp");
            if (t == null) { Debug.LogWarning("[TavernUI] 找不到 Interactable 类型，跳过文案本地化。"); return; }

            var all = Object.FindObjectsByType(t, FindObjectsSortMode.None);
            int changed = 0;
            foreach (var o in all)
            {
                var so = new SerializedObject(o);
                var p = so.FindProperty("prompt");
                if (p == null) continue;

                string v = p.stringValue;
                string nv = v;
                if (v == "Check") nv = "查看告示栏";
                else if (v == "Go") nv = "前往城堡";

                if (nv != v)
                {
                    p.stringValue = nv;
                    so.ApplyModifiedProperties();
                    changed++;
                }
            }
            Debug.Log("[TavernUI] 交互文案本地化：" + changed + " 处。");
        }

        // ================================================================
        //  工具
        // ================================================================
        static void SetRef(SerializedObject so, string path, Object value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[TavernUI] 字段不存在：" + path); return; }
            p.objectReferenceValue = value;
        }

        /// <summary>
        /// 删掉面板根下遗留的旧节点（v1 那套默认 Unity UI 直接挂在面板根上，
        /// 新结构统一收进 <c>Content</c>，不删的话会和新的叠在一起）。
        /// </summary>
        static void CleanupLegacy(GameObject panelRoot, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var t = panelRoot.transform.Find(names[i]);
                if (t != null)
                {
                    Debug.Log("[TavernUI] 删除遗留节点：" + names[i]);
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }
    }
}
