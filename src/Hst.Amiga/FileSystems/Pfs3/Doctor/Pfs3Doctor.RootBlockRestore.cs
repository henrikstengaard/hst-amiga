namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using RigidDiskBlocks;
    using Constants = Hst.Amiga.FileSystems.Pfs3.Constants;

    /// <summary>
    /// Restore of displaced boot and root blocks. Not part of original PFSDoctor.
    /// </summary>
    public partial class Pfs3Doctor
    {
        private const int RestoreSectorSize = 512;

        /// <summary>
        /// Default maximum number of sectors searched before and after volume start for displaced boot and root
        /// blocks.
        /// </summary>
        public const int DefaultMaxDisplacementSectors = 262144;

        /// <summary>
        /// Restore displaced boot and root blocks to volume start. Boot and root blocks are displaced, if a copy
        /// of them is written at a wrong offset, while original boot and root blocks at volume start are destroyed.
        /// A displaced copy is found by searching sectors near volume start for a valid root block with a boot
        /// block before it, a disk size matching the partition and a root block extension located relative to
        /// volume start. Boot block and root block cluster are then copied to volume start.
        /// </summary>
        /// <param name="stream">Stream with disk.</param>
        /// <param name="partitionBlock">Partition block for volume.</param>
        /// <param name="write">Write restored boot and root blocks. If false, displaced boot and root blocks are
        /// only searched and reported.</param>
        /// <param name="maxDisplacementSectors">Maximum number of sectors searched before and after volume start.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task<RootBlockRestoreResult> RestoreRootBlock(Stream stream, PartitionBlock partitionBlock,
            bool write = true, int maxDisplacementSectors = DefaultMaxDisplacementSectors,
            CancellationToken cancellationToken = default)
        {
            var result = new RootBlockRestoreResult();

            var sectorsPerBlock = Math.Max(1U, partitionBlock.Sectors);
            var blockSize = (int)(sectorsPerBlock * RestoreSectorSize);
            var cylinderSectors = (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack;
            var volumeStart = partitionBlock.LowCyl * cylinderSectors;
            var volumeSectors = (partitionBlock.HighCyl - partitionBlock.LowCyl + 1L) * cylinderSectors;
            var diskSectors = stream.Length / RestoreSectorSize;

            // root block at volume start is valid
            var rootBlockAtStart = await ReadSectors(stream, volumeStart + Constants.ROOTBLOCK * sectorsPerBlock, 1,
                cancellationToken);
            var rootBlock = RootBlockReader.TryParseHardDiskRootBlock(rootBlockAtStart);
            if (rootBlock != null && await IsExtensionAt(stream, volumeStart, rootBlock, sectorsPerBlock,
                    cancellationToken))
            {
                result.RootBlockValid = true;
                result.Messages.Add($"Root block at volume start sector {volumeStart} is valid, no restore needed");
                return result;
            }

            // search for displaced root block nearest volume start
            var searchStart = Math.Max(0, volumeStart - maxDisplacementSectors);
            var searchEnd = Math.Min(diskSectors, volumeStart + maxDisplacementSectors + 1);
            var buffer = new byte[1024 * RestoreSectorSize];
            long? bestRootSector = null;
            Blocks.RootBlock bestRootBlock = null;

            for (var sector = searchStart; sector < searchEnd; sector += buffer.Length / RestoreSectorSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sectors = (int)Math.Min(buffer.Length / RestoreSectorSize, searchEnd - sector);
                stream.Seek(sector * RestoreSectorSize, SeekOrigin.Begin);
                await ReadFully(stream, buffer, sectors * RestoreSectorSize, cancellationToken);

                for (var i = 0; i < sectors; i++)
                {
                    var rootSector = sector + i;
                    var diskType = BigEndianConverter.ConvertBytesToInt32(buffer, i * RestoreSectorSize);
                    if (rootSector == volumeStart + Constants.ROOTBLOCK * sectorsPerBlock ||
                        (diskType != Constants.ID_PFS_DISK && diskType != Constants.ID_PFS2_DISK))
                    {
                        continue;
                    }

                    var blockBytes = new byte[RestoreSectorSize];
                    Array.Copy(buffer, i * RestoreSectorSize, blockBytes, 0, RestoreSectorSize);
                    var candidate = RootBlockReader.TryParseHardDiskRootBlock(blockBytes);

                    // disk size must match partition
                    if (candidate == null || (long)candidate.DiskSize * sectorsPerBlock != volumeSectors)
                    {
                        continue;
                    }

                    // boot block must be located before root block
                    var bootSector = rootSector - Constants.ROOTBLOCK * sectorsPerBlock;
                    if (bootSector < 0)
                    {
                        continue;
                    }

                    var bootBlockBytes = await ReadSectors(stream, bootSector, 1, cancellationToken);
                    var bootDiskType = BigEndianConverter.ConvertBytesToInt32(bootBlockBytes, 0);
                    if (bootDiskType != Constants.ID_PFS_DISK && bootDiskType != Constants.ID_PFS2_DISK)
                    {
                        continue;
                    }

                    // root block extension must be located relative to volume start
                    if (!await IsExtensionAt(stream, volumeStart, candidate, sectorsPerBlock, cancellationToken))
                    {
                        continue;
                    }

                    if (!bestRootSector.HasValue ||
                        Math.Abs(rootSector - volumeStart) < Math.Abs(bestRootSector.Value - volumeStart))
                    {
                        bestRootSector = rootSector;
                        bestRootBlock = candidate;
                    }
                }
            }

            if (!bestRootSector.HasValue)
            {
                result.Messages.Add(
                    $"Root block at volume start sector {volumeStart} is not valid and no displaced boot and root blocks were found");
                return result;
            }

            var sourceSector = bestRootSector.Value - Constants.ROOTBLOCK * sectorsPerBlock;
            var blocksToCopy = Constants.ROOTBLOCK + Math.Max(1, (int)bestRootBlock.RblkCluster);
            var sectorsToCopy = (int)(blocksToCopy * sectorsPerBlock);

            result.Found = true;
            result.SourceSector = sourceSector;
            result.DisplacedSectors = sourceSector - volumeStart;
            result.SectorsCopied = sectorsToCopy;
            result.Messages.Add(
                $"Found displaced boot and root blocks for volume '{bestRootBlock.DiskName}' at sector {sourceSector}, displaced {result.DisplacedSectors} sectors from volume start sector {volumeStart}");

            if (!write)
            {
                result.Messages.Add("Boot and root blocks are not restored, write is disabled");
                return result;
            }

            if (!stream.CanWrite)
            {
                result.Messages.Add("Boot and root blocks are not restored, stream is not writable");
                return result;
            }

            // read boot block and root block cluster before writing as source and destination can overlap
            var bytes = await ReadSectors(stream, sourceSector, sectorsToCopy, cancellationToken);
            stream.Seek(volumeStart * RestoreSectorSize, SeekOrigin.Begin);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            result.Restored = true;
            result.Messages.Add(
                $"Restored {blocksToCopy} blocks ({sectorsToCopy} sectors) of boot and root blocks from sector {sourceSector} to volume start sector {volumeStart}, block size {blockSize}");

            return result;
        }

        private static async Task<bool> IsExtensionAt(Stream stream, long volumeStart, Blocks.RootBlock rootBlock,
            uint sectorsPerBlock, CancellationToken cancellationToken)
        {
            var extensionBlock = RootBlockReader.GetExtensionBlock(rootBlock);
            if (extensionBlock == 0)
            {
                // root block doesn't use extension, can't be verified
                return true;
            }

            var extensionSector = volumeStart + (long)extensionBlock * sectorsPerBlock;
            if (extensionSector >= stream.Length / RestoreSectorSize)
            {
                return false;
            }

            var bytes = await ReadSectors(stream, extensionSector, 1, cancellationToken);
            return RootBlockReader.IsExtensionBlock(bytes);
        }

        private static async Task<byte[]> ReadSectors(Stream stream, long sector, int sectors,
            CancellationToken cancellationToken)
        {
            var bytes = new byte[sectors * RestoreSectorSize];
            stream.Seek(sector * RestoreSectorSize, SeekOrigin.Begin);
            await ReadFully(stream, bytes, bytes.Length, cancellationToken);
            return bytes;
        }

        private static async Task ReadFully(Stream stream, byte[] buffer, int length,
            CancellationToken cancellationToken)
        {
            var total = 0;
            while (total < length)
            {
                var read = await stream.ReadAsync(buffer, total, length - total, cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            if (total < length)
            {
                Array.Clear(buffer, total, length - total);
            }
        }
    }
}
