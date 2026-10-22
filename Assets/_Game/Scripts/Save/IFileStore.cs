namespace Game.Save
{
    /// <summary>
    /// 存储后端接口。存档系统只依赖它，不直接碰 System.IO。
    /// 换成本地文件（DiskFileStore）、内存（MemoryFileStore）、云、主机平台 SDK 都只需换一个实现。
    /// </summary>
    public interface IFileStore
    {
        bool Exists(string path);

        /// <summary>读全文。文件不存在时的行为由实现决定（DiskFileStore 会抛 FileNotFoundException）。</summary>
        string ReadAllText(string path);

        /// <summary>
        /// 原子写入：要么彻底成功，要么目标文件保持原样，绝不留下半截文件。
        /// 实现必须保证"失败时目标路径内容不变"。
        /// </summary>
        void WriteAllTextAtomic(string path, string text);

        void Move(string sourcePath, string destinationPath, bool overwrite);

        void Delete(string path);
    }
}
