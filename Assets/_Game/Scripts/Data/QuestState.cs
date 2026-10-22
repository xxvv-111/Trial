using System;
using System.Collections.Generic;
using Game.Save;

namespace Game.Data
{
    /// <summary>
    /// 任务目标的**运行时**状态。与配置里的 <see cref="QuestObjective"/> 按 `objectiveId` 对齐。
    /// </summary>
    public class ObjectiveState
    {
        public string ObjectiveId;
        public QuestObjectiveType Type;
        public string TargetId;
        public int Progress;
        public int Need;
        public string Hint;

        public bool IsDone { get { return Progress >= Need; } }
    }

    /// <summary>
    /// 一条任务的**运行时**视图。UI 直接读它，不要去读 <c>QuestSave</c>（那是存档结构，字段会变）。
    /// </summary>
    public class QuestState
    {
        public string QuestId;
        public QuestStatus Status;
        public QuestConfig Config;
        public List<ObjectiveState> Objectives = new List<ObjectiveState>();

        public bool IsMainQuest { get { return Config != null && Config.IsMainQuest; } }

        public string Title { get { return Config != null ? Config.title : QuestId; } }
    }

    /// <summary>
    /// 任务的**纯逻辑规则**（不碰文件、不碰 Unity 对象）。
    ///
    /// <para>抽出来的理由：状态机是整个任务系统最容易出错的部分（什么时候可接、什么时候可交、
    /// 什么时候解锁下游），而它又是纯函数 —— 独立出来就能在编辑器里毫秒级自测，
    /// 不用进 Play、不用造存档。菜单 `Tools/任务/状态机自测`。</para>
    /// </summary>
    public static class QuestRules
    {
        /// <summary>全部目标都达标。**空列表算没完成** —— 没有目标的任务不该能被交付。</summary>
        public static bool AllObjectivesDone(List<ObjectiveState> objectives)
        {
            if (objectives == null || objectives.Count == 0) return false;

            for (int i = 0; i < objectives.Count; i++)
            {
                var o = objectives[i];
                if (o == null || o.Progress < o.Need) return false;
            }
            return true;
        }

        /// <summary>前置任务全部 `Completed`（没有前置 = true）。</summary>
        public static bool PrerequisitesDone(QuestConfig config, Func<string, QuestStatus> statusOf)
        {
            if (config == null || config.prerequisites == null || config.prerequisites.Count == 0) return true;
            if (statusOf == null) return false;

            for (int i = 0; i < config.prerequisites.Count; i++)
            {
                var pre = config.prerequisites[i];
                if (string.IsNullOrEmpty(pre)) continue;              // 配置校验会另外报，这里不挡
                if (statusOf(pre) != QuestStatus.Completed) return false;
            }
            return true;
        }

        /// <summary>
        /// 由「当前状态 + 目标是否全满 + 前置是否完成」推出新状态。
        ///
        /// 三条不可违反的规则：
        /// 1. `Completed` 是终态，永不回退；
        /// 2. 目标全满**只到 `ReadyToTurnIn`，不自动 `Completed`**（必须回酒馆点交付）；
        /// 3. `Locked → Available` 只看前置，不看进度。
        /// </summary>
        public static QuestStatus Recompute(QuestStatus current, bool allObjectivesDone, bool prerequisitesDone)
        {
            switch (current)
            {
                case QuestStatus.Completed:
                    return QuestStatus.Completed;

                case QuestStatus.ReadyToTurnIn:
                    // 理论上不会掉回去；万一配置被改少了目标，也要能自洽
                    return allObjectivesDone ? QuestStatus.ReadyToTurnIn : QuestStatus.Active;

                case QuestStatus.Active:
                    return allObjectivesDone ? QuestStatus.ReadyToTurnIn : QuestStatus.Active;

                case QuestStatus.Available:
                    return prerequisitesDone ? QuestStatus.Available : QuestStatus.Locked;

                default: // Locked
                    return prerequisitesDone ? QuestStatus.Available : QuestStatus.Locked;
            }
        }
    }
}
