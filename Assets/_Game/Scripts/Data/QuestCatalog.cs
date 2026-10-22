using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 任务表：全局唯一的一份「本项目有哪些任务」。
    ///
    /// <para>⚠️ **必须启动时校验**（<see cref="Validate"/>）：前置任务写错、成环、引用了不存在的 id，
    /// 表现都是"某条任务永远锁着且没有任何提示" —— 与 bug 无法区分。编辑器菜单
    /// `Tools/任务/校验任务表` 可以提前跑一遍。</para>
    ///
    /// <para>⚠️ 任务被删掉后，存档里对应的 <c>QuestSave</c> **保留不动**（删了就找不回来）。
    /// 本表只负责"读出来"，不负责清理存档。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "QuestCatalog", menuName = "Game/Quest/QuestCatalog")]
    public class QuestCatalog : ScriptableObject
    {
        [Tooltip("本项目全部任务。顺序无所谓（列表 UI 会自己排序）。")]
        [SerializeField] private List<QuestConfig> _quests = new List<QuestConfig>();

        [Tooltip("可选：用来校验 targetScene 是否为已登记的 sceneName。留空则跳过该项检查。")]
        [SerializeField] private SceneCatalog _sceneCatalog;

        public IReadOnlyList<QuestConfig> All { get { return _quests; } }

        public SceneCatalog SceneCatalog { get { return _sceneCatalog; } }

        /// <summary>按 questId 查（找不到返回 null）。</summary>
        public QuestConfig Find(string questId)
        {
            if (string.IsNullOrEmpty(questId) || _quests == null) return null;

            for (int i = 0; i < _quests.Count; i++)
            {
                var q = _quests[i];
                if (q != null && q.questId == questId) return q;
            }
            return null;
        }

        /// <summary>
        /// 校验任务表。返回错误列表，**空 = 通过**。
        /// 只检查"一定会在运行时出问题"的项，不管数值是否合理（那是策划的事）。
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (_quests == null || _quests.Count == 0)
            {
                errors.Add("任务表是空的");
                return errors;
            }

            var byId = new Dictionary<string, QuestConfig>();

            // ── 第 1 遍：id 唯一性 + 目标自检 ──
            for (int i = 0; i < _quests.Count; i++)
            {
                var q = _quests[i];
                if (q == null) { errors.Add($"[{i}] 列表里有一个 None（空引用）"); continue; }
                if (string.IsNullOrWhiteSpace(q.questId)) { errors.Add($"{q.name}：questId 为空"); continue; }
                if (byId.ContainsKey(q.questId)) { errors.Add($"questId 重复：{q.questId}"); continue; }

                byId[q.questId] = q;

                if (string.IsNullOrWhiteSpace(q.title))
                    errors.Add($"{q.questId}：title 为空");

                if (q.objectives == null || q.objectives.Count == 0)
                {
                    errors.Add($"{q.questId}：没有任何目标（至少要 1 个）");
                    continue;
                }

                var objIds = new HashSet<string>();
                for (int k = 0; k < q.objectives.Count; k++)
                {
                    var o = q.objectives[k];
                    if (o == null) { errors.Add($"{q.questId}：第 {k} 个目标是 None"); continue; }
                    if (string.IsNullOrWhiteSpace(o.objectiveId)) errors.Add($"{q.questId}：第 {k} 个目标没有 objectiveId");
                    else if (!objIds.Add(o.objectiveId)) errors.Add($"{q.questId}：objectiveId 重复（{o.objectiveId}）");

                    if (o.needCount < 1) errors.Add($"{q.questId}/{o.objectiveId}：needCount = {o.needCount}（至少 1）");

                    switch (o.type)
                    {
                        case QuestObjectiveType.KillCount:
                            if (string.IsNullOrWhiteSpace(o.targetId))
                                errors.Add($"{q.questId}/{o.objectiveId}：KillCount 没填 enemyId");
                            break;
                        case QuestObjectiveType.ReachScene:
                            if (string.IsNullOrWhiteSpace(o.targetId))
                                errors.Add($"{q.questId}/{o.objectiveId}：ReachScene 没填 sceneName");
                            else if (_sceneCatalog != null && _sceneCatalog.Find(o.targetId) == null)
                                errors.Add($"{q.questId}/{o.objectiveId}：场景 {o.targetId} 没在 SceneCatalog 里登记");
                            break;
                    }
                }
            }

            // ── 第 2 遍：前置存在性 + 自环 ──
            foreach (var pair in byId)
            {
                var q = pair.Value;
                if (q.prerequisites == null) continue;

                for (int i = 0; i < q.prerequisites.Count; i++)
                {
                    var pre = q.prerequisites[i];
                    if (string.IsNullOrWhiteSpace(pre)) { errors.Add($"{q.questId}：前置列表里有空项（第 {i} 个）"); continue; }
                    if (pre == q.questId) { errors.Add($"{q.questId}：前置任务写成了自己"); continue; }
                    if (!byId.ContainsKey(pre)) errors.Add($"{q.questId}：前置任务 {pre} 不存在");
                }
            }

            // ── 第 3 遍：环检测（0 未访问 / 1 栈中 / 2 已完成）──
            var state = new Dictionary<string, int>();
            var stack = new List<string>();
            foreach (var pair in byId)
            {
                if (GetState(state, pair.Key) == 0)
                    DetectCycle(pair.Key, byId, state, stack, errors);
            }

            // ── 第 4 遍：targetScene 是否登记 ──
            if (_sceneCatalog != null)
            {
                foreach (var pair in byId)
                {
                    var scene = pair.Value.targetScene;
                    if (!string.IsNullOrWhiteSpace(scene) && _sceneCatalog.Find(scene) == null)
                        errors.Add($"{pair.Key}：taskScene {scene} 没在 SceneCatalog 里登记");
                }
            }

            return errors;
        }

        static int GetState(Dictionary<string, int> state, string id)
        {
            int v;
            return state.TryGetValue(id, out v) ? v : 0;
        }

        static void DetectCycle(string id, Dictionary<string, QuestConfig> byId,
                                Dictionary<string, int> state, List<string> stack, List<string> errors)
        {
            state[id] = 1;
            stack.Add(id);

            var q = byId[id];
            if (q.prerequisites != null)
            {
                for (int i = 0; i < q.prerequisites.Count; i++)
                {
                    var pre = q.prerequisites[i];
                    if (!byId.ContainsKey(pre)) continue;   // 上一遍已报

                    var s = GetState(state, pre);
                    if (s == 1)
                    {
                        var start = stack.IndexOf(pre);
                        var cycle = start >= 0
                            ? string.Join(" → ", stack.GetRange(start, stack.Count - start).ToArray()) + " → " + pre
                            : pre + " → " + id;
                        errors.Add($"前置任务是环：{cycle}");
                    }
                    else if (s == 0)
                    {
                        DetectCycle(pre, byId, state, stack, errors);
                    }
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
        }
    }
}
