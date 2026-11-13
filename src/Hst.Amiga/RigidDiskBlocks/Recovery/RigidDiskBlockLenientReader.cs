namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using Extensions;
    using FileSystems;

    /// <summary>
    /// Rigid disk block blocks read leniently.
    /// </summary>
    public class LenientRigidDiskBlock
    {
        /// <summary>
        /// Rigid disk block found. Null, if not found.
        /// </summary>
        public RigidDiskBlock RigidDiskBlock { get; set; }

        /// <summary>
        /// Sector rigid disk block was found at.
        /// </summary>
        public long RigidDiskBlockSector { get; set; } = -1;

        /// <summary>
        /// Partition blocks found, linked partition blocks first followed by partition blocks not linked.
        /// </summary>
        public IList<PartitionBlock> PartitionBlocks { get; } = new List<PartitionBlock>();

        /// <summary>
        /// File system header blocks with valid load seg blocks found.
        /// </summary>
        public IList<FileSystemHeaderBlock> FileSystemHeaderBlocks { get; } = new List<FileSystemHeaderBlock>();

        /// <summary>
        /// Bad blocks linked from rigid disk block.
        /// </summary>
        public IList<BadBlock> BadBlocks { get; } = new List<BadBlock>();

        /// <summary>
        /// Sectors with rigid disk block blocks (RDSK, PART, FSHD, LSEG, BADB).
        /// </summary>
        public ISet<long> BlockSectors { get; } = new HashSet<long>();
    }

    /// <summary>
    /// Reads rigid disk block leniently by ignoring checksums and scanning rigid disk block area for blocks not
    /// linked. Used to recover rigid disk blocks.
    /// </summary>
    public static class RigidDiskBlockLenientReader
    {
        private const int SectorSize = 512;
        private const int RigidDiskBlockLocationLimit = 16;
        private const long DefaultRdbAreaSectors = 2016;
        private const long MaxRdbAreaSectors = 65536;

        private class BlockEntry<T>
        {
            public long Sector;
            public T Block;
            public bool ChecksumValid;
        }

        /// <summary>
        /// Read rigid disk block leniently.
        /// </summary>
        /// <param name="stream">Stream with disk.</param>
        /// <param name="diskSectors">Size of disk in sectors.</param>
        /// <param name="firstFileSystemSector">Start sector of first file system found, limits rigid disk block area
        /// scanned for blocks.</param>
        /// <param name="result">Result to add messages to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task<LenientRigidDiskBlock> Read(Stream stream, long diskSectors,
            long? firstFileSystemSector, RecoveryResult result, CancellationToken cancellationToken = default)
        {
            var lenient = new LenientRigidDiskBlock();

            // read rigid disk block from one of the first 16 sectors
            for (var sector = 0; sector < RigidDiskBlockLocationLimit && sector < diskSectors; sector++)
            {
                var blockBytes = await ReadSectors(stream, sector, 1, cancellationToken);
                if (BitConverter.ToUInt32(blockBytes, 0) != BlockIdentifiers.RigidDiskBlock)
                {
                    continue;
                }

                var checksumValid = IsChecksumValid(blockBytes, BlockSize.RigidDiskBlock, out var safeBytes);
                try
                {
                    lenient.RigidDiskBlock = await RigidDiskBlockReader.Parse(safeBytes, true);
                    lenient.RigidDiskBlockSector = sector;
                    lenient.BlockSectors.Add(sector);
                    result.Info($"Found rigid disk block at sector {sector}");
                    if (!checksumValid)
                    {
                        result.Warning($"Rigid disk block at sector {sector} has invalid checksum");
                    }

                    break;
                }
                catch (Exception e)
                {
                    result.Warning($"Failed to parse rigid disk block at sector {sector}: {e.Message}");
                }
            }

            var rigidDiskBlock = lenient.RigidDiskBlock;
            if (rigidDiskBlock == null)
            {
                result.Warning("No rigid disk block found");
            }

            // determine end of rigid disk block area to scan for blocks
            long areaEnd = DefaultRdbAreaSectors;
            if (rigidDiskBlock != null)
            {
                if (rigidDiskBlock.RdbBlockHi >= rigidDiskBlock.RdbBlockLo && rigidDiskBlock.RdbBlockHi < diskSectors &&
                    rigidDiskBlock.RdbBlockHi < MaxRdbAreaSectors)
                {
                    areaEnd = rigidDiskBlock.RdbBlockHi + 1L;
                }
                else if (rigidDiskBlock.LoCylinder > 0 && rigidDiskBlock.CylBlocks > 0 &&
                         (long)rigidDiskBlock.LoCylinder * rigidDiskBlock.CylBlocks < MaxRdbAreaSectors)
                {
                    areaEnd = (long)rigidDiskBlock.LoCylinder * rigidDiskBlock.CylBlocks;
                }
            }

            if (firstFileSystemSector.HasValue && firstFileSystemSector.Value < areaEnd)
            {
                areaEnd = firstFileSystemSector.Value;
            }

            areaEnd = Math.Min(areaEnd, diskSectors);

            // scan rigid disk block area for blocks
            var partitionBlocks = new Dictionary<long, BlockEntry<PartitionBlock>>();
            var fileSystemHeaderBlocks = new Dictionary<long, BlockEntry<FileSystemHeaderBlock>>();
            var loadSegBlocks = new Dictionary<long, BlockEntry<LoadSegBlock>>();
            var badBlocks = new Dictionary<long, BlockEntry<BadBlock>>();

            var areaBytes = areaEnd > 0 ? await ReadSectors(stream, 0, (int)areaEnd, cancellationToken) : new byte[0];
            for (long sector = 0; sector < areaEnd; sector++)
            {
                var offset = (int)(sector * SectorSize);
                var identifier = BitConverter.ToUInt32(areaBytes, offset);
                var blockBytes = new byte[SectorSize];
                Array.Copy(areaBytes, offset, blockBytes, 0, SectorSize);

                try
                {
                    switch (identifier)
                    {
                        case BlockIdentifiers.RigidDiskBlock:
                            lenient.BlockSectors.Add(sector);
                            break;
                        case BlockIdentifiers.PartitionBlock:
                        {
                            lenient.BlockSectors.Add(sector);
                            var valid = IsChecksumValid(blockBytes, BlockSize.PartitionBlock, out var safeBytes);
                            partitionBlocks[sector] = new BlockEntry<PartitionBlock>
                            {
                                Sector = sector, ChecksumValid = valid,
                                Block = await PartitionBlockReader.Parse(safeBytes, SectorSize, true)
                            };
                            break;
                        }
                        case BlockIdentifiers.FileSystemHeaderBlock:
                        {
                            lenient.BlockSectors.Add(sector);
                            var valid = IsChecksumValid(blockBytes, BlockSize.FileSystemHeaderBlock,
                                out var safeBytes);
                            fileSystemHeaderBlocks[sector] = new BlockEntry<FileSystemHeaderBlock>
                            {
                                Sector = sector, ChecksumValid = valid,
                                Block = await FileSystemHeaderBlockReader.Parse(safeBytes, true)
                            };
                            break;
                        }
                        case BlockIdentifiers.LoadSegBlock:
                        {
                            lenient.BlockSectors.Add(sector);
                            var valid = IsChecksumValid(blockBytes, SectorSize / 4, out var safeBytes);
                            loadSegBlocks[sector] = new BlockEntry<LoadSegBlock>
                            {
                                Sector = sector, ChecksumValid = valid,
                                Block = await LoadSegBlockReader.Parse(safeBytes, true)
                            };
                            break;
                        }
                        case BlockIdentifiers.BadBlock:
                        {
                            lenient.BlockSectors.Add(sector);
                            var valid = IsChecksumValid(blockBytes, SectorSize / 4, out var safeBytes);
                            badBlocks[sector] = new BlockEntry<BadBlock>
                            {
                                Sector = sector, ChecksumValid = valid,
                                Block = await BadBlockReader.Parse(safeBytes, true)
                            };
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    result.Warning($"Failed to parse block at sector {sector}: {e.Message}");
                }
            }

            ReadPartitionBlocks(lenient, rigidDiskBlock, partitionBlocks, result);
            ReadFileSystemHeaderBlocks(lenient, rigidDiskBlock, fileSystemHeaderBlocks, loadSegBlocks, result);
            ReadBadBlocks(lenient, rigidDiskBlock, badBlocks, result);

            return lenient;
        }

        private static void ReadPartitionBlocks(LenientRigidDiskBlock lenient, RigidDiskBlock rigidDiskBlock,
            IDictionary<long, BlockEntry<PartitionBlock>> partitionBlocks, RecoveryResult result)
        {
            var linked = FollowChain(rigidDiskBlock?.PartitionList, partitionBlocks,
                x => x.Block.NextPartitionBlock);

            if (rigidDiskBlock != null && rigidDiskBlock.PartitionList != BlockIdentifiers.EndOfBlock &&
                linked.Count < partitionBlocks.Count)
            {
                result.Warning("Partition block list is broken or incomplete");
            }

            var entries = linked.Concat(partitionBlocks.Values.Where(x => !linked.Contains(x))
                .OrderBy(x => x.Sector)).ToList();

            foreach (var entry in entries)
            {
                var partitionBlock = entry.Block;
                var isLinked = linked.Contains(entry);

                // skip duplicate partition blocks
                if (lenient.PartitionBlocks.Any(x => x.DriveName == partitionBlock.DriveName &&
                                                     x.LowCyl == partitionBlock.LowCyl &&
                                                     x.HighCyl == partitionBlock.HighCyl))
                {
                    result.Info(
                        $"Skipped duplicate partition block '{partitionBlock.DriveName}' at sector {entry.Sector}");
                    continue;
                }

                if (!entry.ChecksumValid)
                {
                    result.Warning(
                        $"Partition block '{partitionBlock.DriveName}' at sector {entry.Sector} has invalid checksum");
                }

                if (!isLinked)
                {
                    result.Warning(
                        $"Partition block '{partitionBlock.DriveName}' at sector {entry.Sector} is not linked from rigid disk block");
                }

                lenient.PartitionBlocks.Add(partitionBlock);
            }
        }

        private static void ReadFileSystemHeaderBlocks(LenientRigidDiskBlock lenient, RigidDiskBlock rigidDiskBlock,
            IDictionary<long, BlockEntry<FileSystemHeaderBlock>> fileSystemHeaderBlocks,
            IDictionary<long, BlockEntry<LoadSegBlock>> loadSegBlocks, RecoveryResult result)
        {
            var linked = FollowChain(rigidDiskBlock?.FileSysHdrList, fileSystemHeaderBlocks,
                x => x.Block.NextFileSysHeaderBlock);

            if (rigidDiskBlock != null && rigidDiskBlock.FileSysHdrList != BlockIdentifiers.EndOfBlock &&
                linked.Count < fileSystemHeaderBlocks.Count)
            {
                result.Warning("File system header block list is broken or incomplete");
            }

            var entries = linked.Concat(fileSystemHeaderBlocks.Values.Where(x => !linked.Contains(x))
                .OrderBy(x => x.Sector)).ToList();

            foreach (var entry in entries)
            {
                var fileSystemHeaderBlock = entry.Block;
                var dosType = fileSystemHeaderBlock.DosType.FormatDosType();

                if (lenient.FileSystemHeaderBlocks.Any(x => x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)))
                {
                    result.Info(
                        $"Skipped duplicate file system header block with dos type '{dosType}' at sector {entry.Sector}");
                    continue;
                }

                // follow load seg block chain
                var loadSegChain = new List<LoadSegBlock>();
                var visited = new HashSet<long>();
                long next = fileSystemHeaderBlock.SegListBlocks;
                string error = null;
                while (next >= 0)
                {
                    if (!visited.Add(next) || !loadSegBlocks.TryGetValue(next, out var loadSegEntry))
                    {
                        error = $"load seg block at sector {next} is missing";
                        break;
                    }

                    if (!loadSegEntry.ChecksumValid)
                    {
                        error = $"load seg block at sector {next} has invalid checksum";
                        break;
                    }

                    loadSegChain.Add(loadSegEntry.Block);
                    next = loadSegEntry.Block.NextLoadSegBlock;
                }

                if (error != null || loadSegChain.Count == 0)
                {
                    result.Warning(
                        $"Skipped file system with dos type '{dosType}' at sector {entry.Sector}, {error ?? "no load seg blocks"}");
                    continue;
                }

                if (!entry.ChecksumValid)
                {
                    result.Warning(
                        $"File system header block with dos type '{dosType}' at sector {entry.Sector} has invalid checksum");
                }

                fileSystemHeaderBlock.LoadSegBlocks = loadSegChain;
                fileSystemHeaderBlock.FileSystemSize = loadSegChain.Sum(x => x.Data.Length);
                lenient.FileSystemHeaderBlocks.Add(fileSystemHeaderBlock);
                result.Info(
                    $"Found file system with dos type '{dosType}' version {fileSystemHeaderBlock.VersionFormatted} at sector {entry.Sector}");
            }
        }

        private static void ReadBadBlocks(LenientRigidDiskBlock lenient, RigidDiskBlock rigidDiskBlock,
            IDictionary<long, BlockEntry<BadBlock>> badBlocks, RecoveryResult result)
        {
            if (rigidDiskBlock == null || rigidDiskBlock.BadBlockList == BlockIdentifiers.EndOfBlock)
            {
                return;
            }

            var linked = FollowChain(rigidDiskBlock.BadBlockList, badBlocks, x => x.Block.NextBadBlock);
            if (linked.Count == 0 || linked.Any(x => !x.ChecksumValid) ||
                linked.Last().Block.NextBadBlock != BlockIdentifiers.EndOfBlock)
            {
                result.Warning("Skipped bad block list, which is broken or has invalid checksums");
                return;
            }

            foreach (var entry in linked)
            {
                lenient.BadBlocks.Add(entry.Block);
            }
        }

        /// <summary>
        /// Follow chain of linked blocks from first block. Stops at end of block, missing block or loop.
        /// </summary>
        private static List<BlockEntry<T>> FollowChain<T>(uint? first, IDictionary<long, BlockEntry<T>> blocks,
            Func<BlockEntry<T>, uint> next)
        {
            var chain = new List<BlockEntry<T>>();
            if (!first.HasValue)
            {
                return chain;
            }

            var visited = new HashSet<long>();
            var sector = first.Value;
            while (sector != BlockIdentifiers.EndOfBlock && visited.Add(sector) &&
                   blocks.TryGetValue(sector, out var entry))
            {
                chain.Add(entry);
                sector = next(entry);
            }

            return chain;
        }

        /// <summary>
        /// Check if checksum of block is valid. Returns block bytes safe for parsing with size field corrected,
        /// if size field is invalid.
        /// </summary>
        private static bool IsChecksumValid(byte[] blockBytes, int defaultSize, out byte[] safeBytes)
        {
            var size = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4);
            safeBytes = blockBytes;

            if (size < 5 || size * 4 > blockBytes.Length)
            {
                safeBytes = new byte[blockBytes.Length];
                Array.Copy(blockBytes, safeBytes, blockBytes.Length);
                BigEndianConverter.ConvertUInt32ToBytes((uint)defaultSize, safeBytes, 4);
                return false;
            }

            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 8);
            return checksum == ChecksumHelper.CalculateChecksum(blockBytes, 8, (int)size * 4);
        }

        private static async Task<byte[]> ReadSectors(Stream stream, long sector, int sectors,
            CancellationToken cancellationToken)
        {
            var bytes = new byte[sectors * SectorSize];
            stream.Seek(sector * SectorSize, SeekOrigin.Begin);
            await FileSystemScanner.ReadFully(stream, bytes, bytes.Length, cancellationToken);
            return bytes;
        }
    }
}
