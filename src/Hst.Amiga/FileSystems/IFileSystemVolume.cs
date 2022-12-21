namespace Hst.Amiga.FileSystems
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface IFileSystemVolume : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Name of volume
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Size of volume in bytes
        /// </summary>
        long Size { get; }
        
        /// <summary>
        /// Free volume disk space in bytes
        /// </summary>
        long Free { get; }

        /// <summary>
        /// List entries in current directory
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<Entry>> ListEntries();

        /// <summary>
        /// Find entry in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task<Entry> FindEntry(string name);
        
        /// <summary>
        /// Change current directory
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        Task ChangeDirectory(string path);

        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        Task CreateDirectory(string dirName);
        
        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        Task CreateFile(string fileName);

        /// <summary>
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        Task<Stream> OpenFile(string fileName, FileMode mode);
        
        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        Task Delete(string name);

        /// <summary>
        /// Set comment for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="comment"></param>
        Task SetComment(string name, string comment);
        
        /// <summary>
        /// Set protection bits for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="protectionBits"></param>
        Task SetProtectionBits(string name, ProtectionBits protectionBits);

        /// <summary>
        /// Set creation date for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="date"></param>
        Task SetDate(string name, DateTime date);
    }
}