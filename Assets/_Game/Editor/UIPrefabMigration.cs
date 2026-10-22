using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Game.Gameplay;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// **一次性迁移工具**（2026-10-21）：把 Castle 场景里堆在 Canvas 上的 UI 抽成两个共用预制体，
    /// 让 Castle / Forest 共用同一份 UI，以后改一次两边生效。
    ///
    /// <para>产出：</para>
    /// <code>
    /// Assets/_Game/Prefabs/UI/HUD_Common.prefab   ← 两场景共用（Canvas + EventSystem + PauseMenu
    ///                                                + HP/Mana/SkillCD/RoomHint/结算面板 + GameManager）
    /// Assets/_Game/Prefabs/UI/BossBar.prefab      ← 独立 Screen Space Canvas（sortingOrder -1）
    ///                                                再嵌进 Boss_Crichton.prefab 内部
    /// </code>
    ///
    /// <para>⚠️ 顺序不能乱：1 → 2 → 3 → 4。每一步都可以单独重跑（带幂等处理）。</para>
    /// <para>⚠️ 跑之前请先提交/备份场景：本工具会**删除**场景里临时的 BossBarCanvas，
    /// 并把 Canvas 变成预制体实例。</para>
    /// </summary>
    public static class UIPrefabMigration
    {
        private const string UiDir = "Assets/_Game/Prefabs/UI";
        private const string HudPath = UiDir + "/HUD_Common.prefab";
        //⚠️ 文件名必须与预制体根节点同名：SaveAsPrefabAsset 会把根节点**强制改成文件名**，
        //   写 "BossBar.prefab" 会把根从 BossBarCanvas 改名为 BossBar（2026-10-21 实测）。
        private const string BossBarPath = UiDir + "/BossBarCanvas.prefab";
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemy/Boss_Crichton.prefab";
        private const string ForestScenePath = "Assets/Scenes/Forest.unity";

        /// <summary>主 HUD 画布的分辨率基准，与 Castle 现有 CanvasScaler 保持一致。</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ==================== 1. BossBar 预制体 ====================

        /// <summary>
        /// 把 Castle <c>Canvas/BossBar</c> 包一层独立 Canvas 存成预制体。
        ///
        /// ⚠️ 必须**先把新节点脱离父 Canvas 再配置**，两个坑（2026-10-21 实测）：
        ///   1. Canvas 嵌套在另一个 Canvas 下时，Unity **忽略** <c>renderMode</c> 与
        ///      <c>sortingOrder</c> 的赋值 —— 存进预制体后会变成 WorldSpace / 0；
        ///   2. 新建节点默认 RectTransform 只有 100×100，而 BossBar 是
        ///      anchorMin(0,0)/anchorMax(1,1) 的**全拉伸**子物体 ⇒ 留在里面会被压成屏幕中央一个小方块。
        /// 脱离父级成为根 Canvas 后，Unity 会把根 rect 驱动成全屏，全拉伸的 BossBar 自然铺满。
        /// </summary>
        [MenuItem("Tools/UI 共用化/1 建立 BossBar 预制体", false, 1)]
        public static void Step1_CreateBossBarPrefab()
        {
            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[UI迁移] 当前场景里找不到名为 Canvas 的对象。请先打开 Castle 场景。");
                return;
            }

            var wrap = canvasGo.transform.Find("BossBarCanvas") as RectTransform;

            // 上一次跑到一半留下的（已脱离父级的根对象）
            if (wrap == null)
            {
                var leftover = GameObject.Find("BossBarCanvas");
                if (leftover != null) wrap = (RectTransform)leftover.transform;
            }

            if (wrap == null)
            {
                var bar = canvasGo.transform.Find("BossBar");
                if (bar == null)
                {
                    Debug.LogError("[UI迁移] 找不到 Canvas/BossBar。");
                    return;
                }

                var go = new GameObject("BossBarCanvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                wrap = (RectTransform)go.transform;
                // worldPositionStays = false ⇒ BossBar 的局部 rect 一字不改
                ((RectTransform)bar).SetParent(wrap, false);
            }

            //⭐ 关键顺序：先脱离父 Canvas（否则下面两行赋值会被 Unity 忽略）
            if (wrap.parent != null) wrap.SetParent(null, true);
            wrap.name = "BossBarCanvas";
            wrap.localScale = Vector3.one;

            var canvas = wrap.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            //⭐ 低于主 HUD(=0)：暂停面板 / 结算面板画在 HUD 画布上，要能盖住 Boss 血条
            canvas.sortingOrder = -1;

            var scaler = wrap.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            // 预制体资产存不下「对场景实例的引用」，先清空；第 2 步会在 Boss 预制体内部重新接线
            var bhb = wrap.GetComponentInChildren<BossHealthBar>(true);
            if (bhb != null)
            {
                var so = new SerializedObject(bhb);
                ClearRef(so, "_boss");
                ClearRef(so, "_health");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureFolder(UiDir);
            PrefabUtility.SaveAsPrefabAsset(wrap.gameObject, BossBarPath, out bool ok);
            Debug.Log(ok
                ? $"[UI迁移] 第 1 步完成：{BossBarPath}（根 = BossBarCanvas，子 = BossBar）"
                : "[UI迁移] 第 1 步失败：预制体没有生成。");
        }

        // ==================== 2. 嵌进 Boss 预制体 ====================

        /// <summary>
        /// 把 BossBar 预制体嵌进 <c>Boss_Crichton.prefab</c>，并把血条的 _boss / _health
        /// **直接接到 Boss 预制体根上的组件** —— 于是「Boss 拖进任意场景就自带血条」。
        /// </summary>
        [MenuItem("Tools/UI 共用化/2 嵌入 Boss 预制体", false, 2)]
        public static void Step2_NestBossBarIntoBossPrefab()
        {
            var barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossBarPath);
            if (barPrefab == null)
            {
                Debug.LogError($"[UI迁移] 找不到 {BossBarPath}，请先跑第 1 步。");
                return;
            }

            var bossRoot = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                // 幂等：重复执行时先把旧的那份去掉
                var old = bossRoot.transform.Find("BossBarCanvas");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab, bossRoot.transform);
                inst.name = "BossBarCanvas";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                var bhb = inst.GetComponentInChildren<BossHealthBar>(true);
                var bc = bossRoot.GetComponent<BossController>();
                var eh = bossRoot.GetComponent<EnemyHealth>();

                if (bhb == null) { Debug.LogError("[UI迁移] BossBar 预制体里没有 BossHealthBar。"); return; }
                if (bc == null || eh == null)
                {
                    Debug.LogError($"[UI迁移] {BossPrefabPath} 根上缺 BossController / EnemyHealth，" +
                                   "血条无法接线。");
                    return;
                }

                var so = new SerializedObject(bhb);
                var pb = so.FindProperty("_boss");
                var ph = so.FindProperty("_health");
                if (pb == null || ph == null)
                {
                    Debug.LogError("[UI迁移] BossHealthBar 的字段名变了（_boss / _health 找不到）。");
                    return;
                }
                pb.objectReferenceValue = bc;
                ph.objectReferenceValue = eh;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(bossRoot, BossPrefabPath);
                Debug.Log($"[UI迁移] 第 2 步完成：BossBarCanvas 已嵌入 {BossPrefabPath}，" +
                          "_boss/_health 指向 Boss 预制体自身。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(bossRoot);
            }

            // 场景里临时包的那份删掉 —— Boss 预制体实例现在自带血条，留着会重影
            var tmp = GameObject.Find("BossBarCanvas");
            if (tmp != null)
            {
                Object.DestroyImmediate(tmp);
                Debug.Log("[UI迁移] 已删除场景里临时的 BossBarCanvas（改由 Boss 预制体提供）。");
            }
        }

        // ==================== 3. HUD_Common 预制体 ====================

        /// <summary>
        /// 把 Castle 的 Canvas 存成 <c>HUD_Common.prefab</c>，并把 GameManager / EventSystem
        /// 收进 Canvas 一起打包 —— 这两个都没有场景依赖，收进来后整份 HUD 零场景引用。
        /// </summary>
        [MenuItem("Tools/UI 共用化/3 建立 HUD_Common 预制体", false, 3)]
        public static void Step3_CreateHudPrefab()
        {
            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[UI迁移] 当前场景里找不到 Canvas。请先打开 Castle 场景。");
                return;
            }

            //---- 收拢 GameManager / EventSystem ----
            //GameManager 只依赖 GameEvents + InputService，零场景引用；
            //放进预制体后它对三个结算面板的引用就变成「预制体内部引用」，换场景不会 Missing。
            var gm = FindRootByName("GameManager");
            if (gm != null && !gm.transform.IsChildOf(canvasGo.transform))
            {
                Undo.SetTransformParent(gm.transform, canvasGo.transform, "移动 GameManager 进 Canvas");
            }

            var es = FindRootByName("EventSystem");
            if (es != null && !es.transform.IsChildOf(canvasGo.transform))
            {
                Undo.SetTransformParent(es.transform, canvasGo.transform, "移动 EventSystem 进 Canvas");
            }

            //---- 清掉 HPBar / ManaBar 对玩家实例的引用 ----
            //Castle 用 Player2、Forest 用 Player3，预制体里必须留空，改由脚本运行时自动查找。
            int cleared = 0;
            foreach (var hp in canvasGo.GetComponentsInChildren<HPBar>(true))
            {
                var so = new SerializedObject(hp);
                if (ClearRef(so, "_player")) { so.ApplyModifiedPropertiesWithoutUndo(); cleared++; }
            }
            foreach (var mp in canvasGo.GetComponentsInChildren<ManaBar>(true))
            {
                var so = new SerializedObject(mp);
                if (ClearRef(so, "_player")) { so.ApplyModifiedPropertiesWithoutUndo(); cleared++; }
            }

            EnsureFolder(UiDir);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, HudPath,
                InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log($"[UI迁移] 第 3 步完成：{HudPath}（已清空 {cleared} 个玩家引用，" +
                      "GameManager / EventSystem 已收纳）");
        }

        // ==================== 4. Forest 场景放置 ====================

        /// <summary>在 Forest 场景里放一份 HUD_Common 实例（Forest 独有内容请另挂，不要改进预制体）。</summary>
        [MenuItem("Tools/UI 共用化/4 放置到 Forest 场景", false, 4)]
        public static void Step4_SetupForest()
        {
            if (EditorSceneManager.GetActiveScene().path != ForestScenePath)
            {
                Debug.LogError($"[UI迁移] 请先打开 {ForestScenePath} 再跑第 4 步。");
                return;
            }

            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (hud == null)
            {
                Debug.LogError($"[UI迁移] 找不到 {HudPath}，请先跑第 3 步。");
                return;
            }

            var existing = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.LogWarning($"[UI迁移] Forest 里已经有 Canvas（{existing.name}），跳过，避免重复。");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(hud);
            inst.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(inst, "放置 HUD_Common");

            EditorSceneManager.MarkSceneDirty(inst.scene);
            Debug.Log($"[UI迁移] 第 4 步完成：Forest 已放置 {HudPath} 实例。");
        }

        // ==================== 工具 ====================

        private static bool ClearRef(SerializedObject so, string propName)
        {
            var p = so.FindProperty(propName);
            if (p == null) return false;
            if (p.objectReferenceValue == null) return false;
            p.objectReferenceValue = null;
            return true;
        }

        private static GameObject FindRootByName(string name)
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name) return go;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
