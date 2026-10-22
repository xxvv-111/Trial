using Game.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 任务表校验菜单。等价于 QuestService 启动时要做的那次检查，只是提前到编辑器里跑。
///
/// 为什么值得单独做个菜单：前置任务写错 / 成环 / 引用不存在的 id，运行时的表现都是
/// "某条任务永远锁着且没有任何提示" —— 那种问题靠玩是查不出来的。
/// </summary>
public static class QuestCatalogMenu
{
    [MenuItem("Tools/任务/校验任务表", false, 1)]
    public static void Validate()
    {
        var catalog = FindCatalog();
        if (catalog == null)
        {
            Debug.LogError("[任务] 场景/工程里找不到 QuestCatalog 资产（预期在 Assets/_Game/Scripts/Data/Quests/）");
            return;
        }

        var errors = catalog.Validate();

        if (errors.Count == 0)
        {
            Debug.Log($"<color=green>[任务] 校验通过：{catalog.All.Count} 条任务，前置图无环、目标完整</color>");
            PrintSummary(catalog);
            return;
        }

        Debug.LogError($"[任务] 校验失败：{errors.Count} 个问题");
        for (int i = 0; i < errors.Count; i++) Debug.LogError("  ✗ " + errors[i]);
    }

    [MenuItem("Tools/任务/打印任务表", false, 2)]
    public static void PrintSummaryMenu()
    {
        var catalog = FindCatalog();
        if (catalog == null) { Debug.LogError("[任务] 找不到 QuestCatalog"); return; }
        PrintSummary(catalog);
    }

    static void PrintSummary(QuestCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[任务] {catalog.All.Count} 条任务：");
        for (int i = 0; i < catalog.All.Count; i++)
        {
            var q = catalog.All[i];
            if (q == null) { sb.AppendLine($"  [{i}] None"); continue; }

            var pre = q.prerequisites != null && q.prerequisites.Count > 0
                ? string.Join(",", q.prerequisites.ToArray())
                : "无";
            sb.AppendLine($"  [{i}] {q.questId}｜{q.title}｜{(q.IsMainQuest ? "主线" : "支线")}｜" +
                          $"前置={pre}｜地点={q.targetScene}｜目标 {q.ObjectiveCount} 个");

            if (q.objectives == null) continue;
            for (int k = 0; k < q.objectives.Count; k++)
            {
                var o = q.objectives[k];
                if (o == null) { sb.AppendLine("      - None"); continue; }
                sb.AppendLine($"      - {o.type} {o.targetId} × {o.needCount}｜{o.hint}");
            }
        }
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/任务/打印任务状态（需 Play）", false, 4)]
    public static void PrintRuntimeStates()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[任务] 该菜单只在 Play 模式下有意义（运行时状态由 QuestService 持有）");
            return;
        }

        var svc = Game.Gameplay.QuestService.Instance;
        if (svc == null) { Debug.LogError("[任务] QuestService.Instance 为空 —— 服务没创建成功？"); return; }
        if (!svc.IsReady) { Debug.LogWarning("[任务] QuestService 还没初始化完（等一帧再试）"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[任务] 运行时状态（{svc.All.Count} 条）：");
        for (int i = 0; i < svc.All.Count; i++)
        {
            var st = svc.All[i];
            sb.AppendLine($"  {st.QuestId}｜{st.Title}｜{st.Status}");
            for (int k = 0; k < st.Objectives.Count; k++)
            {
                var o = st.Objectives[k];
                sb.AppendLine($"      - {o.Hint}（{o.Progress}/{o.Need}）{(o.IsDone ? " ✓" : "")}");
            }
        }
        Debug.Log(sb.ToString());
    }

    static QuestCatalog FindCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:QuestCatalog");
        if (guids == null || guids.Length == 0) return null;
        if (guids.Length > 1)
            Debug.LogWarning($"[任务] 工程里有 {guids.Length} 个 QuestCatalog，用第一个：{AssetDatabase.GUIDToAssetPath(guids[0])}");

        return AssetDatabase.LoadAssetAtPath<QuestCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
