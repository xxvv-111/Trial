using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Save
{
    public enum LoadStatus
    {
        Ok = 0,             // 读取成功
        NotFound = 1,       // 没有存档（新玩家）
        Corrupted = 2,      // 坏档（已尝试 .bak，也失败）
        VersionTooNew = 3,  // 存档版本高于当前客户端，拒绝加载
    }

    /// <summary>
    /// 纯逻辑的存读盘层，不挂 MonoBehaviour。
    /// 只依赖 IFileStore，因此可以在编辑器里用内存后端毫秒级自测。
    ///
    /// 文件格式（两个部分，用换行分隔）：
    ///   { ... JSON ... }
    ///   #crc32:9A3F21B7
    /// 校验行缺失时视为"手改档"放行（打 warning）；存在但不匹配则判坏档。
    /// </summary>
    public sealed class SaveSystem
    {
        public const int CurrentVersion = 1;
        public const string DefaultFileName = "save.json";
        public const string BackupSuffix = ".bak";
        public const string BadSuffix = ".bad.json";
        public const string CrcPrefix = "#crc32:";

        readonly IFileStore _store;
        readonly string _path;

        public SaveSystem(IFileStore store, string path)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path 为空", nameof(path));
            _store = store;
            _path = path;
        }

        public string Path => _path;
        public IFileStore Store => _store;

        public static SaveFile CreateDefault() => new SaveFile
        {
            version = CurrentVersion,
            header = new SaveHeader(),
            progress = new PlayerProgress(),
        };

        // ───────────────────────────── 读 ─────────────────────────────

        public LoadStatus TryLoad(out SaveFile file) => TryLoad(out file, out _);

        /// <summary>
        /// 降级链：主档 → .bak → 默认值。任何情况下都不抛异常。
        /// </summary>
        public LoadStatus TryLoad(out SaveFile file, out string message)
        {
            file = null;
            message = null;

            if (!_store.Exists(_path))
            {
                var bakPath = _path + BackupSuffix;
                if (_store.Exists(bakPath))
                {
                    var bakStatus = Parse(SafeRead(bakPath), out var fromBak, out var bakMsg);
                    if (bakStatus == LoadStatus.Ok)
                    {
                        file = fromBak;
                        message = "主档缺失，已从 .bak 恢复。" + bakMsg;
                        Debug.LogWarning("[Save] " + message);
                        return LoadStatus.Ok;
                    }
                }

                file = CreateDefault();
                message = "无存档，使用默认进度";
                return LoadStatus.NotFound;
            }

            var status = Parse(SafeRead(_path), out file, out message);

            if (status == LoadStatus.Corrupted)
            {
                // 坏档留证据：改名成 save.bad.json，绝不原地覆盖（保留了人工抢救的可能）
                try { _store.Move(_path, _path + BadSuffix, true); }
                catch (Exception e) { Debug.LogWarning("[Save] 备份坏档失败：" + e.Message); }

                var bakPath = _path + BackupSuffix;
                if (_store.Exists(bakPath))
                {
                    var bakStatus = Parse(SafeRead(bakPath), out var fromBak, out var bakMsg);
                    if (bakStatus == LoadStatus.Ok)
                    {
                        file = fromBak;
                        message = "主档损坏，已从 .bak 恢复。" + bakMsg;
                        Debug.LogWarning("[Save] " + message);
                        return LoadStatus.Ok;
                    }
                }

                file = CreateDefault();
                Debug.LogError("[Save] 坏档且无可用备份，回退默认进度：" + message);
                return LoadStatus.Corrupted;
            }

            if (status == LoadStatus.VersionTooNew)
            {
                // ⚠️ 不要备份、不要覆盖 —— 那是"未来的档"，覆盖了就真没了
                Debug.LogError("[Save] " + message);
                file = CreateDefault();
                return status;
            }

            return status;
        }

        string SafeRead(string path)
        {
            try { return _store.ReadAllText(path); }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] 读取失败：" + e.Message);
                return null;
            }
        }

        LoadStatus Parse(string raw, out SaveFile file, out string message)
        {
            file = null;
            message = null;

            if (raw == null) { message = "内容为 null（读取失败）"; return LoadStatus.Corrupted; }

            raw = raw.TrimStart('\uFEFF');                 // BOM：JsonUtility 会直接抛异常
            if (raw.Trim().Length == 0) { message = "文件为空"; return LoadStatus.Corrupted; }

            var body = raw;
            var lastNewline = raw.LastIndexOf('\n');
            if (lastNewline >= 0)
            {
                var lastLine = raw.Substring(lastNewline + 1).Trim();
                if (lastLine.StartsWith(CrcPrefix, StringComparison.Ordinal))
                {
                    body = raw.Substring(0, lastNewline);
                    var hex = lastLine.Substring(CrcPrefix.Length).Trim();
                    if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var expected))
                    {
                        message = "校验行无法解析：" + lastLine;
                        return LoadStatus.Corrupted;
                    }

                    if (Crc32.Compute(body) != expected)
                    {
                        // Windows 记事本会把手改档写成 CRLF，末尾多一个 \r，容忍一次
                        var alt = body.TrimEnd('\r');
                        if (Crc32.Compute(alt) != expected)
                        {
                            message = "CRC 校验失败（文件被截断或改动）";
                            return LoadStatus.Corrupted;
                        }
                        body = alt;
                    }
                }
                else
                {
                    message = "无校验行（手改档），已跳过校验";
                }
            }

            if (string.IsNullOrWhiteSpace(body)) { message = "正文为空"; return LoadStatus.Corrupted; }

            try { file = JsonUtility.FromJson<SaveFile>(body); }
            catch (Exception e) { message = "JSON 解析异常：" + e.Message; return LoadStatus.Corrupted; }

            if (file == null) { message = "JSON 解析结果为 null"; return LoadStatus.Corrupted; }

            if (file.version > CurrentVersion)
            {
                message = $"存档版本 {file.version} 高于客户端版本 {CurrentVersion}，拒绝加载";
                file = null;
                return LoadStatus.VersionTooNew;
            }

            if (file.progress == null) { message = "缺少 progress 节点"; return LoadStatus.Corrupted; }

            if (file.version <= 0)
            {
                file.version = 1;   // 无 version 字段的旧档
                message = "旧档缺少版本号，按 v1 处理";
            }

            if (file.version < CurrentVersion) Migrate(file, file.version);

            Normalize(file);
            return LoadStatus.Ok;
        }

        // ───────────────────────────── 写 ─────────────────────────────

        /// <summary>写盘。失败只打 error 并返回 false，绝不抛异常给调用方。</summary>
        public bool Save(SaveFile file, string sceneName = null, float playTimeSec = -1f)
        {
            if (file == null) { Debug.LogError("[Save] Save 传入 null"); return false; }

            file.version = CurrentVersion;
            if (file.header == null) file.header = new SaveHeader();
            file.header.savedAtIso = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(sceneName)) file.header.sceneName = sceneName;
            if (playTimeSec >= 0f) file.header.playTimeSec = playTimeSec;

            Normalize(file);

            var body = JsonUtility.ToJson(file, true);
            var text = body + "\n" + CrcPrefix + Crc32.Compute(body).ToString("X8");

            try
            {
                _store.WriteAllTextAtomic(_path, text);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Save] 写盘失败：" + e.Message);
                return false;
            }
        }

        /// <summary>删除主档与备份（调试用）。</summary>
        public void DeleteAll()
        {
            SafeDelete(_path);
            SafeDelete(_path + BackupSuffix);
            SafeDelete(_path + BadSuffix);
        }

        void SafeDelete(string path)
        {
            try { _store.Delete(path); }
            catch (Exception e) { Debug.LogWarning("[Save] 删除失败 " + path + "：" + e.Message); }
        }

        // ───────────────────────────── 迁移 ─────────────────────────────

        void Migrate(SaveFile file, int fromVersion)
        {
            for (var v = fromVersion; v < CurrentVersion; v++)
            {
                switch (v)
                {
                    // case 1: Migrate_1_to_2(file); break;
                    default: break;
                }
            }
            file.version = CurrentVersion;
        }

        // ───────────────────────────── 清洗 ─────────────────────────────

        /// <summary>
        /// 反序列化后的统一清洗：补 null 容器、剔除 null 元素、去重、枚举越界归零。
        /// 这些情况 JsonUtility 全都不会报错，只会悄悄给你 null / 0。
        /// </summary>
        public static void Normalize(SaveFile file)
        {
            if (file == null) return;
            if (file.header == null) file.header = new SaveHeader();
            if (file.progress == null) file.progress = new PlayerProgress();
            if (file.version < CurrentVersion) file.version = CurrentVersion;

            var p = file.progress;

            if (p.equipmentLevels == null) p.equipmentLevels = new List<EquipmentSave>();
            if (p.learnedSpellIds == null) p.learnedSpellIds = new List<string>();
            if (p.trainingCounts == null) p.trainingCounts = new List<TrainingSave>();
            if (p.quests == null) p.quests = new List<QuestSave>();
            if (p.stats == null) p.stats = new StatsSave();

            p.equipmentLevels.RemoveAll(e => e == null || string.IsNullOrEmpty(e.equipmentId));
            p.trainingCounts.RemoveAll(t => t == null || string.IsNullOrEmpty(t.statId));
            p.quests.RemoveAll(q => q == null || string.IsNullOrEmpty(q.questId));
            p.learnedSpellIds.RemoveAll(string.IsNullOrEmpty);

            Dedupe(p.equipmentLevels, e => e.equipmentId);
            Dedupe(p.trainingCounts, t => t.statId);
            Dedupe(p.quests, q => q.questId);
            DedupeStrings(p.learnedSpellIds);

            for (var i = 0; i < p.quests.Count; i++)
            {
                var q = p.quests[i];
                if (q.objectives == null) q.objectives = new List<ObjectiveSave>();
                q.objectives.RemoveAll(o => o == null || string.IsNullOrEmpty(o.objectiveId));
                Dedupe(q.objectives, o => o.objectiveId);

                for (var k = 0; k < q.objectives.Count; k++)
                    if (q.objectives[k].progress < 0) q.objectives[k].progress = 0;   // 手改档可能写出负数

                var s = (int)q.status;
                if (s < 0 || s > (int)QuestStatus.Completed)
                {
                    Debug.LogWarning($"[Save] 任务 {q.questId} 的状态值 {s} 越界，已归零为 Locked");
                    q.status = QuestStatus.Locked;
                }
            }
        }

        static void Dedupe<T>(List<T> list, Func<T, string> key)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = list.Count - 1; i >= 0; i--)
                if (!seen.Add(key(list[i]))) list.RemoveAt(i);
        }

        static void DedupeStrings(List<string> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = list.Count - 1; i >= 0; i--)
                if (!seen.Add(list[i])) list.RemoveAt(i);
        }

        // ───────────────────────────── CRC32 ─────────────────────────────

        /// <summary>标准 CRC-32（IEEE 802.3，多项式 0xEDB88320），自带查表。</summary>
        public static class Crc32
        {
            static readonly uint[] Table = BuildTable();

            static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    var c = i;
                    for (var k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[i] = c;
                }
                return table;
            }

            public static uint Compute(string text)
            {
                if (string.IsNullOrEmpty(text)) return 0u;
                var crc = 0xFFFFFFFFu;
                for (var i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    // 逐字符按 UTF-8 字节喂进去，保证跨平台一致
                    if (ch < 0x80)
                    {
                        crc = (crc >> 8) ^ Table[(crc ^ ch) & 0xFF];
                    }
                    else if (ch < 0x800)
                    {
                        crc = (crc >> 8) ^ Table[(crc ^ (0xC0 | (ch >> 6))) & 0xFF];
                        crc = (crc >> 8) ^ Table[(crc ^ (0x80 | (ch & 0x3F))) & 0xFF];
                    }
                    else
                    {
                        crc = (crc >> 8) ^ Table[(crc ^ (0xE0 | (ch >> 12))) & 0xFF];
                        crc = (crc >> 8) ^ Table[(crc ^ (0x80 | ((ch >> 6) & 0x3F))) & 0xFF];
                        crc = (crc >> 8) ^ Table[(crc ^ (0x80 | (ch & 0x3F))) & 0xFF];
                    }
                }
                return crc ^ 0xFFFFFFFFu;
            }
        }
    }
}
