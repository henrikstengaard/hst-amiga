namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using RigidDiskBlocks;
    using FileMode = FileMode;

    public class FastFileSystemVolume : IFileSystemVolume
    {
        private readonly Volume volume;
        private uint currentDirectorySector;

        public FastFileSystemVolume(Volume volume, uint currentDirectorySector)
        {
            this.volume = volume;
            this.currentDirectorySector = currentDirectorySector;
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
            return (await Directory.ReadEntries(volume, currentDirectorySector)).Select(EntryConverter.ToEntry)
                .ToList();
        }

        public async Task ChangeDirectory(string path)
        {
            var isRootPath = path.StartsWith("/");
            if (isRootPath)
            {
                currentDirectorySector = volume.RootBlockOffset;
            }

            var findEntryResult = await Directory.FindEntry(currentDirectorySector, path, volume);

            if (findEntryResult.PartsNotFound.Any())
            {
                throw new IOException("Not found");
            }

            currentDirectorySector = findEntryResult.Sector;
        }

        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        public async Task CreateDirectory(string dirName)
        {
            await Directory.CreateDirectory(volume, currentDirectorySector, dirName);
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        public async Task CreateFile(string fileName)
        {
            using (var _ = await File.Open(volume, currentDirectorySector, fileName, FileMode.Write))
            {
            }
        }

        /// <summary>
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public async Task<Stream> OpenFile(string fileName, FileMode mode)
        {
            return await File.Open(volume, currentDirectorySector, fileName, mode);
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        public async Task Delete(string name)
        {
            await Directory.RemoveEntry(volume, currentDirectorySector, name);
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName)
        {
            var srcEntryResult = await Directory.FindEntry(currentDirectorySector, oldName, volume);

            if (srcEntryResult.PartsNotFound.Any())
            {
                throw new IOException("Not found");
            }

            var destEntryResult = await Directory.FindEntry(currentDirectorySector, newName, volume);
            var partsNotFound = destEntryResult.PartsNotFound.ToList();

            if (!partsNotFound.Any())
            {
                throw new IOException("New name exists");
            }

            if (partsNotFound.Count > 1)
            {
                throw new IOException($"Directory '{partsNotFound[0]}' not found");
            }

            await Directory.RenameEntry(volume, srcEntryResult.Sector, srcEntryResult.Name, destEntryResult.Sector, 
                destEntryResult.Name);
        }

        /// <summary>
        /// Set comment for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="comment"></param>
        public async Task SetComment(string name, string comment)
        {
            await Directory.SetEntryComment(volume, currentDirectorySector, name, comment);
        }

        /// <summary>
        /// Set protection bits for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="protectionBits"></param>
        public async Task SetProtectionBits(string name, ProtectionBits protectionBits)
        {
            await Directory.SetEntryAccess(volume, currentDirectorySector, name,
                EntryConverter.GetAccess(protectionBits));
        }

        /// <summary>
        /// Set creation date for file in current directory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="date"></param>
        public async Task SetDate(string name, DateTime date)
        {
            await Directory.SetEntryDate(volume, currentDirectorySector, name, date);
        }

        /// <summary>
        /// Mount partition fast file system volume from stream partition block
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partitionBlock"></param>
        /// <returns></returns>
        public static async Task<FastFileSystemVolume> MountPartition(Stream stream, PartitionBlock partitionBlock)
        {
            return await Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
                partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved, partitionBlock.BlockSize,
                partitionBlock.FileSystemBlockSize);
        }

        /// <summary>
        /// Mount adf fast file system volume from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>Fast file system volume</returns>
        public static async Task<FastFileSystemVolume> MountAdf(Stream stream)
        {
            var volume = await FastFileSystemHelper.MountAdf(stream);

            return new FastFileSystemVolume(volume, volume.RootBlockOffset);
        }

        /// <summary>
        /// Mount fast file system volume from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="lowCyl"></param>
        /// <param name="highCyl"></param>
        /// <param name="surfaces"></param>
        /// <param name="blocksPerTrack"></param>
        /// <param name="reserved"></param>
        /// <param name="blockSize"></param>
        /// <param name="fileSystemBlockSize"></param>
        /// <returns></returns>
        public static async Task<FastFileSystemVolume> Mount(Stream stream, uint lowCyl, uint highCyl,
            uint surfaces, uint blocksPerTrack, uint reserved, uint blockSize, uint fileSystemBlockSize)
        {
            var volume = await FastFileSystemHelper.Mount(stream, lowCyl, highCyl, surfaces, blocksPerTrack, reserved, blockSize,
                fileSystemBlockSize);

            return new FastFileSystemVolume(volume, volume.RootBlockOffset);
        }
    }
}