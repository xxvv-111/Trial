using System;
using System.Collections.Generic;
using System.IO;
using Game.Save;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 存档系统自测（30 项）。全部走 MemoryFileStore / 临时目录，不碰玩家的真实存档。
/// 用法：菜单 Tools > 存档 > 自测（30 项）。红了的项会在 Console 里逐条列出。
/// </summary>
public static class SaveSelfTest
{
    const string TestPath = "mem:/save.json";
    const string TempFolderName = "CurriculumDesign-SaveSelfTest";

    static int _pass;
    static int _fail;
    static readonly List<string> _failures = new List<string>();

    [MenuItem("Tools/存档/自测（30 项）")]
    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        _failures.Clear();

        var watch = System.Diagnostics.Stopwatch.StartNew();

        GroupRoundTrip();    // A 6
        GroupDefault();      // B 3
        GroupCorrupted();    // C 7
        GroupVersions();     // D 4
        GroupStructure();    // E 5
        GroupAtomicWrite();  // F 5

        watch.Stop();

        if (_fail == 0)
        {
            Debug.Log($"<color=green>[SaveSelfTest] {_pass}/{_pass} 全部通过，用时 {watch.ElapsedMilliseconds} ms</color>");
            return;
        }

        Debug.LogError($"[SaveSelfTest] {_pass} 通过 / {_fail} 失败，用时 {watch.ElapsedMilliseconds} ms");
        for (var i = 0; i < _failures.Count; i++) Debug.LogError("  ✗ " + _failures[i]);
    }

    // ═══════════════════════ A 往返（6） ═══════════════════════

    static void GroupRoundTrip()
    {
        var sys = NewMemorySystem();
        var src = SaveSystem.CreateDefault();
        src.progress.magicCrystal = 1234;
        src.progress.learnedSpellIds.Add("spell_fireball");
        src.progress.equipmentLevels.Add(new EquipmentSave { equipmentId = "sword_01", level = 3 });
        src.progress.trainingCounts.Add(new TrainingSave { statId = "str", count = 7 });
        src.progress.quests.Add(new QuestSave { questId = "q_001", status = QuestStatus.Active, progress = 2 });
        src.progress.stats.clears = 4;
        src.progress.stats.deaths = 9;
        src.progress.stats.bestTime = 62.5f;

        var writeOk = sys.Save(src);

        var status = sys.TryLoad(out var back, out var msg);
        var problems = new List<string>();
        if (!writeOk) problems.Add("写入失败");
        if (status != LoadStatus.Ok) problems.Add("状态=" + status + " " + msg);
        if (back == null || back.progress == null) problems.Add("读回为空");
        else
        {
            if (back.progress.magicCrystal != 1234) problems.Add("魔晶");
            if (back.progress.learnedSpellIds.Count != 1 || back.progress.learnedSpellIds[0] != "spell_fireball") problems.Add("法术");
            if (back.progress.equipmentLevels.Count != 1 || back.progress.equipmentLevels[0].level != 3) problems.Add("装备等级");
            if (back.progress.trainingCounts.Count != 1 || back.progress.trainingCounts[0].count != 7) problems.Add("锻炼次数");
            if (back.progress.quests.Count != 1 || back.progress.quests[0].progress != 2 || back.progress.quests[0].status != QuestStatus.Active) problems.Add("任务");
            if (back.progress.stats.clears != 4 || back.progress.stats.deaths != 9 || Math.Abs(back.progress.stats.bestTime - 62.5f) > 0.001f) problems.Add("统计");
        }
        Check("A1 满数据往返字段一致", problems.Count == 0, string.Join(" / ", problems));

        // A2 空 List 往返：不能变成 null
        var empty = SaveSystem.CreateDefault();
        var sys2 = NewMemorySystem();
        sys2.Save(empty);
        sys2.TryLoad(out var e2, out _);
        Check("A2 空 List 往返后仍非 null",
            e2.progress.quests != null && e2.progress.quests.Count == 0 &&
            e2.progress.equipmentLevels != null && e2.progress.learnedSpellIds != null);

        // A3 null List：写入前就被 Normalize 补齐
        var nullLists = SaveSystem.CreateDefault();
        nullLists.progress.quests = null;
        nullLists.progress.learnedSpellIds = null;
        var sys3 = NewMemorySystem();
        sys3.Save(nullLists);
        sys3.TryLoad(out var e3, out _);
        Check("A3 null List 被补齐", e3.progress.quests != null && e3.progress.learnedSpellIds != null);

        // A4 中文往返
        var cn = SaveSystem.CreateDefault();
        cn.progress.quests.Add(new QuestSave { questId = "任务_第一章_铁匠的请求", status = QuestStatus.ReadyToTurnIn });
        var sys4 = NewMemorySystem();
        sys4.Save(cn);
        sys4.TryLoad(out var e4, out _);
        Check("A4 中文任务名往返", e4.progress.quests.Count == 1 && e4.progress.quests[0].questId == "任务_第一章_铁匠的请求");

        // A5 超长字符串（2000 字符）
        var longText = new string('长', 2000);
        var lng = SaveSystem.CreateDefault();
        lng.progress.quests.Add(new QuestSave { questId = longText });
        var sys5 = NewMemorySystem();
        sys5.Save(lng);
        sys5.TryLoad(out var e5, out _);
        Check("A5 2000 字符往返", e5.progress.quests.Count == 1 && e5.progress.quests[0].questId.Length == 2000);

        // A6 极值
        var ext = SaveSystem.CreateDefault();
        ext.progress.magicCrystal = int.MaxValue;
        ext.progress.stats.deaths = -1;
        var sys6 = NewMemorySystem();
        sys6.Save(ext);
        sys6.TryLoad(out var e6, out _);
        Check("A6 int.MaxValue 与负数往返", e6.progress.magicCrystal == int.MaxValue && e6.progress.stats.deaths == -1);
    }

    // ═══════════════════════ B 默认值（3） ═══════════════════════

    static void GroupDefault()
    {
        var store = new MemoryFileStore();
        var sys = new SaveSystem(store, TestPath);

        var status = sys.TryLoad(out var file, out _);
        Check("B1 无文件返回 NotFound", status == LoadStatus.NotFound, "实际=" + status);
        Check("B2 无文件返回默认值", file != null && file.progress != null && file.progress.magicCrystal == 0 && file.progress.quests != null);
        Check("B3 无文件不凭空写盘", !store.Exists(TestPath) && store.WriteCount == 0);
    }

    // ═══════════════════════ C 坏档（7） ═══════════════════════

    static void GroupCorrupted()
    {
        var valid = MakeValidText();

        CheckCorrupted("C1 空文件", string.Empty);
        // C2 合法 JSON 但被改过一个字符 —— 只有 CRC 能抓到（JSON 本身仍然能解析）
        var tamperIndex = valid.IndexOf("\"magicCrystal\"", StringComparison.Ordinal);
        CheckCorrupted("C2 正文被改动触发 CRC 校验失败", valid.Insert(tamperIndex, " "));
        CheckCorrupted("C3 截断到 60%", valid.Substring(0, valid.Length * 6 / 10));
        CheckCorrupted("C6 只有空白", " \r\n\t ");

        // ⚠️ 实测结论：JsonUtility 不会把嵌套对象反序列化成 null，
        // 所以 {"progress":null} 和 {} 都会被解析成"空进度"，不是坏档。
        var nullProgressStore = new MemoryFileStore();
        nullProgressStore.SetRaw(TestPath, "{\"version\":1,\"progress\":null}");
        var s4 = new SaveSystem(nullProgressStore, TestPath).TryLoad(out var f4, out _);
        Check("C4 progress=null 按空进度处理（不是坏档）",
            s4 == LoadStatus.Ok && f4.progress != null && f4.progress.quests != null, "实际=" + s4);

        var emptyObjStore = new MemoryFileStore();
        emptyObjStore.SetRaw(TestPath, "{}");
        var s5 = new SaveSystem(emptyObjStore, TestPath).TryLoad(out var f5, out _);
        Check("C5 空对象 {} 按空进度处理",
            s5 == LoadStatus.Ok && f5.version == SaveSystem.CurrentVersion && f5.progress.magicCrystal == 0, "实际=" + s5);

        // C7 BOM 开头的合法档必须能读（JsonUtility 遇到 BOM 会抛 ArgumentException）
        var store = new MemoryFileStore();
        store.SetRaw(TestPath, "\uFEFF" + valid);
        var sys = new SaveSystem(store, TestPath);
        var status = sys.TryLoad(out var file, out var msg);
        Check("C7 带 BOM 的合法档可读", status == LoadStatus.Ok && file != null, status + " " + msg);
    }

    static void CheckCorrupted(string name, string raw)
    {
        var store = new MemoryFileStore();
        store.SetRaw(TestPath, raw);
        var sys = new SaveSystem(store, TestPath);
        LoadStatus status;
        try
        {
            status = sys.TryLoad(out var file, out var msg);
            var ok = status == LoadStatus.Corrupted && file != null && file.progress != null;
            Check(name, ok, $"状态={status} 兜底={(file?.progress != null)} {msg}");
        }
        catch (Exception e)
        {
            Check(name, false, "抛异常：" + e.GetType().Name);
        }
    }

    // ═══════════════════════ D 版本（4） ═══════════════════════

    static void GroupVersions()
    {
        LoadStatus status;

        status = Load("{\"version\":999,\"progress\":{}}", out var tooNew);
        Check("D1 未来版本被拒绝", status == LoadStatus.VersionTooNew && tooNew.progress.magicCrystal == 0, "实际=" + status);

        status = Load("{\"version\":0,\"progress\":{\"magicCrystal\":5}}", out var zero);
        Check("D2 version=0 升到 1 并读取", status == LoadStatus.Ok && zero.version == SaveSystem.CurrentVersion && zero.progress.magicCrystal == 5, "实际=" + status);

        status = Load("{\"progress\":{\"magicCrystal\":7}}", out var noVer);
        Check("D3 缺 version 字段按 v1 处理", status == LoadStatus.Ok && noVer.version == SaveSystem.CurrentVersion && noVer.progress.magicCrystal == 7, "实际=" + status);

        status = Load("{\"version\":-3,\"progress\":{}}", out var negative);
        Check("D4 负版本号被钳到 1", status == LoadStatus.Ok && negative.version == SaveSystem.CurrentVersion, "实际=" + status);
    }

    static LoadStatus Load(string json, out SaveFile file)
    {
        var store = new MemoryFileStore();
        store.SetRaw(TestPath, json);
        return new SaveSystem(store, TestPath).TryLoad(out file, out _);
    }

    // ═══════════════════════ E 结构容错（5） ═══════════════════════

    static void GroupStructure()
    {
        LoadStatus status;

        status = Load("{\"version\":1,\"progress\":{\"quests\":[]}}", out var missingField);
        Check("E1 缺字段用默认值", status == LoadStatus.Ok && missingField.progress.magicCrystal == 0 && missingField.progress.stats != null, "实际=" + status);

        status = Load("{\"version\":1,\"progress\":{\"magicCrystal\":5,\"futureField\":\"x\"}}", out var extra);
        Check("E2 多余未知字段可忽略", status == LoadStatus.Ok && extra.progress.magicCrystal == 5, "实际=" + status);

        status = Load("{\"version\":1,\"progress\":{\"quests\":[{\"questId\":\"q\",\"status\":99,\"progress\":1}]}}", out var badEnum);
        Check("E3 枚举越界归零 Locked",
            status == LoadStatus.Ok && badEnum.progress.quests.Count == 1 && badEnum.progress.quests[0].status == QuestStatus.Locked,
            status + " 实际=" + (badEnum.progress.quests.Count > 0 ? ((int)badEnum.progress.quests[0].status).ToString() : "无元素"));

        status = Load("{\"version\":1,\"progress\":{\"quests\":[null,{\"questId\":\"a\",\"status\":2}]}}", out var nullElem);
        Check("E4 null 元素被剔除", status == LoadStatus.Ok && nullElem.progress.quests.Count == 1, status + " 数量=" + nullElem.progress.quests.Count);

        status = Load("{\"version\":1,\"progress\":{\"quests\":[{\"questId\":\"dup\",\"progress\":1},{\"questId\":\"dup\",\"progress\":2}]}}", out var dup);
        Check("E5 重复 questId 去重", status == LoadStatus.Ok && dup.progress.quests.Count == 1 && dup.progress.quests[0].progress == 2, status + " 数量=" + dup.progress.quests.Count);
    }

    // ═══════════════════════ F 原子写（5） ═══════════════════════

    static void GroupAtomicWrite()
    {
        var dir = FreshTempDir();
        var path = Path.Combine(dir, "save.json");
        var sys = new SaveSystem(new DiskFileStore(), path);

        var first = SaveSystem.CreateDefault();
        first.progress.magicCrystal = 1;
        var ok = sys.Save(first);
        Check("F1 正常写生成文件", ok && File.Exists(path), "ok=" + ok);

        var oldText = File.ReadAllText(path);
        var second = SaveSystem.CreateDefault();
        second.progress.magicCrystal = 2;
        sys.Save(second);
        Check("F2 覆盖写生成 .bak 且内容为旧档",
            File.Exists(path + ".bak") && File.ReadAllText(path + ".bak") == oldText);

        Check("F3 无 .tmp 残留", !File.Exists(path + ".tmp"));

        // F4 注入"写到一半断电" —— 主档必须一字不变
        var mem = new MemoryFileStore();
        var memSys = new SaveSystem(mem, TestPath);
        var a = SaveSystem.CreateDefault();
        a.progress.magicCrystal = 11;
        memSys.Save(a);
        var before = mem.GetRaw(TestPath);

        mem.FailAfterBytes = 20;
        var big = SaveSystem.CreateDefault();
        big.progress.learnedSpellIds.Add(new string('x', 500));
        var writeOk = memSys.Save(big);
        Check("F4 写入中断后主档不变",
            !writeOk && mem.GetRaw(TestPath) == before, "ok=" + writeOk);

        // F5 磁盘满 / 无权限
        var failStore = new MemoryFileStore { AlwaysFail = true };
        var failSys = new SaveSystem(failStore, TestPath);
        var noThrow = false;
        bool falseResult;
        try { falseResult = failSys.Save(SaveSystem.CreateDefault()); noThrow = true; }
        catch (Exception e) { falseResult = true; Debug.LogError(e.Message); }
        Check("F5 写盘失败返回 false 且不抛异常", noThrow && !falseResult);

        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    // ═══════════════════════ 工具 ═══════════════════════

    static SaveSystem NewMemorySystem()
    {
        var store = new MemoryFileStore();
        return new SaveSystem(store, TestPath);
    }

    static string MakeValidText()
    {
        var store = new MemoryFileStore();
        var sys = new SaveSystem(store, TestPath);
        var file = SaveSystem.CreateDefault();
        file.progress.magicCrystal = 42;
        file.progress.quests.Add(new QuestSave { questId = "q_bom", status = QuestStatus.Active, progress = 1 });
        sys.Save(file);
        return store.GetRaw(TestPath);
    }

    static string FreshTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), TempFolderName);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void Check(string name, bool ok, string detail = null)
    {
        if (ok) { _pass++; return; }
        _fail++;
        _failures.Add(name + (string.IsNullOrEmpty(detail) ? string.Empty : " → " + detail));
    }
}
