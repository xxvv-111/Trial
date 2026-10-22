using System.IO;
using Game.Save;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 存档调试菜单。用途：确认到底读写的是哪个文件、当前档多大、怎么快速造一份测试档。
/// </summary>
public static class SaveMenu
{
    const string MenuRoot = "Tools/存档/";

    static string SaveDir => Application.persistentDataPath;
    static string SavePath => Path.Combine(SaveDir, SaveSystem.DefaultFileName);

    [MenuItem(MenuRoot + "打开存档目录", false, 1)]
    static void OpenFolder()
    {
        if (!Directory.Exists(SaveDir)) Directory.CreateDirectory(SaveDir);
        EditorUtility.RevealInFinder(SaveDir);
    }

    [MenuItem(MenuRoot + "打印存档路径与大小", false, 2)]
    static void PrintInfo()
    {
        Debug.Log($"[存档] 目录：{SaveDir}");
        Debug.Log($"[存档] 主档：{Describe(SavePath)}");
        Debug.Log($"[存档] 备份：{Describe(SavePath + DiskFileStore.BackupSuffix)}");
        Debug.Log($"[存档] 坏档：{Describe(SavePath + SaveSystem.BadSuffix)}");
        Debug.Log($"[存档] 临时：{Describe(SavePath + DiskFileStore.TempSuffix)}");
    }

    static string Describe(string path)
    {
        if (!File.Exists(path)) return path + "  （不存在）";
        var bytes = new FileInfo(path).Length;
        return $"{path}  （{bytes} B，最后写入 {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}）";
    }

    [MenuItem(MenuRoot + "写入测试数据", false, 20)]
    static void WriteTestData()
    {
        if (Application.isPlaying)
            Debug.LogWarning("[存档] 正在 Play 模式中：ProgressService 会在下次落盘时覆盖这份测试档。");

        var system = new SaveSystem(new DiskFileStore(), SavePath);
        var file = SaveSystem.CreateDefault();
        file.progress.magicCrystal = 999;
        file.progress.learnedSpellIds.Add("spell_fireball");
        file.progress.equipmentLevels.Add(new EquipmentSave { equipmentId = "sword_01", level = 3 });
        file.progress.trainingCounts.Add(new TrainingSave { statId = "str", count = 5 });
        file.progress.quests.Add(new QuestSave { questId = "quest_smith", status = QuestStatus.Active, progress = 2 });
        file.progress.stats.clears = 1;

        if (system.Save(file, "Tavern", 120f))
        {
            Debug.Log($"[存档] 已写入测试档：{SavePath}");
            AssetDatabase.Refresh();
        }
    }

    [MenuItem(MenuRoot + "删除存档（含 .bak/.bad）", false, 40)]
    static void DeleteSave()
    {
        var ok = EditorUtility.DisplayDialog(
            "删除存档",
            "将删除以下三个文件：\n" + SavePath + "\n" + SavePath + ".bak\n" + SavePath + ".bad.json\n\n此操作不可撤销。",
            "删除", "取消");
        if (!ok) return;

        new SaveSystem(new DiskFileStore(), SavePath).DeleteAll();
        Debug.LogWarning("[存档] 已删除存档文件");
    }
}
