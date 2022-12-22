namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;
    using RigidDiskBlocks;
    using FileMode = FileMode;

    public class Pfs3Volume : IFileSystemVolume, IAsyncDisposable, IDisposable
    {
        public readonly globaldata g;
        private objectinfo currentDirectory;
        private uint dirNodeNr;

        public Pfs3Volume(globaldata g, objectinfo currentDirectory, uint dirNodeNr)
        {
            this.g = g;
            this.currentDirectory = currentDirectory;
            this.dirNodeNr = dirNodeNr;
        }

        public async ValueTask DisposeAsync()
        {
            await Pfs3Helper.Unmount(g);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Name of volume
        /// </summary>
        public string Name => g.RootBlock.DiskName;
        
        /// <summary>
        /// Size of volume in bytes
        /// </summary>
        public long Size => (long)g.RootBlock.DiskSize * g.blocksize;
        
        /// <summary>
        /// Free volume disk space in bytes
        /// </summary>
        public long Free => (long)g.RootBlock.BlocksFree * g.blocksize;

        /// <summary>
        /// List entries in current directory
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Entry>> ListEntries()
        {
            return (await Directory.GetDirEntries(dirNodeNr, g)).Select(DirEntryConverter.ToEntry).ToList();
        }

        /// <summary>
        /// Find entry in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<Entry> FindEntry(string name)
        {
            var objectInfo = currentDirectory.Clone();
            var found = await Directory.SearchInDir(dirNodeNr, name, objectInfo, g);
            return found ? DirEntryConverter.ToEntry(objectInfo.file.direntry) : null;
        }
        
        /// <summary>
        /// Change current directory
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        public async Task ChangeDirectory(string path)
        {
            var isRootPath = path.StartsWith("/");
            if (isRootPath && !Macro.IsRoot(currentDirectory))
            {
                currentDirectory = await Directory.GetRoot(g);
            }
            
            if (!isRootPath && (await Directory.Find(currentDirectory, path, g)).Any())
            {
                throw new PathNotFoundException($"Path '{path}' not found");
            }
            
            dirNodeNr = Macro.IsRoot(currentDirectory) ? (uint)Macro.ANODE_ROOTDIR : currentDirectory.file.direntry.anode;
        }
        
        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        public async Task CreateDirectory(string dirName)
        {
            await Directory.NewDir(currentDirectory, dirName, g);
            await Update.UpdateDisk(g);
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreProtectionBits"></param>
        public async Task CreateFile(string fileName, bool overwrite = false, bool ignoreProtectionBits = false)
        {
            var objectInfo = currentDirectory.Clone();
            var found = !(await Directory.Find(objectInfo, fileName, g)).Any();
            await Directory.NewFile(found, currentDirectory, fileName, objectInfo, overwrite, ignoreProtectionBits, g);
            await Update.UpdateDisk(g);

            foreach (var fileEntry in g.currentvolume.fileentries)
            {
                fileEntry.ListEntry.type.flags.access = Constants.ET_FILEENTRY;
                fileEntry.ListEntry.filelock.fl_Access = Constants.ET_FILEENTRY;
            }

            g.currentvolume.fileentries.Clear();
        }
        
        public void ClearCachedData()
        {
            foreach (var fileentry in g.currentvolume.fileentries)
            {
                fileentry.ListEntry.type.flags.access = Constants.ET_FILEENTRY;
                fileentry.ListEntry.filelock.fl_Access = Constants.ET_FILEENTRY;
            }

            g.currentvolume.fileentries.Clear();
            g.currentvolume.anodechainlist.Clear();
            foreach (var anblk in g.currentvolume.anblks)
            {
                anblk.Clear();
            }
            foreach (var dirblk in g.currentvolume.dirblks)
            {
                dirblk.Clear();
            }
            g.currentvolume.bmblks.Clear();
            g.currentvolume.bmindexblks.Clear();
            g.currentvolume.deldirblks.Clear();
            g.currentvolume.indexblks.Clear();
            g.currentvolume.superblks.Clear();
            

            g.glob_lrudata.LRUarray = Array.Empty<LruCachedBlock>();
            g.glob_lrudata.poolsize = 0;
            g.glob_lrudata.LRUpool.Clear();
            g.glob_lrudata.LRUqueue.Clear();
        }

        /// <summary>
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="mode"></param>
        /// <param name="ignoreProtectionBits"></param>
        /// <returns></returns>
        public async Task<Stream> OpenFile(string fileName, FileMode mode, bool ignoreProtectionBits = false)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, fileName, g)).Any())
            {
                // remaining parts of path is returned, not found
                if (mode == FileMode.Read)
                {
                    throw new PathNotFoundException($"Path '{fileName}' not found");
                }

                // create new file
                await Directory.NewFile(false, currentDirectory, fileName, objectInfo, true, ignoreProtectionBits, g);
                await Update.UpdateDisk(g);
            }

            var hasReadProtectionBit =
                (objectInfo.file.direntry.protection & Constants.FIBF_READ) == 0;

            if (!ignoreProtectionBits && !hasReadProtectionBit)
            {
                throw new FileSystemException($"File '{fileName}' does not have read protection bits set");
            }
            
            var fileEntry = await File.Open(objectInfo, mode == FileMode.Write || mode == FileMode.Append, g) as fileentry;
            
            File.MakeSharedFileEntriesAndClear(g);
            
            return new EntryStream(fileEntry, g);
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ignoreProtectionBits"></param>
        public async Task Delete(string name, bool ignoreProtectionBits = false)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.DeleteObject(objectInfo, ignoreProtectionBits, g);
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName)
        {
            var srcInfo = currentDirectory.Clone();
            if ((await Directory.Find(srcInfo, oldName, g)).Any())
            {
                throw new PathNotFoundException($"Path '{oldName}' not found");
            }

            var destInfo = currentDirectory.Clone();
            var remainingParts = await Directory.Find(destInfo, newName, g);

            if (remainingParts.Length == 0)
            {
                throw new PathAlreadyExistsException($"Path '{newName}' already exists");
            }

            if (remainingParts.Length > 1)
            {
                throw new PathNotFoundException($"Path '{remainingParts[0]}' not found");
            }
            
            await Directory.RenameAndMove(currentDirectory, srcInfo, destInfo, remainingParts[0], g);
        }

        /// <summary>
        /// Set comment for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="comment"></param>
        public async Task SetComment(string name, string comment)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.AddComment(objectInfo, comment, g);
        }

        /// <summary>
        /// Set protection bits for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="protectionBits"></param>
        public async Task SetProtectionBits(string name, ProtectionBits protectionBits)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.ProtectFile(objectInfo, DirEntryConverter.GetProtection(protectionBits), g);
        }

        /// <summary>
        /// Set creation date for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="date"></param>
        public async Task SetDate(string name, DateTime date)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.SetDate(objectInfo, date, g);
        }
        
        /// <summary>
        /// Mount pfs3 volume in stream using partition block information
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partitionBlock"></param>
        /// <returns></returns>
        public static async Task<Pfs3Volume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            var g = await Pfs3Helper.Mount(stream, partitionBlock);
            
            var root = await Directory.GetRoot(g);
            var dirNodeNr = (uint)Macro.ANODE_ROOTDIR;
            
            return new Pfs3Volume(g, root, dirNodeNr);
        }

        /// <summary>
        /// Flush file system changes
        /// </summary>
        /// <returns></returns>
        public async Task Flush()
        {
            await Pfs3Helper.Unmount(g);
        }

        public void Dispose()
        {
            Pfs3Helper.Unmount(g).GetAwaiter().GetResult();

            GC.SuppressFinalize(this);
        }
    }
}