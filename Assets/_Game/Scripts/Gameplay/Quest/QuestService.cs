using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Save;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 任务系统的**唯一逻辑入口**（跨场景常驻，自动创建）。
    ///
    /// <para>职责：订阅事件 → 推进目标 → 重算状态 → 写进 <see cref="ProgressService"/> → 广播
    /// <see cref="GameEvents.QuestsChanged"/>。**它自己绝不碰文件**，持久化全交给 ProgressService。</para>
    ///
    /// <para>⭐ **为什么必须有它**：存档层只管"存下来"，不知道"什么时候该变"；UI 只管"显示"，
    /// 不该知道规则。任务规则（前置、多目标、可交付判定、交付发奖）必须有一个唯一的地方，
    /// 否则两个系统各算一次，迟早不一致。</para>
    ///
    /// <para>⚠️ 它是 `DontDestroyOnLoad` 单例 ⇒ 事件只在 `Awake` 订阅一次、`OnDestroy` 退订。
    /// **不要**写成 `OnEnable`（切场景会重复订阅，症状是"杀一只怪进度 +2"，即 T7）。</para>
    ///
    /// <para>⚠️ 初始化顺序：`Start` 里才读 `ProgressService.Instance`。两个都是 `BeforeSceneLoad`
    /// 创建的，谁能先 Awake 没有保证 —— 放 `Awake` 里可能拿到 null。</para>
    /// </summary>
    public sealed class QuestService : MonoBehaviour
    {
        /// <summary>任务表在 Resources 下的路径（不带扩展名）。</summary>
        public const string CatalogResourcePath = "Quests/QuestCatalog";

        public static QuestService Instance { get; private set; }

        readonly List<QuestState> _states = new List<QuestState>();
        readonly Dictionary<string, QuestState> _byId = new Dictionary<string, QuestState>();
        QuestCatalog _catalog;
        bool _ready;

        /// <summary>全部任务的运行时视图（UI 用，顺序 = 任务表顺序）。</summary>
        public IReadOnlyList<QuestState> All { get { return _states; } }

        public QuestCatalog Catalog { get { return _catalog; } }

        public bool IsReady { get { return _ready; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[QuestService]");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _catalog = Resources.Load<QuestCatalog>(CatalogResourcePath);
            if (_catalog == null)
            {
                Debug.LogError($"[Quest] 加载不到任务表 Resources/{CatalogResourcePath} —— 任务系统不会工作");
                return;
            }

            // 编辑器里先跑一遍图校验，坏配置在这里就喊出来
            var errors = _catalog.Validate();
            for (int i = 0; i < errors.Count; i++) Debug.LogError("[Quest] 任务表配置错误：" + errors[i]);

            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.BossDied += OnBossDied;
            GameEvents.SceneEntered += OnSceneEntered;
            GameEvents.PointReached += OnPointReached;
        }

        void Start()
        {
            // 等 ProgressService 就绪（见类注释：两个单例的 Awake 顺序不保证）
            SyncFromSave();
        }

        void OnDestroy()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.BossDied -= OnBossDied;
            GameEvents.SceneEntered -= OnSceneEntered;
            GameEvents.PointReached -= OnPointReached;

            if (Instance == this) Instance = null;
        }

        // ───────────────────────── 初始化：存档 ↔ 配置对齐 ─────────────────────────

        void SyncFromSave()
        {
            if (_catalog == null) return;

            var progress = ProgressService.Instance != null ? ProgressService.Instance.Progress : null;
            if (progress == null)
            {
                Debug.LogError("[Quest] 没有 ProgressService，任务的接取/进度无法保存");
                return;
            }

            _states.Clear();
            _byId.Clear();

            var changed = false;   // ⚠️ 只有"真的修正了已有数据"才标脏，否则每次启动都会凭空生成存档文件

            var all = _catalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var cfg = all[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.questId)) continue;

                var save = progress.FindQuest(cfg.questId);
                if (save == null)
                {
                    // 表里有、存档没有 → 新任务，按前置算初始状态
                    save = new QuestSave { questId = cfg.questId, status = QuestStatus.Locked };
                    progress.quests.Add(save);
                }

                var state = new QuestState { QuestId = cfg.questId, Config = cfg, Status = save.status };

                // 目标按 objectiveId 对齐（配置里新增的补 0，配置里删掉的从存档剔除）
                if (cfg.objectives != null)
                {
                    for (int k = 0; k < cfg.objectives.Count; k++)
                    {
                        var def = cfg.objectives[k];
                        if (def == null || string.IsNullOrEmpty(def.objectiveId)) continue;

                        var os = FindObjectiveSave(save, def.objectiveId);
                        if (os == null)
                        {
                            os = new ObjectiveSave { objectiveId = def.objectiveId, progress = 0 };
                            save.objectives.Add(os);
                        }

                        var clamped = Mathf.Clamp(os.progress, 0, def.needCount);
                        if (clamped != os.progress) { os.progress = clamped; changed = true; }

                        state.Objectives.Add(new ObjectiveState
                        {
                            ObjectiveId = def.objectiveId,
                            Type = def.type,
                            TargetId = def.targetId,
                            Progress = clamped,
                            Need = Mathf.Max(1, def.needCount),
                            Hint = def.hint,
                        });
                    }

                    changed |= PruneObjectives(save, cfg);
                }

                // 用规则算一次：新任务该 Available 还是 Locked；老任务目标已满的该 ReadyToTurnIn
                var prereqOk = QuestRules.PrerequisitesDone(cfg, StatusOf);
                state.Status = QuestRules.Recompute(save.status, QuestRules.AllObjectivesDone(state.Objectives), prereqOk);
                if (state.Status != save.status) { save.status = state.Status; changed = true; }

                _states.Add(state);
                _byId[cfg.questId] = state;
            }

            _ready = true;
            if (changed) ProgressService.Instance.MarkDirty();   // 对齐过程中修正过数据才落盘
            Debug.Log($"[Quest] 初始化完成：{_states.Count} 条任务");
            GameEvents.RaiseQuestsChanged();
        }

        static ObjectiveSave FindObjectiveSave(QuestSave save, string objectiveId)
        {
            for (int i = 0; i < save.objectives.Count; i++)
                if (save.objectives[i] != null && save.objectives[i].objectiveId == objectiveId)
                    return save.objectives[i];
            return null;
        }

        /// <summary>把配置里已经删掉的目标从存档里剔掉（否则会一直占着且永远不推进）。返回是否真的删了东西。</summary>
        bool PruneObjectives(QuestSave save, QuestConfig cfg)
        {
            var removed = false;
            for (int i = save.objectives.Count - 1; i >= 0; i--)
            {
                var os = save.objectives[i];
                if (os == null) { save.objectives.RemoveAt(i); removed = true; continue; }

                bool stillExists = false;
                if (cfg.objectives != null)
                {
                    for (int k = 0; k < cfg.objectives.Count; k++)
                    {
                        var def = cfg.objectives[k];
                        if (def != null && def.objectiveId == os.objectiveId) { stillExists = true; break; }
                    }
                }
                if (!stillExists) { save.objectives.RemoveAt(i); removed = true; }
            }
            return removed;
        }

        QuestStatus StatusOf(string questId)
        {
            QuestState st;
            return _byId.TryGetValue(questId, out st) ? st.Status : QuestStatus.Locked;
        }

        // ───────────────────────── 事件 → 推进 ─────────────────────────

        void OnEnemyKilled(string enemyId, Vector3 position)
        {
            if (string.IsNullOrEmpty(enemyId)) return;   // 配置漏填 id 的敌人不参与任务
            Advance((obj) => obj.Type == QuestObjectiveType.KillCount && obj.TargetId == enemyId, 1, "击杀 " + enemyId);
        }

        void OnBossDied()
        {
            Advance((obj) => obj.Type == QuestObjectiveType.KillBoss, 1, "击杀 Boss");
        }

        void OnSceneEntered(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            Advance((obj) => obj.Type == QuestObjectiveType.ReachScene && obj.TargetId == sceneName, 1, "到达 " + sceneName);
        }

        void OnPointReached(string pointId)
        {
            if (string.IsNullOrEmpty(pointId)) return;
            Advance((obj) => obj.Type == QuestObjectiveType.ReachPoint && obj.TargetId == pointId, 1, "抵达 " + pointId);
        }

        /// <summary>遍历所有 `Active` 任务的每个未完成目标，命中的推进 <paramref name="amount"/>。</summary>
        void Advance(System.Func<ObjectiveState, bool> match, int amount, string reason)
        {
            if (!_ready) return;

            var dirty = false;

            for (int i = 0; i < _states.Count; i++)
            {
                var st = _states[i];
                if (st.Status != QuestStatus.Active) continue;   // 只有进行中的任务会推进

                var touched = false;
                for (int k = 0; k < st.Objectives.Count; k++)
                {
                    var obj = st.Objectives[k];
                    if (obj.IsDone) continue;
                    if (!match(obj)) continue;

                    obj.Progress = Mathf.Min(obj.Need, obj.Progress + amount);
                    touched = true;
                }
                if (!touched) continue;

                RefreshStatus(st);
                WriteBack(st);
                dirty = true;

                Debug.Log($"[Quest] {st.QuestId} 推进（{reason}）→ {st.Status}");
            }

            if (!dirty) return;

            ProgressService.Instance.MarkDirty();
            GameEvents.RaiseQuestsChanged();
        }

        // ───────────────────────── 接取 / 交付 ─────────────────────────

        /// <summary>接取。只有 `Available` 才能接（前置没完成的一律拒绝）。</summary>
        public bool Accept(string questId)
        {
            var st = Find(questId);
            if (st == null) { Debug.LogWarning($"[Quest] 接取失败：没有任务 {questId}"); return false; }

            if (st.Status != QuestStatus.Available)
            {
                Debug.LogWarning($"[Quest] 接取失败：{questId} 当前状态是 {st.Status}（只有 Available 能接）");
                return false;
            }

            if (!QuestRules.PrerequisitesDone(st.Config, StatusOf))
            {
                Debug.LogWarning($"[Quest] 接取失败：{questId} 的前置任务没完成");
                return false;
            }

            st.Status = QuestStatus.Active;
            WriteBack(st);
            ProgressService.Instance.MarkDirty();
            GameEvents.RaiseQuestsChanged();
            Debug.Log($"[Quest] 已接取 {questId}（{st.Title}）");
            return true;
        }

        /// <summary>交付。只有 `ReadyToTurnIn` 才能交（进度满了但玩家没来点，不算完成）。</summary>
        public bool TurnIn(string questId)
        {
            var st = Find(questId);
            if (st == null) { Debug.LogWarning($"[Quest] 交付失败：没有任务 {questId}"); return false; }

            if (st.Status != QuestStatus.ReadyToTurnIn)
            {
                Debug.LogWarning($"[Quest] 交付失败：{questId} 当前状态是 {st.Status}（只有 ReadyToTurnIn 能交）");
                return false;
            }

            GrantRewards(st.Config);
            st.Status = QuestStatus.Completed;
            WriteBack(st);

            // 完成后重算所有 Locked 任务：前置满足了就变 Available（任务链由此解开）
            RefreshLockedQuests();

            ProgressService.Instance.MarkDirty();
            GameEvents.RaiseQuestsChanged();
            Debug.Log($"[Quest] 已交付 {questId}（{st.Title}）");
            return true;
        }

        /// <summary>不改变状态，只按当前进度/前置重算一次（读档后、配置改动后用）。</summary>
        public void RefreshStatus(QuestState st)
        {
            var prereqOk = QuestRules.PrerequisitesDone(st.Config, StatusOf);
            st.Status = QuestRules.Recompute(st.Status, QuestRules.AllObjectivesDone(st.Objectives), prereqOk);
        }

        void RefreshLockedQuests()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var st = _states[i];
                if (st.Status != QuestStatus.Locked) continue;

                if (!QuestRules.PrerequisitesDone(st.Config, StatusOf)) continue;

                st.Status = QuestStatus.Available;
                WriteBack(st);
                Debug.Log($"[Quest] {st.QuestId} 解锁（{st.Title}）");
            }
        }

        void GrantRewards(QuestConfig cfg)
        {
            var progress = ProgressService.Instance != null ? ProgressService.Instance.Progress : null;
            if (progress == null || cfg.rewards == null) return;

            for (int i = 0; i < cfg.rewards.Count; i++)
            {
                var r = cfg.rewards[i];
                if (r == null || r.amount <= 0) continue;

                switch (r.kind)
                {
                    case RewardKind.MagicCrystal:
                        progress.magicCrystal += r.amount;
                        break;

                    case RewardKind.Gold:
                        progress.gold += r.amount;
                        break;

                    case RewardKind.Spell:
                        if (!string.IsNullOrEmpty(r.id) && !progress.learnedSpellIds.Contains(r.id))
                            progress.learnedSpellIds.Add(r.id);
                        break;

                    case RewardKind.Equipment:
                        if (string.IsNullOrEmpty(r.id)) break;
                        var eq = FindEquipment(progress, r.id);
                        if (eq == null)
                        {
                            eq = new EquipmentSave { equipmentId = r.id, level = 0 };
                            progress.equipmentLevels.Add(eq);
                        }
                        eq.level += r.amount;
                        break;
                }

                Debug.Log($"[Quest] 奖励 {cfg.questId}：{r.kind} +{r.amount} {(string.IsNullOrEmpty(r.id) ? "" : r.id)}");
            }
        }

        static EquipmentSave FindEquipment(PlayerProgress progress, string equipmentId)
        {
            for (int i = 0; i < progress.equipmentLevels.Count; i++)
                if (progress.equipmentLevels[i] != null && progress.equipmentLevels[i].equipmentId == equipmentId)
                    return progress.equipmentLevels[i];
            return null;
        }

        // ───────────────────────── 查询 / 写回 ─────────────────────────

        public QuestState Find(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            QuestState st;
            return _byId.TryGetValue(questId, out st) ? st : null;
        }

        /// <summary>把运行时状态写回 <c>PlayerProgress.quests</c>（不落盘，落盘由 MarkDirty 触发）。</summary>
        void WriteBack(QuestState st)
        {
            var progress = ProgressService.Instance != null ? ProgressService.Instance.Progress : null;
            if (progress == null) return;

            var save = progress.FindQuest(st.QuestId);
            if (save == null)
            {
                save = new QuestSave { questId = st.QuestId };
                progress.quests.Add(save);
            }

            save.status = st.Status;

            for (int i = 0; i < st.Objectives.Count; i++)
            {
                var obj = st.Objectives[i];
                var os = FindObjectiveSave(save, obj.ObjectiveId);
                if (os == null)
                {
                    os = new ObjectiveSave { objectiveId = obj.ObjectiveId };
                    save.objectives.Add(os);
                }
                os.progress = obj.Progress;
            }
        }
    }
}
