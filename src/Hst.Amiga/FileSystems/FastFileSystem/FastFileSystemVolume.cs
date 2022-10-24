namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using RigidDiskBlocks;

    public class FastFileSystemVolume : IFileSystemVolume
    {
        private readonly Volume volume;
        private EntryBlock currentDirectory;

        public FastFileSystemVolume(Volume volume, EntryBlock currentDirectory)
        {
            this.volume = volume;
            this.currentDirectory = currentDirectory;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        public void Dispose()
        {
        }

        public string Name => volume.RootBlock.DiskName;

        public long Size => (long)volume.Blocks * volume.BlockSize;

        public long Free
        {
            get
            {
                var freeBlocks = volume.BitmapTable.Sum(bitmapBlock =>
                    bitmapBlock.Map.Sum(m => MapBlockHelper.ConvertUInt32ToBlockFreeMap(m).Count(f => f)));

                return (long)freeBlocks * volume.BlockSize;
            }
        }

        public async Task<IEnumerable<FileSystems.Entry>> ListEntries()
        {
            return (await Directory.ReadEntries(volume,
                    FastFileSystemHelper.GetSector(volume, currentDirectory.HeaderKey))).Select(EntryConverter.ToEntry)
                .ToList();
        }

        public async Task ChangeDirectory(string path)
        {
            var isRootPath = path.StartsWith("/");
            if (isRootPath)
            {
                currentDirectory = volume.RootBlock;
            }

            var findEntryResult = await Directory.FindEntry(currentDirectory, path, volume);

            if (findEntryResult.PartsNotFound.Any())
            {
                throw new IOException("Not found");
            }

            currentDirectory = findEntryResult.EntryBlock;
        }

        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        public async Task CreateDirectory(string dirName)
        {
            await Directory.CreateDirectory(volume, currentDirectory, dirName);
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        public async Task CreateFile(string fileName)
        {
            using (var _ = await File.Open(volume, currentDirectory, fileName, FileMode.Write))
            {
            }
        }

        /// <summary>
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="write"></param>
        /// <returns></returns>
        public async Task<Stream> OpenFile(string fileName, bool write)
        {
            return await File.Open(volume, currentDirectory, fileName,
                write ? FileMode.Write : FileMode.Read);
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        public async Task Delete(string name)
        {
            await Directory.RemoveEntry(volume, FastFileSystemHelper.GetSector(volume, currentDirectory.HeaderKey),
                name);
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName)
        {
            var srcEntryResult = await Directory.FindEntry(currentDirectory, oldName, volume);

            if (srcEntryResult.PartsNotFound.Any())
            {
                throw new IOException("Not found");
            }

            var destEntryResult = await Directory.FindEntry(currentDirectory, newName, volume);
            var partsNotFound = destEntryResult.PartsNotFound.ToList();

            if (!partsNotFound.Any())
            {
                throw new IOException("New name exists");
            }

            if (partsNotFound.Count > 1)
            {
                throw new IOException($"Directory '{partsNotFound[0]}' not found");
            }

            var srcSector = srcEntryResult.EntryBlock is RootBlock
                ? volume.RootBlockOffset
                : srcEntryResult.EntryBlock.HeaderKey;
            var destSector = destEntryResult.EntryBlock is RootBlock
                ? volume.RootBlockOffset
                : destEntryResult.EntryBlock.HeaderKey;

            await Directory.RenameEntry(volume, srcSector, srcEntryResult.Name, destSector, destEntryResult.Name);
        }

        /// <summary>
        /// Set comment for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="comment"></param>
        public async Task SetComment(string name, string comment)
        {
            await Directory.SetEntryComment(volume, currentDirectory, name, comment);
        }

        /// <summary>
        /// Set protection bits for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="protectionBits"></param>
        public async Task SetProtectionBits(string name, ProtectionBits protectionBits)
        {
            await Directory.SetEntryAccess(volume, currentDirectory, name,
                EntryConverter.GetAccess(protectionBits));
        }

        /// <summary>
        /// Set creation date for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="date"></param>
        public async Task SetDate(string name, DateTime date)
        {
            await Directory.SetEntryDate(volume, currentDirectory, name, date);
        }

        /// <summary>
        /// Mount pfs3 volume in stream using partition block information
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partitionBlock"></param>
        /// <returns></returns>
        public static async Task<FastFileSystemVolume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            var volume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
                partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
                partitionBlock.FileSystemBlockSize);

            return new FastFileSystemVolume(volume, volume.RootBlock);
        }
    }
}