using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 由 Roskva 双版本模型生成两个玩家预制体。
///
/// 方案（实查后定稿，2026-10-05）
/// ------------------------------------------------------------------
/// 每个预制体的**根 = 对应 FBX 的嵌套实例本身**（模型即预制体根），
/// 所以模型换/重导时预制体自动跟随，不需要"再挂一次模型"。
///
///     Player1  ←  `OVR - Roskva.fbx`        背剑版（9 网格，剑在 Equips 里）
///     Player2  ←  `OVR - Roskva_Sword.fbx`  持剑版（10 网格，多出 Roskva_Sword_Hand）
///
/// 两者的"玩家身份"完全一致：从现有 `Player.prefab` 逐组件复制而来 ——
///     8 个玩法脚本：PlayerMotor / PlayerDash / PlayerFSM / PlayerAttack /
///                   PlayerHealth / PlayerEnergy / AttackFxBridge / HitboxController
///     Animator（Avatar = 本模型自带的 Humanoid Avatar；Controller = PlayerAC）
///     CharacterController（⚠️ PlayerMotor 第 56 行 `_cc.Move(...)` 强依赖它，
///                          且 PlayerMotor/PlayerAttack 都要求 Animator 在**同一个物体**上）
///     4 个判定体子物体 Hitbox_Attack1~4（BoxCollider + kinematic Rigidbody + Hitbox）
///
/// ⚠️ 刻意**不加** CapsuleCollider、也不加根 Rigidbody ——
///    这是遗留问题 T2 的处理方向（"三套物理组件并存，只保留 CharacterController"）。
///    Player.prefab 上那个 CapsuleCollider 是历史遗留；新预制体不再复制它。
///
/// ⚠️ 与 `Player.prefab` 的关系：**只读不写**。Player.prefab 只作为"组件模板"被读取，
///    本工具不会修改它，也不会修改任何 FBX。
///
/// ⚠️ 判定体的「层」和「位置」按**世界坐标**搬运（不按 localPosition），
///    这样根物体从 Y Bot 换成 Roskva 也不会让判定体跑位。
///
/// 菜单：`Tools ▸ Roskva ▸ 双版本玩家预制体`
/// </summary>
public static class CreatePlayerPrefabs
{
    const string MENU = "Tools/Roskva/双版本玩家预制体/";

    const string FBX_DIR     = "Assets/_Game/Art/characters/Roskva/_model/fbx/";
    const string SRC_PLAYER  = "Assets/_Game/Prefabs/Player/Player.prefab";
    const string PLAYER_AC   = "Assets/_Game/Art/Animations/Player/PlayerAC.controller";
    const string OUT_DIR     = "Assets/_Game/Prefabs/Player/";

    const int    PLAYER_LAYER = 8;
    const string PLAYER_TAG   = "Player";

    /// <summary>三套物理组件的目标值（与 Player.prefab 里的 CapsuleCollider 对齐）。</summary>
    const float CC_RADIUS = 0.3f;
    const float CC_HEIGHT = 2f;
    static readonly Vector3 CC_CENTER = new Vector3(0f, 0.8f, 0f);

    static readonly string[] HitboxNames =
        { "Hitbox_Attack1", "Hitbox_Attack2", "Hitbox_Attack3", "Hitbox_Attack4" };
    const string HITBOXES_FIELD = "_hitboxes";

    class Spec
    {
        public string Fbx;
        public string Name;
        public string Desc;
    }

    static readonly Spec[] Specs =
    {
        new Spec { Fbx = FBX_DIR + "OVR - Roskva.fbx",       Name = "Player1", Desc = "背剑版" },
        new Spec { Fbx = FBX_DIR + "OVR - Roskva_Sword.fbx", Name = "Player2", Desc = "持剑版" },
    };

