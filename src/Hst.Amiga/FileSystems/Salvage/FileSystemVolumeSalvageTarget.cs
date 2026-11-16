namespace Hst.Amiga.FileSystems.Salvage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    /// <summary>
    /// Salvage target re-creating directories and files in a mounted Amiga file system volume, e.g. a partition
    /// in a rigid disk block image file. Protection bits, comments and dates are set on directories and files.
    /// </summary>
    public class FileSystemVolumeSalvageTarget : ISalvageTarget, IDisposable
    {
        private readonly IFileSystemVolume volume;
        private readonly bool ownsVolume;

        /// <summary>
        /// Create salvage target for mounted file system volume.
        /// </summary>
        /// <param name="volume">Mounted file system volume.</param>
        /// <param name="ownsVolume">Dispose volume, when target is disposed.</param>
        public FileSystemVolumeSalvageTarget(IFileSystemVolume volume, bool ownsVolume = false)
        {
            this.volume = volume ?? throw new ArgumentNullException(nameof(volume));
            this.ownsVolume = ownsVolume;
        }

        /// <summary>
        /// Mounted file system volume.
        /// </summary>
        public IFileSystemVolume Volume => volume;

        /// <summary>
        /// Mount partition by drive name in rigid disk block from stream as salvage target. Partition must be
        /// formatted with PFS3, FFS or SFS.
        /// </summary>
        /// <param name="stream">Stream with rigid disk block, e.g. a rigid disk block image file.</param>
        /// <param name="partitionName">Drive name of partition, e.g. DH0.</param>
        /// <returns>Salvage target for mounted partition, which must be disposed.</returns>
        public static async Task<FileSystemVolumeSalvageTarget> MountPartition(Stream stream, string partitionName)
        {
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
            if (rigidDiskBlock == null)
            {
                throw new IOException("Rigid disk block not found");
            }

            var partitionBlock = rigidDiskBlock.PartitionBlocks.FirstOrDefault(x =>
                string.Equals(x.DriveName, partitionName, StringComparison.OrdinalIgnoreCase));
            if (partitionBlock == null)
            {
                throw new IOException($"Partition '{partitionName}' not found");
            }

            return await MountPartition(stream, partitionBlock);
        }

        /// <summary>
        /// Mount partition from stream as salvage target. Partition must be formatted with PFS3, FFS or SFS.
        /// </summary>
        /// <param name="stream">Stream with rigid disk block, e.g. a rigid disk block image file.</param>
        /// <param name="partitionBlock">Partition block of partition.</param>
        /// <returns>Salvage target for mounted partition, which must be disposed.</returns>
        public static async Task<FileSystemVolumeSalvageTarget> MountPartition(Stream stream,
            PartitionBlock partitionBlock)
        {
            var volume = await FileSystemVolumeFactory.MountPartition(stream, partitionBlock);
            return new FileSystemVolumeSalvageTarget(volume, true);
        }

        public async Task CreateDirectory(IReadOnlyList<string> parentPath, SalvageEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ChangeDirectory(parentPath, cancellationToken);

            var existing = await volume.FindEntry(entry.Name, cancellationToken);
            if (existing?.Entry != null && (existing.PartsNotFound == null || !existing.PartsNotFound.Any()))
            {
                if (existing.Entry.Type != EntryType.Dir)
                {
                    throw new IOException($"Entry '{entry.Name}' already exists and is not a directory");
                }

                return;
            }

            await volume.CreateDirectory(entry.Name, cancellationToken);
        }

        public async Task SetDirectoryMetadata(IReadOnlyList<string> parentPath, SalvageEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ChangeDirectory(parentPath, cancellationToken);
            await SetMetadata(entry, cancellationToken);
        }

        public async Task WriteFile(IReadOnlyList<string> parentPath, SalvageEntry entry, Stream data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ChangeDirectory(parentPath, cancellationToken);

            await volume.CreateFile(entry.Name, true, true, cancellationToken);
            using (var stream = await volume.OpenFile(entry.Name, FileSystems.FileMode.Write, true, true,
                       cancellationToken))
            {
                await data.CopyToAsync(stream, CopyBufferSize, cancellationToken);
            }

            await SetMetadata(entry, cancellationToken);
        }

        public Task Flush(CancellationToken cancellationToken = default) => volume.Flush(cancellationToken);

        private const int CopyBufferSize = 81920;

        private async Task ChangeDirectory(IReadOnlyList<string> parentPath, CancellationToken cancellationToken)
        {
            await volume.ChangeDirectory("/", cancellationToken);
            foreach (var name in parentPath)
            {
                await volume.ChangeDirectory(name, cancellationToken);
            }
        }

        private async Task SetMetadata(SalvageEntry entry, CancellationToken cancellationToken)
        {
            await volume.SetProtectionBits(entry.Name, entry.ProtectionBits, cancellationToken);
            await volume.SetComment(entry.Name, entry.Comment ?? string.Empty, cancellationToken);
            await volume.SetDate(entry.Name, entry.Date, cancellationToken);
        }

        public void Dispose()
        {
            if (ownsVolume)
            {
                volume.Dispose();
            }
        }
    }
}
