namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using FileSystems;
    using FfsConstants = FileSystems.FastFileSystem.Constants;
    using FfsRootBlockParser = FileSystems.FastFileSystem.Blocks.RootBlockParser;
    using Pfs3Constants = FileSystems.Pfs3.Constants;
    using Pfs3RootBlock = FileSystems.Pfs3.Blocks.RootBlock;
    using Pfs3RootBlockReader = FileSystems.Pfs3.RootBlockReader;
    using SfsChecksumHelper = FileSystems.SmartFileSystem.ChecksumHelper;
    using SfsConstants = FileSystems.SmartFileSystem.Constants;
    using SfsRootBlockParser = FileSystems.SmartFileSystem.Blocks.RootBlockParser;

    /// <summary>
    /// Scans disk sector by sector for boot and root blocks of known file systems (fast file system, professional
    /// file system 3 and smart file system) to find start offset and size of partitions.
    /// </summary>
    public static class FileSystemScanner
    {
        private const int SectorSize = 512;
        private const int MinBlockSize = 512;
        private const int MaxBlockSize = 32768;

        private class FfsBootBlock
        {
            public long Sector;
            public byte[] DosType;
        }

        private class FfsRootBlock
        {
            public long Sector;
            public uint BlockSize;
            public uint BitmapBlockOffset;
            public string Name;
            public DateTime CreationDate;
        }

        private class Pfs3RootBlockCandidate
        {
            public long Sector;
            public Pfs3RootBlock RootBlock;
        }

        /// <summary>
        /// Scan disk for file systems.
        /// </summary>
        /// <param name="stream">Stream with disk.</param>
        /// <param name="diskSize">Size of disk in bytes.</param>
        /// <param name="bufferSize">Size of buffer used to read disk.</param>
        /// <param name="progress">Progress reporting bytes scanned.</param>
        /// <param name="messages">Messages for file systems found and skipped.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File systems found ordered by start sector. Overlapping file systems are resolved by keeping
        /// newest file system.</returns>
        public static async Task<IList<FoundFileSystem>> Scan(Stream stream, long diskSize, int bufferSize = 1024 * 1024,
            IProgress<long> progress = null, IList<RecoveryMessage> messages = null,
            CancellationToken cancellationToken = default)
        {
            messages = messages ?? new List<RecoveryMessage>();
            bufferSize = Math.Max(64 * 1024, bufferSize / SectorSize * SectorSize);

            var ffsBootBlocks = new List<FfsBootBlock>();
            var ffsRootBlocks = new List<FfsRootBlock>();
            var pfsIdSectors = new HashSet<long>();
            var pfs3RootBlocks = new List<Pfs3RootBlockCandidate>();
            var sfsFileSystems = new Dictionary<ulong, FoundFileSystem>();

            // buffer is larger than scanned area to read root blocks larger than sector size at end of scanned area
            var buffer = new byte[bufferSize + MaxBlockSize];

            for (long position = 0; position < diskSize; position += bufferSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var length = (int)Math.Min(buffer.Length, diskSize - position);
                stream.Seek(position, SeekOrigin.Begin);
                var available = await ReadFully(stream, buffer, length, cancellationToken);
                var scanLength = Math.Min(bufferSize, available);

                for (var offset = 0; offset + SectorSize <= scanLength; offset += SectorSize)
                {
                    var id = BigEndianConverter.ConvertBytesToUInt32(buffer, offset);
                    var sector = (position + offset) / SectorSize;

                    if ((id & 0xffffff00) == 0x444f5300 && (id & 0xff) <= 7)
                    {
                        // fast file system boot block "DOS\0" - "DOS\7"
                        ffsBootBlocks.Add(new FfsBootBlock
                        {
                            Sector = sector,
                            DosType = CopyBytes(buffer, offset, 4)
                        });
                    }
                    else if (id == FfsConstants.T_HEADER)
                    {
                        var ffsRootBlock = TryReadFfsRootBlock(buffer, offset, available, sector);
                        if (ffsRootBlock != null)
                        {
                            ffsRootBlocks.Add(ffsRootBlock);
                        }
                    }
                    else if (id == Pfs3Constants.ID_PFS_DISK || id == Pfs3Constants.ID_PFS2_DISK)
                    {
                        pfsIdSectors.Add(sector);
                        var rootBlock = TryReadPfs3RootBlock(buffer, offset);
                        if (rootBlock != null)
                        {
                            pfs3RootBlocks.Add(new Pfs3RootBlockCandidate { Sector = sector, RootBlock = rootBlock });
                        }
                    }
                    else if (id == SfsConstants.RootBlockId)
                    {
                        var sfsFileSystem = TryReadSfsRootBlock(buffer, offset, available, sector);
                        if (sfsFileSystem == null)
                        {
                            continue;
                        }

                        // root block is stored at start and end of partition, prefer root block at start of
                        // partition as reserved blocks can only be calculated from it
                        var firstByte = (ulong)sfsFileSystem.StartSector * SectorSize;
                        if (!sfsFileSystems.TryGetValue(firstByte, out var existing) ||
                            (existing.Reserved == 0 && sfsFileSystem.Reserved != 0))
                        {
                            sfsFileSystems[firstByte] = sfsFileSystem;
                        }
                    }
                }

                progress?.Report(Math.Min(diskSize, position + scanLength));
            }

            var candidates = new List<FoundFileSystem>();
            candidates.AddRange(PairFfsBlocks(ffsBootBlocks, ffsRootBlocks));
            candidates.AddRange(PairPfs3Blocks(pfsIdSectors, pfs3RootBlocks));
            candidates.AddRange(sfsFileSystems.Values.Select(x =>
            {
                if (x.Reserved == 0)
                {
                    x.Reserved = 2;
                }

                return x;
            }));

            return ResolveOverlaps(candidates, diskSize / SectorSize, messages);
        }

        private static FfsRootBlock TryReadFfsRootBlock(byte[] buffer, int offset, int available, long sector)
        {
            // header key, high seq and first data are 0 for root block
            if (BigEndianConverter.ConvertBytesToUInt32(buffer, offset + 0x4) != 0 ||
                BigEndianConverter.ConvertBytesToUInt32(buffer, offset + 0x8) != 0 ||
                BigEndianConverter.ConvertBytesToUInt32(buffer, offset + 0x10) != 0)
            {
                return null;
            }

            // block size is determined from hash table size: block size / 4 - 56
            var hashTableSize = BigEndianConverter.ConvertBytesToUInt32(buffer, offset + 0xc);
            if (hashTableSize == 0 || hashTableSize > MaxBlockSize / 4)
            {
                return null;
            }

            var blockSize = (hashTableSize + 56) * 4;
            if (!IsValidBlockSize(blockSize) || offset + blockSize > available)
            {
                return null;
            }

            if (BigEndianConverter.ConvertBytesToInt32(buffer, offset + (int)blockSize - 4) != FfsConstants.ST_ROOT)
            {
                return null;
            }

            var blockBytes = CopyBytes(buffer, offset, (int)blockSize);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            if (checksum != ChecksumHelper.CalculateChecksum(blockBytes, 0x14))
            {
                return null;
            }

            try
            {
                var rootBlock = FfsRootBlockParser.Parse(blockBytes);
                return new FfsRootBlock
                {
                    Sector = sector,
                    BlockSize = blockSize,
                    BitmapBlockOffset = rootBlock.BitmapBlockOffsets.FirstOrDefault(),
                    Name = rootBlock.Name,
                    CreationDate = rootBlock.FileSystemCreationDate
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Pfs3RootBlock TryReadPfs3RootBlock(byte[] buffer, int offset)
        {
            Pfs3RootBlock rootBlock;
            try
            {
                rootBlock = Pfs3RootBlockReader.Parse(CopyBytes(buffer, offset, SectorSize));
            }
            catch (Exception)
            {
                return null;
            }

            // boot block has same disk type as root block, but otherwise only contains zeroes
            if (!rootBlock.Options.HasFlag(Pfs3RootBlock.DiskOptionsEnum.MODE_HARDDISK) ||
                rootBlock.DiskSize == 0 ||
                (rootBlock.ReservedBlksize != 1024 && rootBlock.ReservedBlksize != 2048 &&
                 rootBlock.ReservedBlksize != 4096) ||
                rootBlock.FirstReserved < 2 ||
                rootBlock.LastReserved <= rootBlock.FirstReserved ||
                rootBlock.LastReserved >= rootBlock.DiskSize)
            {
                return null;
            }

            return rootBlock;
        }

        private static FoundFileSystem TryReadSfsRootBlock(byte[] buffer, int offset, int available, long sector)
        {
            var blockSize = BigEndianConverter.ConvertBytesToUInt32(buffer, offset + 0x34);
            if (!IsValidBlockSize(blockSize) || offset + blockSize > available)
            {
                return null;
            }

            var blockBytes = CopyBytes(buffer, offset, (int)blockSize);
            if (!SfsChecksumHelper.IsValid(blockBytes))
            {
                return null;
            }

            var rootBlock = SfsRootBlockParser.Parse(blockBytes);

            if (rootBlock.LastByte <= rootBlock.FirstByte ||
                rootBlock.FirstByte % SectorSize != 0 ||
                rootBlock.LastByte % SectorSize != 0 ||
                (rootBlock.LastByte - rootBlock.FirstByte) / rootBlock.BlockSize != rootBlock.TotalBlocks ||
                rootBlock.OwnBlock >= rootBlock.TotalBlocks ||
                rootBlock.FirstByte + (ulong)rootBlock.OwnBlock * rootBlock.BlockSize != (ulong)sector * SectorSize)
            {
                return null;
            }

            var startSector = (long)(rootBlock.FirstByte / SectorSize);
            var sectors = (long)((rootBlock.LastByte - rootBlock.FirstByte) / SectorSize);

            return new FoundFileSystem
            {
                Type = FileSystemType.SmartFileSystem,
                DosType = new byte[] { 0x53, 0x46, 0x53, 0x00 },
                StartSector = startSector,
                Sectors = sectors,
                MaxSectors = sectors,
                IsExactSize = true,
                RootBlockSector = sector,
                FileSystemBlockSize = rootBlock.BlockSize,
                // root block is next block after admin space container at first block after reserved
                Reserved = rootBlock.OwnBlock < rootBlock.TotalBlocks - 1 && rootBlock.OwnBlock >= 2
                    ? rootBlock.OwnBlock - 1
                    : 0,
                VolumeName = string.Empty,
                CreationDate = rootBlock.DateCreated
            };
        }

        /// <summary>
        /// Pair fast file system boot blocks with root blocks. Root block is located in the middle of partition,
        /// root key = (partition sectors / 2) / sectors per block.
        /// </summary>
        private static IEnumerable<FoundFileSystem> PairFfsBlocks(IList<FfsBootBlock> bootBlocks,
            IEnumerable<FfsRootBlock> rootBlocks)
        {
            foreach (var rootBlock in rootBlocks)
            {
                var sectorsPerBlock = rootBlock.BlockSize / SectorSize;

                // find nearest boot block before root block with root block offset within partition
                var bootBlock = bootBlocks
                    .Where(x => x.Sector < rootBlock.Sector &&
                                (rootBlock.Sector - x.Sector) % sectorsPerBlock == 0 &&
                                IsBitmapBlockInPartition(rootBlock, (rootBlock.Sector - x.Sector) / sectorsPerBlock))
                    .OrderByDescending(x => x.Sector)
                    .FirstOrDefault();

                if (bootBlock == null)
                {
                    continue;
                }

                var rootKey = (rootBlock.Sector - bootBlock.Sector) / sectorsPerBlock;
                var minSectors = 2 * rootKey * sectorsPerBlock;

                yield return new FoundFileSystem
                {
                    Type = FileSystemType.FastFileSystem,
                    DosType = bootBlock.DosType,
                    StartSector = bootBlock.Sector,
                    Sectors = minSectors,
                    MaxSectors = minSectors + 2 * sectorsPerBlock - 1,
                    IsExactSize = false,
                    RootBlockSector = rootBlock.Sector,
                    FileSystemBlockSize = rootBlock.BlockSize,
                    Reserved = 2,
                    VolumeName = rootBlock.Name,
                    CreationDate = rootBlock.CreationDate
                };
            }
        }

        private static bool IsBitmapBlockInPartition(FfsRootBlock rootBlock, long rootKey)
        {
            // bitmap block offset is relative to partition start and must be within partition
            return rootKey > 0 && (rootBlock.BitmapBlockOffset == 0 || rootBlock.BitmapBlockOffset < 2 * rootKey + 2);
        }

        /// <summary>
        /// Pair professional file system 3 root blocks with boot blocks located 2 blocks before root block.
        /// </summary>
        private static IEnumerable<FoundFileSystem> PairPfs3Blocks(ISet<long> pfsIdSectors,
            IEnumerable<Pfs3RootBlockCandidate> rootBlocks)
        {
            var rootBlockSectors = new HashSet<long>(rootBlocks.Select(x => x.Sector));

            foreach (var candidate in rootBlocks)
            {
                for (uint sectorsPerBlock = 1; sectorsPerBlock <= MaxBlockSize / SectorSize; sectorsPerBlock <<= 1)
                {
                    var bootBlockSector = candidate.Sector - Pfs3Constants.ROOTBLOCK * sectorsPerBlock;
                    if (bootBlockSector < 0)
                    {
                        break;
                    }

                    if (!pfsIdSectors.Contains(bootBlockSector) || rootBlockSectors.Contains(bootBlockSector))
                    {
                        continue;
                    }

                    var sectors = (long)candidate.RootBlock.DiskSize * sectorsPerBlock;

                    yield return new FoundFileSystem
                    {
                        Type = FileSystemType.ProfessionalFileSystem3,
                        DosType = new byte[] { 0x50, 0x46, 0x53, (byte)(candidate.RootBlock.DiskType & 0xff) },
                        StartSector = bootBlockSector,
                        Sectors = sectors,
                        MaxSectors = sectors,
                        IsExactSize = true,
                        RootBlockSector = candidate.Sector,
                        FileSystemBlockSize = sectorsPerBlock * SectorSize,
                        Reserved = 2,
                        VolumeName = candidate.RootBlock.DiskName,
                        CreationDate = candidate.RootBlock.CreationDate
                    };
                    break;
                }
            }
        }

        /// <summary>
        /// Resolve overlapping file systems found, which can be left overs from file systems formatted before
        /// disk was partitioned again. Newest file system is kept, exact size is preferred if same date.
        /// </summary>
        private static IList<FoundFileSystem> ResolveOverlaps(IEnumerable<FoundFileSystem> candidates, long diskSectors,
            IList<RecoveryMessage> messages)
        {
            var accepted = new List<FoundFileSystem>();

            var ordered = candidates
                .OrderByDescending(x => x.CreationDate)
                .ThenByDescending(x => x.IsExactSize)
                .ThenBy(x => x.StartSector)
                .ToList();

            foreach (var candidate in ordered)
            {
                if (candidate.StartSector + candidate.Sectors > diskSectors)
                {
                    messages.Add(new RecoveryMessage(RecoveryMessageLevel.Warning,
                        $"Found {candidate}, which exceeds disk size of {diskSectors} sectors"));
                }

                var overlapping = accepted.FirstOrDefault(x =>
                    candidate.StartSector < x.StartSector + x.Sectors &&
                    x.StartSector < candidate.StartSector + candidate.Sectors);

                if (overlapping != null)
                {
                    messages.Add(new RecoveryMessage(RecoveryMessageLevel.Warning,
                        $"Skipped {candidate}, which overlaps newer {overlapping}"));
                    continue;
                }

                accepted.Add(candidate);
            }

            var fileSystems = accepted.OrderBy(x => x.StartSector).ToList();
            foreach (var fileSystem in fileSystems)
            {
                messages.Add(new RecoveryMessage(RecoveryMessageLevel.Info, $"Found {fileSystem}"));
            }

            return fileSystems;
        }

        private static bool IsValidBlockSize(uint blockSize) =>
            blockSize >= MinBlockSize && blockSize <= MaxBlockSize && (blockSize & (blockSize - 1)) == 0;

        private static byte[] CopyBytes(byte[] buffer, int offset, int length)
        {
            var bytes = new byte[length];
            Array.Copy(buffer, offset, bytes, 0, length);
            return bytes;
        }

        internal static async Task<int> ReadFully(Stream stream, byte[] buffer, int length,
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

            return total;
        }
    }
}