    // ==================================================================
    //  菜单
    // ==================================================================
    [MenuItem(MENU + "1. 生成 Player1 + Player2", false, 1)]
    public static void CreateBoth()
    {
        var rep = new StringBuilder();
        int ok = 0;
        foreach (var s in Specs)
        {
            rep.AppendLine("──────────────────────────────────────────");
            if (Build(s, rep)) ok++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 生成后立刻自校验（同一份报告，省得再点一次）
        rep.AppendLine();
        rep.AppendLine("══════════════ 校验 ══════════════");
        foreach (var s in Specs)
        {
            rep.AppendLine();
            rep.AppendLine(Verify(OUT_DIR + s.Name + ".prefab", s.Name, s));
        }
        rep.AppendLine();
        rep.AppendLine("成功 " + ok + " / " + Specs.Length + " 个。");
        rep.AppendLine("Player.prefab 与两个 FBX 均未被修改。");
        Debug.Log("[PlayerPrefabs]\n" + rep);
        EditorUtility.DisplayDialog(ok == Specs.Length ? "完成" : "部分失败",
            "生成 " + ok + "/" + Specs.Length + " 个预制体。\n详细报告见 Console（含校验）。", "好");
    }

    [MenuItem(MENU + "2. 只做校验：Player1 / Player2", false, 2)]
    public static void VerifyMenu()
    {
        var rep = new StringBuilder();
        rep.AppendLine("=========== 预制体校验 ===========");
        foreach (var s in Specs)
        {
            rep.AppendLine();
            rep.AppendLine(Verify(OUT_DIR + s.Name + ".prefab", s.Name, s));
        }
        Debug.Log("[PlayerPrefabs]\n" + rep);
        EditorUtility.DisplayDialog("校验完成", "结果见 Console。", "好");
    }

    // ==================================================================
    //  生成
    // ==================================================================
    static bool Build(Spec spec, StringBuilder rep)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx);
        if (model == null)
        {
            rep.AppendLine("[" + spec.Name + "] 找不到模型：" + spec.Fbx);
            return false;
        }
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(SRC_PLAYER);
        if (template == null)
        {
            rep.AppendLine("[" + spec.Name + "] 找不到组件模板：" + SRC_PLAYER);
            return false;
        }
        string outPath = OUT_DIR + spec.Name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null &&
            !EditorUtility.DisplayDialog("已存在",
                spec.Name + ".prefab 已存在，覆盖吗？", "覆盖", "跳过"))
        {
            rep.AppendLine("[" + spec.Name + "] 用户选择跳过。");
            return false;
        }

        rep.AppendLine("【" + spec.Name + "】" + spec.Desc + "  源: " + spec.Fbx);

        var playerContents = PrefabUtility.LoadPrefabContents(SRC_PLAYER);
        // ⚠️ 在**隐藏的预览场景**里干活：InstantiantePrefab 默认会往当前打开的场景里塞物体，
        //    那会把用户正在编辑的场景标脏（退出时弹"是否保存"）。预览场景没有这个问题。
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        bool ok = false;
        try
        {
            // --- 1) 根 = 模型本身 ---
            root = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
            if (root == null)
            {
                rep.AppendLine("  实例化失败。");
                return false;
            }
            root.name = spec.Name;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = PLAYER_LAYER;
            root.tag = PLAYER_TAG;
            rep.AppendLine("  根物体: " + spec.Name + "  (layer=" + root.layer + ", tag=" + root.tag + ")");

            // --- 2) 复制根上的玩法脚本（跳过 Transform / Animator / 物理组件）---
            int copied = 0;
            var skipped = new List<string>();
            foreach (var src in playerContents.GetComponents<Component>())
            {
                if (src == null) { skipped.Add("(丢失脚本)"); continue; }
                if (src is Transform || src is Animator) continue;
                if (src is Collider || src is Rigidbody)     // T2：物理组件不沿用，见类注释
                {
                    skipped.Add(src.GetType().Name + "(按方案跳过)");
                    continue;
                }
                var t = src.GetType();
                try
                {
                    var dst = root.GetComponent(t);
                    if (dst == null) dst = root.AddComponent(t);
                    EditorUtility.CopySerialized(src, dst);
                    copied++;
                }
                catch (System.Exception e)
                {
                    skipped.Add(t.Name + "(" + e.GetType().Name + ")");
                }
            }
            rep.AppendLine("  玩法脚本: 复制 " + copied + " 个" +
                (skipped.Count > 0 ? "  未复制: " + string.Join(", ", skipped.ToArray()) : ""));

            // --- 3) Animator（必须与玩法脚本同物体 —— PlayerMotor 用 GetComponent 取）---
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            var tmplAnim = playerContents.GetComponent<Animator>();
            anim.applyRootMotion = false;
            if (tmplAnim != null)
            {
                anim.cullingMode = tmplAnim.cullingMode;
                anim.updateMode = tmplAnim.updateMode;
            }
            anim.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PLAYER_AC);
            anim.avatar = LoadOwnAvatar(spec.Fbx);
            rep.AppendLine("  Animator: Avatar=" + (anim.avatar != null ? anim.avatar.name : "null") +
                " isHuman=" + (anim.avatar != null && anim.avatar.isHuman) +
                "  Controller=" + (anim.runtimeAnimatorController != null
                    ? anim.runtimeAnimatorController.name : "null"));

            // --- 4) CharacterController（PlayerMotor 强依赖）---
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.radius = CC_RADIUS;
            cc.height = CC_HEIGHT;
            cc.center = CC_CENTER;
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.08f;
            rep.AppendLine(string.Format("  CharacterController: r={0} h={1} center={2}",
                cc.radius, cc.height, cc.center));

            // --- 5) 判定体子物体（按世界坐标搬运）---
            var newHitboxes = new List<Component>();
            int hb = 0;
            foreach (var hn in HitboxNames)
            {
                var src = FindDeep(playerContents.transform, hn);
                if (src == null) { rep.AppendLine("  判定体 " + hn + ": 模板里没有"); continue; }
                var go = new GameObject(hn);
                go.layer = src.gameObject.layer;
                go.transform.SetParent(root.transform, false);
                go.transform.position = src.transform.position;          // 世界坐标，抗根物体差异
                go.transform.rotation = src.transform.rotation;
                go.transform.localScale = src.transform.localScale;
                int c = 0;
                foreach (var sc in src.GetComponents<Component>())
                {
                    if (sc == null || sc is Transform) continue;
                    var st = sc.GetType();
                    var dst = go.AddComponent(st);
                    EditorUtility.CopySerialized(sc, dst);
                    c++;
                    if (st.Name == "Hitbox") newHitboxes.Add(dst);
                }
                hb++;
                rep.AppendLine(string.Format("  判定体 {0}: {1} 组件, layer={2}, 世界坐标={3}",
                    hn, c, go.layer, go.transform.position));
            }

            // --- 6) 重连 HitboxController._hitboxes ---
            rep.AppendLine("  _hitboxes 重连: " + RelinkHitboxes(root, newHitboxes, rep) + " 个");

            // --- 7) 保存 ---
            var saved = PrefabUtility.SaveAsPrefabAsset(root, outPath);
            ok = saved != null;
            rep.AppendLine("  保存: " + outPath + (ok ? "  OK" : "  失败"));
            rep.AppendLine("  判定体共 " + hb + " / " + HitboxNames.Length);
        }
        catch (System.Exception e)
        {
            rep.AppendLine("  出错: " + e);
            ok = false;
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            PrefabUtility.UnloadPrefabContents(playerContents);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        return ok;
    }

    /// <summary>取 FBX 自己生成的 Humanoid Avatar。</summary>
    static Avatar LoadOwnAvatar(string fbxPath)
    {
        Avatar fallback = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            var av = o as Avatar;
            if (av == null) continue;
            if (av.isHuman) return av;
            if (fallback == null) fallback = av;
        }
        return fallback;
    }

    static int RelinkHitboxes(GameObject root, List<Component> hitboxes, StringBuilder rep)
    {
        foreach (var c in root.GetComponents<Component>())
        {
            if (c == null) continue;
            var so = new SerializedObject(c);
            var prop = so.FindProperty(HITBOXES_FIELD);
            if (prop == null || !prop.isArray) continue;
            prop.arraySize = hitboxes.Count;
            for (int i = 0; i < hitboxes.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = hitboxes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            rep.AppendLine("    在 " + c.GetType().Name + " 上写入 " + hitboxes.Count + " 个");
            return hitboxes.Count;
        }
        rep.AppendLine("    ⚠️ 没找到带 " + HITBOXES_FIELD + " 的组件");
        return 0;
    }

    // ==================================================================
    //  校验
    // ==================================================================
    static string Verify(string path, string label, Spec spec)
    {
        var sb = new StringBuilder();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null) return "【" + label + "】不存在：" + path;
        sb.AppendLine("【" + label + "】" + spec.Desc + "  " + path);

        var srcObj = PrefabUtility.GetCorrespondingObjectFromSource(root);
        string srcPath = srcObj != null ? AssetDatabase.GetAssetPath(srcObj) : "";
        sb.AppendLine("  根 = 模型实例: " + (string.IsNullOrEmpty(srcPath) ? "(不是模型实例!)" : srcPath));
        bool srcOk = srcPath == spec.Fbx;
        sb.AppendLine("  源模型匹配: " + (srcOk ? "✅" : "❌ 期望 " + spec.Fbx));
        sb.AppendLine("  layer=" + root.layer + "  tag=" + root.tag);

        var comps = new List<string>();
        foreach (var c in root.GetComponents<Component>())
            if (c != null && !(c is Transform)) comps.Add(c.GetType().Name);
        sb.AppendLine("  根组件(" + comps.Count + "): " + string.Join(", ", comps.ToArray()));

        var anim = root.GetComponent<Animator>();
        if (anim != null)
            sb.AppendLine("  Animator: Avatar=" + (anim.avatar != null ? anim.avatar.name : "null") +
                " isHuman=" + (anim.avatar != null && anim.avatar.isHuman) +
                "  Controller=" + (anim.runtimeAnimatorController != null
                    ? anim.runtimeAnimatorController.name : "null"));
        else sb.AppendLine("  Animator: ❌ 缺失（PlayerMotor 会 NRE）");

        if (root.GetComponent<CharacterController>() == null)
            sb.AppendLine("  CharacterController: ❌ 缺失（PlayerMotor 会 NRE）");
        if (root.GetComponent<CapsuleCollider>() != null)
            sb.AppendLine("  ⚠️ 仍带 CapsuleCollider（按方案应已去掉）");

        // 关键脚本是否齐
        string[] need = { "PlayerMotor", "PlayerDash", "PlayerFSM", "PlayerAttack",
                          "PlayerHealth", "PlayerEnergy", "AttackFxBridge", "HitboxController" };
        var miss = new List<string>();
        foreach (var n in need)
        {
            bool found = false;
            foreach (var c in root.GetComponents<Component>())
                if (c != null && c.GetType().Name == n) { found = true; break; }
            if (!found) miss.Add(n);
        }
        sb.AppendLine("  必备脚本: " + (miss.Count == 0 ? "8/8 ✅" : "缺 " + string.Join(", ", miss.ToArray())));

        // 判定体
        int hbFound = 0;
        foreach (var hn in HitboxNames)
        {
            var t = FindDeep(root.transform, hn);
            if (t == null) { sb.AppendLine("  判定体 " + hn + ": ❌ 缺"); continue; }
            hbFound++;
            var col = t.GetComponent<BoxCollider>();
            var rb = t.GetComponent<Rigidbody>();
            sb.AppendLine(string.Format("  判定体 {0}: layer={1} 世界坐标={2} BoxCollider={3} Rigidbody(kinematic)={4}",
                hn, t.gameObject.layer, t.position,
                col != null ? "有" : "❌无",
                rb != null ? rb.isKinematic.ToString() : "❌无"));
        }
        sb.AppendLine("  判定体齐: " + hbFound + "/4");
        sb.AppendLine("  HitboxController._hitboxes: " + CountRefs(root, HITBOXES_FIELD) + " 个");

        // 模型侧
        int smr = 0, bones = 0;
        var names = new List<string>();
        foreach (var m in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr++;
            bones = Mathf.Max(bones, m.bones.Length);
            names.Add(m.name);
        }
        sb.AppendLine("  蒙皮网格 " + smr + " 个: " + string.Join(", ", names.ToArray()));
        sb.AppendLine("  单网格最大骨骼数: " + bones);

        string[] keyBones = { "Bip001-Pelvis", "Bip001-Head", "B_Weapon_R", "B_Weapon_L", "B_B_Knife" };
        foreach (var bn in keyBones)
            sb.AppendLine("  骨骼 " + bn + ": " + (FindDeep(root.transform, bn) != null ? "✅" : "❌"));
        sb.AppendLine("  含剑网格 Roskva_Sword_Hand: " +
            (FindDeep(root.transform, "Roskva_Sword_Hand") != null ? "✅（持剑版）" : "—（背剑版，剑在 Equips 里）"));
        return sb.ToString().TrimEnd();
    }

    static int CountRefs(GameObject root, string field)
    {
        foreach (var c in root.GetComponents<Component>())
        {
            if (c == null) continue;
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.isArray) return p.arraySize;
        }
        return -1;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
