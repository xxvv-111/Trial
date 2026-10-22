using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 一条任务的**静态配置**（策划资产）。运行时状态（接了没、进度多少）在
    /// <c>Game.Save.QuestSave</c> 里，两者按 <see cref="questId"/> 对齐。
    ///
    /// <para>⭐ **一条任务 = 一组目标**（2026-10-22 拍板）：`objectives` 是列表，全部完成才可交付。
    /// 「到达场景」因此不是任务类型，而是目标类型之一 —— 它可以作为复合任务的开场步骤。</para>
    ///
    /// <para>⭐ **前置任务链**：`prerequisites` 里的任务全部 `Completed` 后，本任务才从
    /// `Locked` 变 `Available`；留空 = 无条件。可以同时接多条，不设并行上限。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "QuestConfig", menuName = "Game/Quest/QuestConfig")]
    public class QuestConfig : ScriptableObject
    {
        [Header("身份")]
        [Tooltip("唯一 id，⭐ 存档里存的就是它，**定下别改**。建议 `quest_序号_短名`，例 `quest_01_depart`。")]
        public string questId;

        [Tooltip("列表与详情里显示的标题。")]
        public string title;

        [TextArea(2, 6)]
        [Tooltip("详情面板的描述文案。")]
        public string description;

        [Tooltip("主线 / 支线。目前只影响 UI 上的标记，不影响逻辑。")]
        public QuestCategory category = QuestCategory.Side;

        [Header("前置任务（全部 Completed 才可接取；留空 = 无条件）")]
        [Tooltip("填对方的 questId。⚠️ 必须是本任务表里存在的任务，且不能成环 —— " +
                 "菜单 Tools/任务/校验任务表 会检查。")]
        public List<string> prerequisites = new List<string>();

        [Header("目标（1~N 个，全部完成才能交付）")]
        public List<QuestObjective> objectives = new List<QuestObjective>();

        [Header("地点与奖励")]
        [Tooltip("任务地点：必须是 SceneCatalog 里登记过的 sceneName（例 Forest）。\n" +
                 "接取时酒馆大门会被指到这里；留空 = 不动大门。")]
        public string targetScene;

        [Tooltip("交付时发放的奖励。")]
        public List<QuestReward> rewards = new List<QuestReward>();

        public bool IsMainQuest { get { return category == QuestCategory.Main; } }

        public int ObjectiveCount { get { return objectives != null ? objectives.Count : 0; } }
    }

    /// <summary>任务里的**一个**目标。多个目标并行推进，全部达标才转可交付。</summary>
    [Serializable]
    public class QuestObjective
    {
        [Tooltip("稳定 id（例 reach_forest / kill_ghoul）。⭐ 存档按它记进度 —— " +
                 "改了这个字符串，玩家旧档里的进度就丢了。")]
        public string objectiveId;

        public QuestObjectiveType type;

        [Tooltip("KillCount → 敌人的 enemyId（enemy1 / enemy2 / boss_crichton）\n" +
                 "ReachScene → 场景名（Forest / Castle / Tavern）\n" +
                 "ReachPoint → 探查点的 pointId（必须与场景里 QuestPointTrigger 上填的一致）\n" +
                 "KillBoss → 留空")]
        public string targetId;

        [Min(1)]
        [Tooltip("需要多少次。KillBoss / ReachScene 填 1。")]
        public int needCount = 1;

        [Tooltip("详情面板里显示的那一行文案，例「前往覆雪林地」。")]
        public string hint;
    }

    /// <summary>一条奖励。<see cref="RewardKind.MagicCrystal"/> 时 <see cref="id"/> 留空。</summary>
    [Serializable]
    public class QuestReward
    {
        public RewardKind kind = RewardKind.MagicCrystal;

        [Tooltip("Spell / Equipment 时填对应 id；MagicCrystal 留空。")]
        public string id;

        [Min(1)]
        public int amount = 1;
    }

    /// <summary>
    /// ⚠️ 显式赋值，禁止重排（配到资产里的是整数）。
    /// </summary>
    public enum QuestObjectiveType
    {
        KillCount = 0,    // 击杀指定敌人 N 次
        KillBoss = 1,     // 击败 Boss（不区分哪个 Boss，V1 只有一个）
        ReachScene = 2,   // 到达指定场景
        ReachPoint = 3,   // 进入场景里的某个探查点范围（2026-10-22 新增，见 QuestPointTrigger）
    }

    public enum QuestCategory
    {
        Main = 0,
        Side = 1,
    }

    public enum RewardKind
    {
        MagicCrystal = 0,   // 魔晶：学法术
        Spell = 1,
        Equipment = 2,
        Gold = 3,           // 金币：买装备（2026-10-22 新增，追加在末尾，不改旧值）
    }
}
