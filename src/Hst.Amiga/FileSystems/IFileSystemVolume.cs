﻿namespace Hst.Amiga.FileSystems
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
        Task<FindEntryResult> FindEntry(string name);
        
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
        /// <param name="overwrite"></param>
        /// <param name="ignoreProtectionBits"></param>
        Task CreateFile(string fileName, bool overwrite, bool ignoreProtectionBits);

        /// <summary>
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="mode"></param>
        /// <param name="ignoreProtectionBits"></param>
        /// <returns></returns>
        Task<Stream> OpenFile(string fileName, FileMode mode, bool ignoreProtectionBits);

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        Task Rename(string oldName, string newName);
        
        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ignoreProtectionBits"></param>
        Task Delete(string name, bool ignoreProtectionBits);

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

        /// <summary>
        /// Flush file system changes
        /// </summary>
        /// <returns></returns>
        Task Flush();

        /// <summary>
        /// Get status of file system
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetStatus();

        /// <summary>
        /// Current directory block number.
        /// </summary>
        uint CurrentDirectoryBlockNumber { get; }
        
        /// <summary>
        /// Get the path to the current directory.
        /// </summary>
        /// <returns>Path to current directory.</returns>
        Task<string> GetCurrentPath();
    }
}