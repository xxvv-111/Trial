using System.Collections.Generic;
using Game.Data;
using Game.Save;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 任务状态机自测（14 项）。只测纯逻辑的 <see cref="QuestRules"/> —— 不碰 Unity 对象、
/// 不碰存档、不进 Play，毫秒级。
///
/// 用法：菜单 Tools > 任务 > 状态机自测。
/// </summary>
public static class QuestRulesSelfTest
{
    static int _pass;
    static int _fail;
    static readonly List<string> _failures = new List<string>();

    [MenuItem("Tools/任务/状态机自测", false, 3)]
    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        _failures.Clear();

        // ── AllObjectivesDone ──
        Check("空目标列表算未完成", !QuestRules.AllObjectivesDone(new List<ObjectiveState>()));

        var half = new List<ObjectiveState>
        {
            new ObjectiveState { ObjectiveId = "a", Progress = 1, Need = 1 },
            new ObjectiveState { ObjectiveId = "b", Progress = 0, Need = 2 },
        };
        Check("2 个目标完成 1 个 → 未完成", !QuestRules.AllObjectivesDone(half));

        var full = new List<ObjectiveState>
        {
            new ObjectiveState { ObjectiveId = "a", Progress = 1, Need = 1 },
            new ObjectiveState { ObjectiveId = "b", Progress = 2, Need = 2 },
        };
        Check("2 个目标全满 → 完成", QuestRules.AllObjectivesDone(full));

        // ── PrerequisitesDone ──
        // ⚠️ ScriptableObject 不能 new（Unity 会警告且对象不完整），测试用完 DestroyImmediate
        var noPre = ScriptableObject.CreateInstance<QuestConfig>();
        Check("无前置 → 可接", QuestRules.PrerequisitesDone(noPre, id => QuestStatus.Active));

        var pre = ScriptableObject.CreateInstance<QuestConfig>();
        pre.prerequisites = new List<string> { "q1", "q2" };
        var statuses = new Dictionary<string, QuestStatus>
        {
            { "q1", QuestStatus.Completed },
            { "q2", QuestStatus.Active },
        };
        Check("前置 1 个没完成 → 不可接", !QuestRules.PrerequisitesDone(pre, id => statuses[id]));

        statuses["q2"] = QuestStatus.Completed;
        Check("前置全部完成 → 可接", QuestRules.PrerequisitesDone(pre, id => statuses[id]));

        // ── Recompute ──
        Check("Locked + 前置未完成 → Locked",
            QuestRules.Recompute(QuestStatus.Locked, false, false) == QuestStatus.Locked);

        Check("Locked + 前置完成 → Available",
            QuestRules.Recompute(QuestStatus.Locked, false, true) == QuestStatus.Available);

        Check("Available + 前置完成 → Available",
            QuestRules.Recompute(QuestStatus.Available, false, true) == QuestStatus.Available);

        Check("Active + 目标未满 → Active",
            QuestRules.Recompute(QuestStatus.Active, false, true) == QuestStatus.Active);

        Check("Active + 目标全满 → ReadyToTurnIn（不自动完成）",
            QuestRules.Recompute(QuestStatus.Active, true, true) == QuestStatus.ReadyToTurnIn);

        Check("ReadyToTurnIn + 目标未满 → 退回 Active",
            QuestRules.Recompute(QuestStatus.ReadyToTurnIn, false, true) == QuestStatus.Active);

        Check("Completed 是终态（目标全满）",
            QuestRules.Recompute(QuestStatus.Completed, true, true) == QuestStatus.Completed);

        Check("Completed 是终态（目标未满也不回退）",
            QuestRules.Recompute(QuestStatus.Completed, false, true) == QuestStatus.Completed);

        Object.DestroyImmediate(noPre);
        Object.DestroyImmediate(pre);

        if (_fail == 0)
        {
            Debug.Log($"<color=green>[QuestRulesSelfTest] {_pass}/{_pass} 全部通过</color>");
            return;
        }

        Debug.LogError($"[QuestRulesSelfTest] {_pass} 通过 / {_fail} 失败");
        for (int i = 0; i < _failures.Count; i++) Debug.LogError("  ✗ " + _failures[i]);
    }

    static void Check(string name, bool ok)
    {
        if (ok) { _pass++; return; }
        _fail++;
        _failures.Add(name);
    }
}
