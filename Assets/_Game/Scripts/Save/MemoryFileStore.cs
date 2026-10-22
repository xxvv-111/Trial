using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Save
{
    /// <summary>
    /// 内存实现。三个用途：
    /// 1) 自测的测试替身（不碰磁盘、毫秒级、可注入故障）
    /// 2) 无盘演示模式
    /// 3) 将来接云存档时的本地缓存层
    /// </summary>
    public sealed class MemoryFileStore : IFileStore
    {
        readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>≥0 时：写入内容长度超过该值就抛 IOException，用来模拟"写到一半断电"。−1 = 关闭。</summary>
        public int FailAfterBytes = -1;

        /// <summary>任何写入都失败，用来测"磁盘满 / 无权限"。</summary>
        public bool AlwaysFail;

        public int WriteCount { get; private set; }

        public IEnumerable<string> Paths => _files.Keys;

        public bool Exists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path)
        {
            string text;
            if (!_files.TryGetValue(path, out text))
                throw new FileNotFoundException("文件不存在：" + path, path);
            return text;
        }

        public void WriteAllTextAtomic(string path, string text)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path 为空", nameof(path));
            if (text == null) text = string.Empty;
            if (AlwaysFail) throw new IOException("模拟磁盘错误：权限不足或磁盘已满");
            if (FailAfterBytes >= 0 && text.Length > FailAfterBytes)
                throw new IOException($"模拟写入中断（{FailAfterBytes} 字符后）");

            // 关键：先写"临时区"，成功后才替换 —— 与真实原子写语义保持一致
            WriteCount++;
            _files[path] = text;
        }

        public void Move(string sourcePath, string destinationPath, bool overwrite)
        {
            string text;
            if (!_files.TryGetValue(sourcePath, out text))
                throw new FileNotFoundException("文件不存在：" + sourcePath, sourcePath);
            if (_files.ContainsKey(destinationPath) && !overwrite)
                throw new IOException("目标已存在：" + destinationPath);

            _files[destinationPath] = text;
            _files.Remove(sourcePath);
        }

        public void Delete(string path) => _files.Remove(path);

        /// <summary>直接塞入原始文本（用于构造坏档）。</summary>
        public void SetRaw(string path, string text) => _files[path] = text;

        public string GetRaw(string path) => _files.TryGetValue(path, out var t) ? t : null;

        public void Clear() => _files.Clear();
    }
}
