using System;
using System.Collections.Generic;

namespace Game.Save
{
    /// <summary>
    /// 存档根对象。⚠️ 字段名就是磁盘上的 JSON 键名 —— 改名等于丢数据，必须配 version 升级 + 迁移。
    /// </summary>
    [Serializable]
    public class SaveFile
    {
        public int version;
        public SaveHeader header = new SaveHeader();
        public PlayerProgress progress = new PlayerProgress();
    }

    /// <summary>
    /// 存档头。选档界面只读这一块，不需要把整份进度反序列化。
    /// </summary>
    [Serializable]
    public class SaveHeader
    {
        public string savedAtIso;      // ISO 8601，本地时间
        public string sceneName;       // 保存时所在场景
        public float playTimeSec;      // 累计游玩时长（秒）
    }

    /// <summary>
    /// 全部持久化进度。⚠️ 只放"跨会话要保留"的数据；
    /// 血量 / 魔力 / 是否拔刀 / 玩家坐标属于"存档点状态"，不在这里。
    /// </summary>
    [Serializable]
    public class PlayerProgress
    {
        public int magicCrystal;                                            // 魔晶
        public List<EquipmentSave> equipmentLevels = new List<EquipmentSave>();
        public List<string> learnedSpellIds = new List<string>();
        public List<TrainingSave> trainingCounts = new List<TrainingSave>();
        public List<QuestSave> quests = new List<QuestSave>();
        public StatsSave stats = new StatsSave();

        public QuestSave FindQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            for (int i = 0; i < quests.Count; i++)
                if (quests[i] != null && quests[i].questId == questId) return quests[i];
            return null;
        }

        public QuestSave GetOrAddQuest(string questId)
        {
            var found = FindQuest(questId);
            if (found != null) return found;
            var q = new QuestSave { questId = questId, status = QuestStatus.Locked };
            quests.Add(q);
            return q;
        }
    }

    [Serializable]
    public class QuestSave
    {
        public string questId;
        public QuestStatus status;

        // 一条任务 = 1~N 个目标，全部完成才可交付。
        // ⚠️ 按 objectiveId 与配置对齐，**不要用下标** —— 往配置中间插一个目标会让旧档进度错位。
        public List<ObjectiveSave> objectives = new List<ObjectiveSave>();
    }

    [Serializable]
    public class ObjectiveSave
    {
        public string objectiveId;
        public int progress;
    }

    [Serializable]
    public class EquipmentSave
    {
        public string equipmentId;
        public int level;
    }

    [Serializable]
    public class TrainingSave
    {
        public string statId;
        public int count;
    }

    [Serializable]
    public class StatsSave
    {
        public int clears;
        public int deaths;
        public float bestTime;
    }

    /// <summary>
    /// ⚠️ 显式赋值，禁止重排 —— JsonUtility 存的是整数，插一项会让所有旧档状态错位一格，且不报错。
    /// </summary>
    public enum QuestStatus
    {
        Locked        = 0,   // 未解锁（前置未完成）
        Available     = 1,   // 可接取
        Active        = 2,   // 进行中
        ReadyToTurnIn = 3,   // 进度已满，回酒馆可交付
        Completed     = 4,   // 已交付
    }
}
