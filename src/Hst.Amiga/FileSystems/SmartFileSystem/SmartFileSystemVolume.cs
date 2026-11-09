namespace Hst.Amiga.FileSystems.SmartFileSystem
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

    /// <summary>
    /// Smart file system volume for reading and writing smart file system (SFS\0) partitions.
    /// </summary>
    public class SmartFileSystemVolume : IFileSystemVolume
    {
        private readonly Volume volume;
        private readonly string name;
        private uint currentDirectoryNode;

        public SmartFileSystemVolume(Volume volume, string name, uint currentDirectoryNode)
        {
            this.volume = volume;
            this.name = name;
            this.currentDirectoryNode = currentDirectoryNode;
        }

        /// <summary>
        /// Mounted volume state.
        /// </summary>
        public Volume Volume => volume;

        /// <summary>
        /// Name of volume
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Size of volume in bytes
        /// </summary>
        public long Size => (long)volume.TotalBlocks * volume.BlockSize;

        /// <summary>
        /// Free volume disk space in bytes
        /// </summary>
        public long Free => (long)volume.FreeBlocks * volume.BlockSize;

        /// <summary>
        /// Current directory object node number.
        /// </summary>
        public uint CurrentDirectoryBlockNumber => currentDirectoryNode;

        /// <summary>
        /// Mount smart file system volume in stream using partition block.
        /// </summary>
        public static async Task<SmartFileSystemVolume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            return await Create(await SmartFileSystemHelper.Mount(stream, partitionBlock));
        }

        /// <summary>
        /// Mount smart file system volume in stream.
        /// </summary>
        /// <param name="stream">Stream with smart file system volume.</param>
        /// <param name="partitionOffset">Byte offset of partition in stream.</param>
        /// <param name="partitionSize">Size of partition in bytes.</param>
        /// <param name="blockSize">File system block size.</param>
        public static async Task<SmartFileSystemVolume> Mount(Stream stream, long partitionOffset, long partitionSize,
            int blockSize)
        {
            return await Create(await SmartFileSystemHelper.Mount(stream, partitionOffset, partitionSize, blockSize));
        }

        private static async Task<SmartFileSystemVolume> Create(Volume volume)
        {
            var root = await Objects.ReadObject(volume, Constants.RootNode);
            return new SmartFileSystemVolume(volume, root.Object.Name, Constants.RootNode);
        }

        /// <summary>
        /// List entries in current directory. Hidden entries like recycled directory are not listed.
        /// </summary>
        public async Task<IEnumerable<Entry>> ListEntries()
        {
            var entries = new List<Entry>();
            foreach (var objectInfo in await ListRawEntries())
            {
                if (objectInfo.Object.IsHidden)
                {
                    continue;
                }

                entries.Add(await ToEntry(objectInfo.Object));
            }

            return entries;
        }

        /// <summary>
        /// List raw objects in current directory including hidden objects.
        /// </summary>
        public async Task<IEnumerable<ObjectInfo>> ListRawEntries()
        {
            var dir = await GetCurrentDirectory();
            return await Objects.ListObjects(volume, dir);
        }

        /// <summary>
        /// Find entry in current directory
        /// </summary>
        public async Task<FindEntryResult> FindEntry(string entryName)
        {
            if (entryName.IndexOf("/", StringComparison.Ordinal) >= 0 ||
                entryName.IndexOf("\\", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("Name contains directory separator", nameof(entryName));
            }

            var objectInfo = await Objects.FindObject(volume, await GetCurrentDirectory(), entryName);

            return new FindEntryResult
            {
                PartsNotFound = objectInfo == null ? new List<string> { entryName } : new List<string>(),
                Entry = objectInfo == null ? null : await ToEntry(objectInfo.Object)
            };
        }

        /// <summary>
        /// Change current directory
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        public async Task ChangeDirectory(string path)
        {
            var result = await FindPath(path);
            if (result.PartsNotFound.Any())
            {
                throw new PathNotFoundException($"Path '{path}' not found");
            }

            if (result.Found.Count == 0)
            {
                currentDirectoryNode = result.DirNode;
                return;
            }

            var last = result.Found.Last();
            if (!last.Object.IsDir)
            {
                throw new PathNotFoundException($"Path '{path}' is not a directory");
            }

            currentDirectoryNode = last.Object.ObjectNode;
        }

        /// <summary>
        /// Create directory in current directory
        /// </summary>
        public async Task CreateDirectory(string dirName)
        {
            EnsureWritable();
            await Objects.CreateObject(volume, currentDirectoryNode, new SfsObject
            {
                Name = dirName,
                Bits = Constants.OTypeDir,
                Protection = ProtectionHelper.DefaultProtection,
                DateModified = DateTime.Now
            });
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        public async Task CreateFile(string fileName, bool overwrite = false, bool ignoreProtectionBits = false)
        {
            using (await OpenFile(fileName, FileMode.Write, overwrite, ignoreProtectionBits))
            {
            }
        }

        /// <summary>
        /// Create soft link in current directory. Smart file system doesn't support hard links.
        /// </summary>
        /// <param name="linkName">Name of link to create.</param>
        /// <param name="linkPath">Path of entry to link to.</param>
        /// <param name="overwrite">Overwrite link, if it exists.</param>
        /// <param name="ignoreProtectionBits">Ignore protection bits, if link exists.</param>
        public async Task CreateLink(string linkName, string linkPath, bool overwrite = false,
            bool ignoreProtectionBits = false)
        {
            EnsureWritable();

            var target = await FindPath(linkPath);
            if (target.PartsNotFound.Any() || target.Found.Count == 0)
            {
                throw new PathNotFoundException($"Path not found '{linkPath}'");
            }

            var dir = await GetCurrentDirectory();
            var existing = await Objects.FindObject(volume, dir, linkName);
            if (existing != null)
            {
                if (!overwrite)
                {
                    throw new PathAlreadyExistsException($"Path '{linkName}' already exists");
                }

                await DeleteObject(existing, ignoreProtectionBits);
            }

            await Objects.CreateObject(volume, currentDirectoryNode, new SfsObject
            {
                Name = linkName,
                Bits = Constants.OTypeLink,
                Protection = ProtectionHelper.DefaultProtection,
                DateModified = DateTime.Now
            }, linkPath);
        }

        /// <summary>
        /// Open file for reading or writing data. File is created if it doesn't exist and mode is write or append.
        /// </summary>
        public async Task<Stream> OpenFile(string fileName, FileMode mode, bool overwrite = false,
            bool ignoreProtectionBits = false)
        {
            var write = mode == FileMode.Write || mode == FileMode.Append;
            if (write)
            {
                EnsureWritable();
            }

            var dir = await GetCurrentDirectory();
            var existing = await Objects.FindObject(volume, dir, fileName);

            if (existing != null && !existing.Object.IsFile)
            {
                throw new NotAFileException($"Entry '{fileName}' is not a file");
            }

            switch (mode)
            {
                case FileMode.Read:
                    if (existing == null)
                    {
                        throw new PathNotFoundException($"Path '{fileName}' not found");
                    }

                    if (!ignoreProtectionBits && !ProtectionHelper.IsReadable(existing.Object.Protection))
                    {
                        throw new FileSystemException($"File '{fileName}' does not have read protection bits set");
                    }

                    return await EntryStream.Open(volume, existing.Object.ObjectNode, false);
                case FileMode.Write:
                    if (existing == null)
                    {
                        return await EntryStream.Open(volume, await CreateFileObject(fileName), true);
                    }

                    if (!overwrite)
                    {
                        throw new PathAlreadyExistsException($"Path '{fileName}' already exists");
                    }

                    if (!ignoreProtectionBits && !ProtectionHelper.IsWritable(existing.Object.Protection))
                    {
                        throw new FileSystemException($"File '{fileName}' does not have write protection bits set");
                    }

                    var stream = await EntryStream.Open(volume, existing.Object.ObjectNode, true);
                    stream.SetLength(0);
                    return stream;
                case FileMode.Append:
                    if (existing == null)
                    {
                        return await EntryStream.Open(volume, await CreateFileObject(fileName), true);
                    }

                    if (!ignoreProtectionBits && !ProtectionHelper.IsWritable(existing.Object.Protection))
                    {
                        throw new FileSystemException($"File '{fileName}' does not have write protection bits set");
                    }

                    var appendStream = await EntryStream.Open(volume, existing.Object.ObjectNode, true);
                    appendStream.Seek(0, SeekOrigin.End);
                    return appendStream;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private async Task<uint> CreateFileObject(string fileName)
        {
            return await Objects.CreateObject(volume, currentDirectoryNode, new SfsObject
            {
                Name = fileName,
                Bits = 0,
                Protection = ProtectionHelper.DefaultProtection,
                DateModified = DateTime.Now
            });
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        public async Task Rename(string oldName, string newName)
        {
            EnsureWritable();

            var source = await FindPath(oldName);
            if (source.PartsNotFound.Any() || source.Found.Count == 0)
            {
                throw new PathNotFoundException($"Path '{oldName}' not found");
            }

            var sourceInfo = source.Found.Last();
            var sourceObject = sourceInfo.Object;
            if (sourceObject.ObjectNode == Constants.RootNode)
            {
                throw new FileSystemException("Root directory can't be renamed");
            }

            var destination = await FindPath(newName);
            var partsNotFound = destination.PartsNotFound.ToList();
            string destinationName;
            uint destinationDirNode;

            if (partsNotFound.Count == 0)
            {
                // allow changing case of name for same object
                var destinationInfo = destination.Found.LastOrDefault();
                destinationName = newName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (destinationInfo == null || destinationInfo.Object.ObjectNode != sourceObject.ObjectNode ||
                    string.Equals(destinationName, sourceObject.Name, StringComparison.Ordinal))
                {
                    throw new PathAlreadyExistsException($"Path '{newName}' already exists");
                }

                destinationDirNode = destinationInfo.ParentNode;
            }
            else if (partsNotFound.Count > 1)
            {
                throw new PathNotFoundException($"Path '{partsNotFound[0]}' not found");
            }
            else
            {
                destinationName = partsNotFound[0];
                destinationDirNode = destination.DirNode;
            }

            Objects.ValidateName(destinationName);

            // prevent moving a directory into itself or a sub directory of itself
            if (sourceObject.IsDir)
            {
                var node = destinationDirNode;
                while (node != 0)
                {
                    if (node == sourceObject.ObjectNode)
                    {
                        throw new FileSystemException($"Directory '{oldName}' can't be moved into itself");
                    }

                    node = await Objects.GetParentNode(volume, node);
                }
            }

            var sourceDirNode = sourceInfo.ParentNode;
            await Objects.DetachObject(volume, sourceInfo);
            sourceObject.Name = destinationName;
            await Objects.AttachObject(volume, destinationDirNode, sourceObject);

            await Objects.BumpObject(volume, sourceDirNode);
            if (destinationDirNode != sourceDirNode)
            {
                await Objects.BumpObject(volume, destinationDirNode);
            }
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        public async Task Delete(string entryName, bool ignoreProtectionBits = false)
        {
            EnsureWritable();
            var objectInfo = await FindObjectInfo(entryName);
            await DeleteObject(objectInfo, ignoreProtectionBits);
        }

        private async Task DeleteObject(ObjectInfo objectInfo, bool ignoreProtectionBits)
        {
            if (objectInfo.Object.IsUndeletable)
            {
                throw new FileSystemException($"Entry '{objectInfo.Object.Name}' can't be deleted");
            }

            if (!ignoreProtectionBits && !ProtectionHelper.IsDeletable(objectInfo.Object.Protection))
            {
                throw new FileSystemException($"Entry '{objectInfo.Object.Name}' does not have delete protection bits set");
            }

            await Objects.DeleteObject(volume, objectInfo);
        }

        /// <summary>
        /// Set comment for file in current directory
        /// </summary>
        public async Task SetComment(string entryName, string comment)
        {
            EnsureWritable();
            Objects.ValidateComment(comment);

            var objectInfo = await FindObjectInfo(entryName);
            var sfsObject = objectInfo.Object;
            sfsObject.Comment = comment ?? string.Empty;

            if (objectInfo.Container.Fits(volume.BlockSize))
            {
                await Disk.WriteObjectContainer(volume, objectInfo.Container);
                return;
            }

            // object doesn't fit in object container with new comment, move it to another object container
            var dirNode = objectInfo.ParentNode;
            await Objects.DetachObject(volume, objectInfo);
            await Objects.AttachObject(volume, dirNode, sfsObject);
        }

        /// <summary>
        /// Set protection bits for file in current directory
        /// </summary>
        public async Task SetProtectionBits(string entryName, ProtectionBits protectionBits)
        {
            EnsureWritable();
            var objectInfo = await FindObjectInfo(entryName);
            await Objects.UpdateObject(volume, objectInfo.Object.ObjectNode,
                o => o.Protection = ProtectionHelper.ToProtection(protectionBits, o.Protection));
        }

        /// <summary>
        /// Set date for file in current directory
        /// </summary>
        public async Task SetDate(string entryName, DateTime date)
        {
            EnsureWritable();
            var objectInfo = await FindObjectInfo(entryName);
            await Objects.UpdateObject(volume, objectInfo.Object.ObjectNode, o => o.DateModified = date);
        }

        /// <summary>
        /// Flush file system changes
        /// </summary>
        public async Task Flush()
        {
            if (!volume.IsReadOnly)
            {
                await volume.Stream.FlushAsync();
            }
        }

        public IEnumerable<string> GetStatus()
        {
            var status = new List<string>
            {
                "SFS status:",
                $"Block size: {volume.BlockSize}",
                $"Total blocks: {volume.TotalBlocks}",
                $"Free blocks: {volume.FreeBlocks}",
                $"Case sensitive: {volume.CaseSensitive}",
                $"Recycled: {volume.RootBlock.Recycled}"
            };
            status.AddRange(volume.Logs);
            return status;
        }

        /// <summary>
        /// Get the path to the current directory.
        /// </summary>
        public async Task<string> GetCurrentPath()
        {
            var pathComponents = new LinkedList<string>();
            var node = currentDirectoryNode;
            var visited = new HashSet<uint>();
            while (node != Constants.RootNode && node != 0 && visited.Add(node))
            {
                var objectInfo = await Objects.ReadObject(volume, node);
                pathComponents.AddFirst(objectInfo.Object.Name);
                node = objectInfo.ParentNode;
            }

            return string.Concat("/", string.Join("/", pathComponents));
        }

        public void Dispose()
        {
            Flush().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await Flush();
            GC.SuppressFinalize(this);
        }

        private async Task<SfsObject> GetCurrentDirectory()
        {
            return (await Objects.ReadObject(volume, currentDirectoryNode)).Object;
        }

        private async Task<Entry> ToEntry(SfsObject sfsObject)
        {
            string linkPath = null;
            if (sfsObject.IsSoftLink && sfsObject.Data != 0)
            {
                linkPath = (await Disk.ReadSoftLink(volume, sfsObject.Data)).Path;
            }

            return EntryConverter.ToEntry(sfsObject, linkPath);
        }

        private async Task<ObjectInfo> FindObjectInfo(string path)
        {
            var result = await FindPath(path);
            if (result.PartsNotFound.Any() || result.Found.Count == 0)
            {
                throw new PathNotFoundException($"Path '{path}' not found");
            }

            return result.Found.Last();
        }

        private class FindPathResult
        {
            public IList<ObjectInfo> Found { get; set; }
            public IList<string> PartsNotFound { get; set; }

            /// <summary>
            /// Object node of directory where search ended.
            /// </summary>
            public uint DirNode { get; set; }
        }

        /// <summary>
        /// Find objects in path relative to current directory or absolute from root, if path starts with "/".
        /// </summary>
        private async Task<FindPathResult> FindPath(string path)
        {
            var isRoot = path.StartsWith("/");
            var dirNode = isRoot ? Constants.RootNode : currentDirectoryNode;
            var parts = (isRoot ? path.Substring(1) : path).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var found = new List<ObjectInfo>();
            int i;
            for (i = 0; i < parts.Length; i++)
            {
                var dir = (await Objects.ReadObject(volume, dirNode)).Object;
                var objectInfo = await Objects.FindObject(volume, dir, parts[i]);
                if (objectInfo == null)
                {
                    break;
                }

                if (!objectInfo.Object.IsDir && i < parts.Length - 1)
                {
                    // entry is not a directory and path has more parts
                    break;
                }

                found.Add(objectInfo);
                if (objectInfo.Object.IsDir && i < parts.Length - 1)
                {
                    dirNode = objectInfo.Object.ObjectNode;
                }
            }

            return new FindPathResult
            {
                Found = found,
                PartsNotFound = parts.Skip(i).ToList(),
                DirNode = dirNode
            };
        }

        private void EnsureWritable()
        {
            if (volume.IsReadOnly)
            {
                throw new FileSystemException("Volume is mounted read only");
            }
        }
    }
}
