namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;
    using RigidDiskBlocks;
    using FileMode = FileMode;

    public class Pfs3Volume : IFileSystemVolume
    {
        internal readonly globaldata g;
        private uint dirNodeNr;
        private MountRegistration mountRegistration = MountRegistration.None;

        internal Pfs3Volume(globaldata g, uint dirNodeNr)
        {
            this.g = g;
            this.dirNodeNr = dirNodeNr;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Pfs3Helper.Flush(g);
            }
            finally
            {
                mountRegistration.Release();
            }

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
        /// Smallest maximum file name length that can be set with <see cref="SetMaxFileNameLength"/>.
        /// </summary>
        public const int MinSupportedMaxFileNameLength = 30;

        /// <summary>
        /// Largest maximum file name length that can be set with <see cref="SetMaxFileNameLength"/>.
        /// </summary>
        public const int MaxSupportedMaxFileNameLength = Constants.FNSIZE - 1;

        /// <summary>
        /// Maximum length of file and directory names in characters. Stored in the root block extension
        /// as file name size (fnsize) minus 1. Older pfs3 versions used 31; newer ones support up to 107.
        /// </summary>
        public int MaxFileNameLength => g.fnsize - 1;

        /// <summary>
        /// Change maximum length of file and directory names. Only new and renamed entries are affected,
        /// existing entries keep their names. The change is written to the root block extension on flush.
        /// When lowering the limit without force, all entries on the volume are checked and the change is
        /// rejected if any existing name is longer than the new limit, as such entries can no longer be found
        /// by their full name.
        /// </summary>
        /// <param name="maxFileNameLength">Maximum file name length in characters, between
        /// <see cref="MinSupportedMaxFileNameLength"/> and <see cref="MaxSupportedMaxFileNameLength"/>.</param>
        /// <param name="force">Lower the limit, even if existing names are longer than the new limit.</param>
        /// <param name="progress">Optional progress of the name scan, reported once per directory scanned.
        /// Only reported when the limit is lowered without force.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentOutOfRangeException">Max file name length is out of range.</exception>
        /// <exception cref="IOException">Volume has no root block extension.</exception>
        /// <exception cref="FileNameTooLongException">An existing name is longer than the new limit and force is false.</exception>
        public async Task SetMaxFileNameLength(int maxFileNameLength, bool force = false,
            IProgress<FileNameScanProgress> progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maxFileNameLength < MinSupportedMaxFileNameLength || maxFileNameLength > MaxSupportedMaxFileNameLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFileNameLength),
                    $"Max file name length must be between {MinSupportedMaxFileNameLength} and {MaxSupportedMaxFileNameLength}");
            }

            var rext = g.currentvolume.rblkextension;
            if (rext == null)
            {
                throw new IOException("Volume has no root block extension to store max file name length");
            }

            var fnsize = (ushort)(maxFileNameLength + 1);
            if (g.fnsize == fnsize && rext.rblkextension.fnsize == fnsize)
            {
                return;
            }

            if (!force && maxFileNameLength < MaxFileNameLength)
            {
                await EnsureNoNameIsLongerThan(maxFileNameLength, progress, cancellationToken);
            }

            rext.rblkextension.fnsize = fnsize;
            g.fnsize = fnsize;
            await Update.MakeBlockDirty(rext, g);
        }

        /// <summary>
        /// Walk all directories on volume and throw, if an entry name is longer than max file name length.
        /// Links are not followed.
        /// </summary>
        private async Task EnsureNoNameIsLongerThan(int maxFileNameLength, IProgress<FileNameScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var directoriesScanned = 0;
            var entriesScanned = 0;

            // link paths are not needed and resolving them is expensive
            var resolveLinkPaths = g.ResolveLinkPaths;
            g.ResolveLinkPaths = false;

            try
            {
                var visited = new HashSet<uint>();
                var pending = new Stack<(uint DirNodeNr, string Path)>();
                pending.Push((Constants.ANODE_ROOTDIR, string.Empty));

                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (nodeNr, path) = pending.Pop();
                    if (!visited.Add(nodeNr))
                    {
                        continue;
                    }

                    foreach (var dirEntry in await Directory.GetDirEntries(nodeNr, g))
                    {
                        var entryPath = string.Concat(path, "/", dirEntry.Name);
                        if (dirEntry.Name.Length > maxFileNameLength)
                        {
                            throw new FileNameTooLongException(
                                $"Entry '{entryPath}' has name length {dirEntry.Name.Length}, which is longer than max file name length {maxFileNameLength}",
                                entryPath, dirEntry.Name.Length);
                        }

                        if (dirEntry.type == Constants.ST_USERDIR)
                        {
                            pending.Push((dirEntry.anode, entryPath));
                        }

                        entriesScanned++;
                    }

                    directoriesScanned++;
                    progress?.Report(new FileNameScanProgress(directoriesScanned, entriesScanned,
                        path.Length == 0 ? "/" : path));
                }
            }
            finally
            {
                g.ResolveLinkPaths = resolveLinkPaths;
            }
        }

        /// <summary>
        /// List entries in current directory
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Entry>> ListEntries(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await Directory.GetDirEntries(dirNodeNr, g)).Select(DirEntryConverter.ToEntry).ToList();
        }

        internal async Task<IEnumerable<direntry>> ListRawEntries()
        {
            return await Directory.GetDirEntries(dirNodeNr, g);
        }

        /// <summary>
        /// Find entry in current directory.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task<FindEntryResult> FindEntry(string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ChangeDirectory(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isRootPath = path.StartsWith("/");
            var relativePath = (isRootPath ? path.Substring(1) : path).TrimEnd('/');
            var currentDirectory = isRootPath ? await Directory.GetRoot(g) : await GetCurrentDirectory();

            // find path relative to current directory or root for absolute path, e.g. "/dir1/dir2"
            if (relativePath.Length > 0)
            {
                if ((await Directory.Find(currentDirectory, relativePath, g)).Any())
                {
                    throw new PathNotFoundException($"Path '{path}' not found");
                }

                // throw exception, if path is not a directory
                if (currentDirectory.file.direntry != null && !Macro.IsDir(currentDirectory))
                {
                    throw new PathNotFoundException($"Path '{path}' is not a directory");
                }
            }

            if (currentDirectory.file.direntry != null &&
                currentDirectory.file.direntry.type == Constants.ST_LINKDIR)
            {
                dirNodeNr = currentDirectory.file.direntry.ExtraFields.link;
                return;
            }
            
            dirNodeNr = Macro.IsRoot(currentDirectory) ? (uint)Macro.ANODE_ROOTDIR : currentDirectory.file.direntry.anode;
        }
        
        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CreateDirectory(string dirName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = await GetCurrentDirectory();
            var entry = await Directory.NewDir(currentDirectory, dirName, g);

            // TODO: Examine when it's necessary to keep entry in volume file entries after creating new dir
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
        /// Create file in current directory.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreProtectionBits"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CreateFile(string fileName, bool overwrite = false, bool ignoreProtectionBits = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            g.IgnoreProtectionBits = ignoreProtectionBits;
            using (await OpenFile(fileName, FileMode.Write, overwrite, ignoreProtectionBits, cancellationToken))
            {
            }
        }
        
        /// <summary>
        /// Create link in current directory.
        /// </summary>
        /// <param name="linkName">Link name to create.</param>
        /// <param name="name">Name of entry to create link to.</param>
        /// <param name="overwrite">Overwrite link, if it exists.</param>
        /// <param name="ignoreProtectionBits">Ignore protection bits, if link exists.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CreateLink(string linkName, string name, bool overwrite = false, bool ignoreProtectionBits = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            g.IgnoreProtectionBits = ignoreProtectionBits;

            var currentDirectory = await GetCurrentDirectory();
            var linkFromDir = currentDirectory;
            
            var linkTo = new objectinfo();
            var linkToFound = await Directory.FindObject(currentDirectory, name, linkTo, g);
            
            if (!linkToFound)
            {
                throw new PathNotFoundException($"Path not found '{name}'");
            }
            
            var type = new ListType
            {
                value = Constants.ET_FILEENTRY
            };

            // make list entry resolves link to entry to real entry, if link to entry is a link dir or file
            IEntry fileFe;
            if ((fileFe = await Lock.MakeListEntry(linkTo, type, g)) == null)
            {
                throw new IOException("make list entry error");
            }
            
            var newLink = new objectinfo();
            await Directory.CreateLink(linkFromDir, linkName, fileFe.ListEntry.info, newLink, g);
            
            Lock.FreeListEntry(fileFe, g);
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
        /// Open file for reading or writing data.
        /// If a file is opened in write or append mode and file does not exist, it will be created.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="mode"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreProtectionBits"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task<Stream> OpenFile(string fileName, FileMode mode, bool overwrite = false, bool ignoreProtectionBits = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            g.IgnoreProtectionBits = ignoreProtectionBits;
            var currentDirectory = await GetCurrentDirectory();
            
            var fileEntry = await File.Open(currentDirectory, fileName, mode, overwrite, g) as fileentry;
            
            File.MakeSharedFileEntriesAndClear(g);
            
            return new EntryStream(fileEntry, g);
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ignoreProtectionBits"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task Delete(string name, bool ignoreProtectionBits = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetComment(string name, string comment, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetProtectionBits(string name, ProtectionBits protectionBits, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetDate(string name, DateTime date, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        /// <param name="resolveLinkPaths"></param>
        /// <param name="force">Mount partition, even if partition is already mounted by another volume on the same stream</param>
        /// <returns></returns>
        /// <exception cref="FileSystems.Exceptions.PartitionAlreadyMountedException">Partition is already mounted on the same stream</exception>
        public static async Task<Pfs3Volume> Mount(Stream stream, PartitionBlock partitionBlock,
            bool resolveLinkPaths = true, bool force = false)
        {
            // register mounted partition before mounting, as mounting pfs3 can write blocks
            var blocksPerCylinder = (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack;
            var registration = MountRegistry.Register(stream,
                partitionBlock.LowCyl * blocksPerCylinder * partitionBlock.BlockSize,
                (partitionBlock.HighCyl - partitionBlock.LowCyl + 1L) * blocksPerCylinder * partitionBlock.BlockSize,
                force);

            try
            {
                var g = await Pfs3Helper.Mount(stream, partitionBlock);
                g.ResolveLinkPaths = resolveLinkPaths;

                var dirNodeNr = (uint)Macro.ANODE_ROOTDIR;

                return new Pfs3Volume(g, dirNodeNr)
                {
                    mountRegistration = registration
                };
            }
            catch
            {
                registration.Release();
                throw;
            }
        }

        /// <summary>
        /// Flush file system changes
        /// </summary>
        /// <returns></returns>
        public async Task Flush(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            try
            {
                AsyncHelper.RunSync(() => Pfs3Helper.Flush(g));
            }
            finally
            {
                mountRegistration.Release();
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Get the path to the current directory.
        /// </summary>
        /// <returns>Path to current directory.</returns>
        /// <exception cref="IOException"></exception>
        public async Task<string> GetCurrentPath(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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