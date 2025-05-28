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
        private uint dirNodeNr;

        public Pfs3Volume(globaldata g, uint dirNodeNr)
        {
            this.g = g;
            this.dirNodeNr = dirNodeNr;
        }

        public async ValueTask DisposeAsync()
        {
            await Pfs3Helper.Flush(g);

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
        public async Task<FindEntryResult> FindEntry(string name)
        {
            if (name.IndexOf("/", StringComparison.Ordinal) >= 0 || name.IndexOf("\\", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("Name contains directory separator", nameof(name));
            }
            
            var objectInfo = await GetCurrentDirectory();
            var found = await Directory.SearchInDir(dirNodeNr, name, objectInfo, g);

            return new FindEntryResult
            {
                PartsNotFound = found ? new List<string>() : new List<string> { name },
                Entry = found ? DirEntryConverter.ToEntry(objectInfo.file.direntry) : null
            };
        }
        
        /// <summary>
        /// Change current directory
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        public async Task ChangeDirectory(string path)
        {
            var currentDirectory = await GetCurrentDirectory();
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
            var currentDirectory = await GetCurrentDirectory();
            var entry = await Directory.NewDir(currentDirectory, dirName, g);

            // TODO: Examine when it's necessary to keep entyr in volume file entries after creating new dir
            for (var node = g.currentvolume.fileentries.First; node != null; node = node.Next)
            {
                if (node.Value != entry)
                {
                    continue;
                }
                g.currentvolume.fileentries.Remove(node);
            }
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreProtectionBits"></param>
        public async Task CreateFile(string fileName, bool overwrite = false, bool ignoreProtectionBits = false)
        {
            g.IgnoreProtectionBits = ignoreProtectionBits;
            var currentDirectory = await GetCurrentDirectory();
            var objectInfo = currentDirectory.Clone();
            var found = !(await Directory.Find(objectInfo, fileName, g)).Any();
            await Directory.NewFile(found, currentDirectory, fileName, objectInfo, overwrite, g);

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
            // foreach (var anblk in g.currentvolume.anblks)
            // {
            //     anblk.Clear();
            // }
            g.currentvolume.anblks.Clear();
            // foreach (var dirblk in g.currentvolume.dirblks)
            // {
            //     dirblk.Clear();
            // }
            g.currentvolume.dirblks.Clear();
            g.currentvolume.bmblks.Clear();
            g.currentvolume.bmblksBySeqNr.Clear();
            g.currentvolume.bmindexblks.Clear();
            g.currentvolume.bmindexblksBySeqNr.Clear();
            g.currentvolume.deldirblks.Clear();
            g.currentvolume.deldirblksBySeqNr.Clear();
            g.currentvolume.indexblks.Clear();
            g.currentvolume.indexblksBySeqNr.Clear();
            g.currentvolume.superblks.Clear();
            g.currentvolume.superblksBySeqNr.Clear();
            

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
            g.IgnoreProtectionBits = ignoreProtectionBits;
            var currentDirectory = await GetCurrentDirectory();
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, fileName, g)).Any())
            {
                // remaining parts of path is returned, not found
                if (mode == FileMode.Read)
                {
                    throw new PathNotFoundException($"Path '{fileName}' not found");
                }

                // create new file
                await Directory.NewFile(false, currentDirectory, fileName, objectInfo, true, g);
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
            g.IgnoreProtectionBits = ignoreProtectionBits;
            var currentDirectory = await GetCurrentDirectory();
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.DeleteObject(objectInfo, g);
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName)
        {
            var currentDirectory = await GetCurrentDirectory();
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
            var currentDirectory = await GetCurrentDirectory();
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
            var currentDirectory = await GetCurrentDirectory();
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.ProtectFile(objectInfo, ProtectionBitsConverter.ToProtectionValue(protectionBits), g);
        }

        /// <summary>
        /// Set creation date for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="date"></param>
        public async Task SetDate(string name, DateTime date)
        {
            var currentDirectory = await GetCurrentDirectory();
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }
            await Directory.SetDate(objectInfo, date, g);
        }

        private async Task<objectinfo> GetCurrentDirectory()
        {
            if (dirNodeNr == Constants.ANODE_ROOTDIR)
            {
                return await Directory.GetRoot(g);
            }

            var canode = new canode();
            await anodes.GetAnode(canode, dirNodeNr, g);
            
            var dirBlock = await Directory.LoadDirBlock(canode.blocknr, g);
            
            return new objectinfo
            {
                volume = new volumeinfo
                {
                    root = (uint)(dirNodeNr == Constants.ST_USERDIR ? 0 : 1),
                    volume = g.currentvolume
                },
                file = new fileinfo
                {
                    dirblock = dirBlock,
                    direntry = new direntry(0, Constants.ST_USERDIR, dirNodeNr, 0, 0, DateTime.Now, string.Empty, string.Empty, new extrafields(), g)
                }
            };
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
            
            //var root = (await Directory.GetRoot(g)).Clone();
            var dirNodeNr = (uint)Macro.ANODE_ROOTDIR;
            
            return new Pfs3Volume(g, dirNodeNr);
        }

        /// <summary>
        /// Flush file system changes
        /// </summary>
        /// <returns></returns>
        public async Task Flush()
        {
            await Pfs3Helper.Flush(g);
        }

        public IEnumerable<string> GetStatus()
        {
            return new[]
            {
                $"PF3 cache status:",
                $"Cached anode blocks: {g.currentvolume.anblks.Count}",
                $"Cached dir blocks: {g.currentvolume.dirblks.Count}",
                $"Cached deldir blocks: {g.currentvolume.deldirblks.Count}",
                $"Cached bitmap blocks: {g.currentvolume.bmblks.Count}",
                $"Cached bitmap index blocks: {g.currentvolume.bmindexblks.Count}",
                $"Cached index blocks: {g.currentvolume.indexblks.Count}",
                $"Cached super blocks: {g.currentvolume.superblks.Count}",
                $"LRU queue blocks: {g.glob_lrudata.LRUqueue.Count}",
                $"LRU array blocks: {g.glob_lrudata.LRUarray.Length}",
                $"LRU pool blocks: {g.glob_lrudata.LRUpool.Count}"
            };
        }

        public void Dispose()
        {
            Pfs3Helper.Flush(g).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Get the path to the current directory.
        /// </summary>
        /// <returns>Path to current directory.</returns>
        /// <exception cref="IOException"></exception>
        public async Task<string> GetCurrentPath()
        {
            var currentDirectory = await GetCurrentDirectory();
            var parentDirectory = new objectinfo();
            var pathComponents = new LinkedList<string>();

            do
            {
                if (!await Directory.GetParent(currentDirectory, parentDirectory, g))
                {
                    break;
                }

                if (parentDirectory.file.dirblock.dirblock == null)
                {
                    break;
                }

                currentDirectory = parentDirectory;

                pathComponents.AddFirst(currentDirectory.file.direntry.Name);
            } while (currentDirectory.file.dirblock.dirblock != null &&
                     currentDirectory.file.dirblock.dirblock.anodenr != Constants.ANODE_ROOTDIR);

            return string.Concat("/", string.Join("/", pathComponents.ToList()));
        }

        /// <summary>
        /// Current directory block number.
        /// </summary>
        public uint CurrentDirectoryBlockNumber => dirNodeNr;
    }
}