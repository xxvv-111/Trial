using System;
using System.IO;
using System.Text;

namespace Game.Save
{
    /// <summary>
    /// 真实磁盘实现。核心是 WriteAllTextAtomic：写 tmp → 刷盘 → 旧档转 .bak → 替换。
    ///
    /// 为什么不能直接 File.WriteAllText：
    /// 断电 / 崩溃 / 磁盘满时可能留下半截文件，下次启动就是坏档，进度全丢。
    /// 原子写保证"任何时刻磁盘上都至少有一份完整档"。
    /// </summary>
    public sealed class DiskFileStore : IFileStore
    {
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";

        public bool Exists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

        public void WriteAllTextAtomic(string path, string text)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path 为空", nameof(path));
            if (text == null) text = string.Empty;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var tmp = path + TempSuffix;
            try
            {
                // UTF8Encoding(false)：不写 BOM。JsonUtility.FromJson 遇到 BOM 会抛 ArgumentException。
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None,
                                               4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    writer.Write(text);
                    writer.Flush();
                    fs.Flush(true);   // true = 刷到物理盘，不是只进 OS 缓存
                }

                if (File.Exists(path))
                    File.Replace(tmp, path, path + BackupSuffix);   // 旧档自动变成 .bak
                else
                    File.Move(tmp, path);
            }
            finally
            {
                // 失败时清掉残片；成功时 tmp 已经被 Replace/Move 消耗掉，这里是空操作
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch { /* 清不掉也不能让存档流程抛出去 */ }
                }
            }
        }

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(destinationPath))
            {
                if (!overwrite) throw new IOException($"目标已存在：{destinationPath}");
                File.Delete(destinationPath);
            }
            File.Move(sourcePath, destinationPath);
        }

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
