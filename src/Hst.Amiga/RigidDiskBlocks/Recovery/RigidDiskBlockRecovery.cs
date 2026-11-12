namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;

    /// <summary>
    /// Recovers broken rigid disk block by either fixing existing rigid disk block or rebuilding a new rigid disk
    /// block from disk size and file systems found by scanning disk for known file system boot and root blocks.
    /// </summary>
    public static class RigidDiskBlockRecovery
    {
        private const int SectorSize = 512;

        private class PartitionEntry
        {
            public PartitionBlock PartitionBlock;
            public FoundFileSystem FileSystem;
            public long StartSector;
            public long EndSector;
        }

        /// <summary>
        /// Analyze disk and recover rigid disk block without writing any changes to disk.
        /// </summary>
        /// <param name="stream">Stream with disk.</param>
        /// <param name="options">Recovery options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result with recovered rigid disk block, file systems found and messages.</returns>
        public static async Task<RecoveryResult> Analyze(Stream stream, RecoveryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var analysis = await AnalyzeDisk(stream, options ?? new RecoveryOptions(), cancellationToken);
            return analysis.Result;
        }

        /// <summary>
        /// Recover rigid disk block and write it to disk, if rigid disk block was recovered without errors and
        /// differs from rigid disk block on disk.
        /// </summary>
        /// <param name="stream">Stream with disk.</param>
        /// <param name="options">Recovery options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result with recovered rigid disk block, file systems found and messages.</returns>
        public static async Task<RecoveryResult> Repair(Stream stream, RecoveryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var analysis = await AnalyzeDisk(stream, options ?? new RecoveryOptions(), cancellationToken);
            var result = analysis.Result;

            if (!result.IsValid)
            {
                result.Error("Rigid disk block is not written, as it could not be recovered without errors");
                return result;
            }

            if (!result.Modified)
            {
                result.Info("Rigid disk block is valid, no changes written");
                return result;
            }

            if (!stream.CanWrite)
            {
                result.Error("Rigid disk block is not written, stream is not writable");
                return result;
            }

            foreach (var sector in analysis.ChangedSectors)
            {
                stream.Seek(sector * SectorSize, SeekOrigin.Begin);
                await stream.WriteAsync(analysis.RdbAreaBytes, (int)(sector * SectorSize), SectorSize,
                    cancellationToken);
            }

            await stream.FlushAsync(cancellationToken);

            result.Written = true;
            result.Info($"Rigid disk block written, {analysis.ChangedSectors.Count} sectors updated");

            return result;
        }

        private class Analysis
        {
            public RecoveryResult Result;
            public byte[] RdbAreaBytes;
            public IList<long> ChangedSectors = new List<long>();
        }

        private static async Task<Analysis> AnalyzeDisk(Stream stream, RecoveryOptions options,
            CancellationToken cancellationToken)
        {
            var result = new RecoveryResult();
            var analysis = new Analysis { Result = result };

            var diskSize = options.DiskSize > 0 ? options.DiskSize : stream.Length;
            var diskSectors = diskSize / SectorSize;

            if (diskSectors == 0)
            {
                result.Error("Disk is empty");
                return analysis;
            }

            // scan disk for file systems
            var scanMessages = new List<RecoveryMessage>();
            result.FileSystems = await FileSystemScanner.Scan(stream, diskSectors * SectorSize, options.ScanBufferSize,
                options.Progress, scanMessages, options.SkipFloppyImages, cancellationToken);
            foreach (var message in scanMessages)
            {
                result.Messages.Add(message);
            }

            // read rigid disk block leniently
            var firstFileSystemSector = result.FileSystems.Count > 0
                ? result.FileSystems.Min(x => x.StartSector)
                : (long?)null;
            var lenient = await RigidDiskBlockLenientReader.Read(stream, diskSectors, firstFileSystemSector, result,
                cancellationToken);

            var mode = options.Mode == RecoveryMode.Auto
                ? lenient.RigidDiskBlock != null ? RecoveryMode.Fix : RecoveryMode.Rebuild
                : options.Mode;
            result.Mode = mode;

            if (mode == RecoveryMode.Fix && lenient.RigidDiskBlock == null)
            {
                result.Error("Rigid disk block can't be fixed as no rigid disk block was found, use rebuild instead");
                return analysis;
            }

            result.Info(mode == RecoveryMode.Fix ? "Fixing rigid disk block" : "Rebuilding rigid disk block");

            // resolve geometry
            var preferredGeometries = new List<(uint Heads, uint Sectors)>();
            if (options.Heads > 0 && options.Sectors > 0)
            {
                preferredGeometries.Add((options.Heads, options.Sectors));
            }

            if (lenient.RigidDiskBlock != null && lenient.RigidDiskBlock.BlockSize == SectorSize)
            {
                preferredGeometries.Add((lenient.RigidDiskBlock.Heads, lenient.RigidDiskBlock.Sectors));
            }

            preferredGeometries.AddRange(lenient.PartitionBlocks.Select(x => (x.Surfaces, x.BlocksPerTrack)));

            var geometry = GeometryResolver.Resolve(result.FileSystems, preferredGeometries, result);
            var cylinderBlocks = geometry.Heads * geometry.Sectors;
            GeometryResolver.Snap(result.FileSystems, cylinderBlocks, result);
            result.Info($"Using geometry with {geometry.Heads} heads and {geometry.Sectors} sectors per track");

            // build partitions from existing partition blocks and file systems found
            var partitionEntries = BuildPartitionEntries(mode, lenient, result.FileSystems, diskSectors, result);

            var rigidDiskBlock = mode == RecoveryMode.Fix
                ? lenient.RigidDiskBlock
                : new RigidDiskBlock { RdbBlockLo = 0 };

            if (mode == RecoveryMode.Fix && rigidDiskBlock.RdbBlockLo != lenient.RigidDiskBlockSector)
            {
                result.Warning(
                    $"Rigid disk block RdbBlockLo {rigidDiskBlock.RdbBlockLo} -> {lenient.RigidDiskBlockSector}, sector rigid disk block was found at");
                rigidDiskBlock.RdbBlockLo = (uint)lenient.RigidDiskBlockSector;
            }

            var partitionBlocks = BuildPartitionBlocks(mode, partitionEntries, geometry, options, result);

            if (!SetGeometry(mode, rigidDiskBlock, geometry, diskSectors, partitionBlocks, result))
            {
                return analysis;
            }

            rigidDiskBlock.PartitionBlocks = partitionBlocks;
            rigidDiskBlock.FileSystemHeaderBlocks = lenient.FileSystemHeaderBlocks.ToList();
            rigidDiskBlock.BadBlocks = mode == RecoveryMode.Fix
                ? lenient.BadBlocks.ToList()
                : Enumerable.Empty<BadBlock>();

            if (!SetRdbArea(rigidDiskBlock, partitionBlocks, result))
            {
                return analysis;
            }

            foreach (var partitionBlock in partitionBlocks)
            {
                if (partitionBlock.DosType.Take(3).SequenceEqual(new byte[] { 0x44, 0x4f, 0x53 }))
                {
                    continue;
                }

                if (rigidDiskBlock.FileSystemHeaderBlocks.All(x => !x.DosType.SequenceEqual(partitionBlock.DosType)))
                {
                    result.Warning(
                        $"Partition '{partitionBlock.DriveName}' uses dos type '{partitionBlock.DosTypeFormatted}', which has no file system in rigid disk block");
                }
            }

            result.RigidDiskBlock = rigidDiskBlock;

            // build rigid disk block area and compare with disk to detect changes
            await BuildRdbArea(stream, analysis, lenient, cancellationToken);
            result.Modified = analysis.ChangedSectors.Count > 0;

            return analysis;
        }

        private static IList<PartitionEntry> BuildPartitionEntries(RecoveryMode mode, LenientRigidDiskBlock lenient,
            IList<FoundFileSystem> fileSystems, long diskSectors, RecoveryResult result)
        {
            var existing = new List<PartitionEntry>();
            foreach (var partitionBlock in lenient.PartitionBlocks)
            {
                var cylinderBlocks = (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack;
                if (cylinderBlocks == 0 || partitionBlock.HighCyl < partitionBlock.LowCyl)
                {
                    result.Warning(
                        $"Skipped partition '{partitionBlock.DriveName}' with invalid geometry, low cylinder {partitionBlock.LowCyl}, high cylinder {partitionBlock.HighCyl}, {partitionBlock.Surfaces} surfaces and {partitionBlock.BlocksPerTrack} blocks per track");
                    continue;
                }

                existing.Add(new PartitionEntry
                {
                    PartitionBlock = partitionBlock,
                    StartSector = partitionBlock.LowCyl * cylinderBlocks,
                    EndSector = (partitionBlock.HighCyl + 1L) * cylinderBlocks
                });
            }

            var entries = new List<PartitionEntry>();
            var matched = new HashSet<PartitionEntry>();

            // match file systems found with existing partitions
            foreach (var fileSystem in fileSystems)
            {
                var match = existing
                    .Where(x => !matched.Contains(x) && x.StartSector < fileSystem.EndSector &&
                                fileSystem.StartSector < x.EndSector)
                    .OrderByDescending(x => x.StartSector == fileSystem.StartSector)
                    .ThenByDescending(x =>
                        Math.Min(x.EndSector, fileSystem.EndSector) - Math.Max(x.StartSector, fileSystem.StartSector))
                    .FirstOrDefault();

                if (match != null)
                {
                    matched.Add(match);
                }

                entries.Add(new PartitionEntry
                {
                    PartitionBlock = match?.PartitionBlock,
                    FileSystem = fileSystem,
                    StartSector = fileSystem.StartSector,
                    EndSector = fileSystem.EndSector
                });
            }

            // existing partitions without file systems found
            foreach (var entry in existing.Where(x => !matched.Contains(x)))
            {
                var name = entry.PartitionBlock.DriveName;
                if (mode == RecoveryMode.Rebuild)
                {
                    result.Warning($"Skipped partition '{name}', no file system found");
                    continue;
                }

                var overlapping = entries.FirstOrDefault(x =>
                    entry.StartSector < x.EndSector && x.StartSector < entry.EndSector);
                if (overlapping != null)
                {
                    result.Warning(
                        $"Skipped partition '{name}', which overlaps partition with {overlapping.FileSystem}");
                    continue;
                }

                if (entry.EndSector > diskSectors)
                {
                    result.Warning($"Partition '{name}' exceeds disk size");
                }

                result.Warning($"Partition '{name}' has no file system found, partition is kept as is");
                entries.Add(entry);
            }

            return entries.OrderBy(x => x.StartSector).ToList();
        }

        private static IList<PartitionBlock> BuildPartitionBlocks(RecoveryMode mode, IList<PartitionEntry> entries,
            (uint Heads, uint Sectors) geometry, RecoveryOptions options, RecoveryResult result)
        {
            var partitionBlocks = new List<PartitionBlock>();
            var cylinderBlocks = (long)geometry.Heads * geometry.Sectors;
            var driveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var isNew = entry.PartitionBlock == null;
                var partitionBlock = entry.PartitionBlock ?? new PartitionBlock();
                var name = string.IsNullOrWhiteSpace(partitionBlock.DriveName) || driveNames.Contains(partitionBlock.DriveName)
                    ? null
                    : partitionBlock.DriveName;

                if (name == null)
                {
                    name = NextDriveName(options.DriveNamePrefix, driveNames, entries);
                    if (!isNew)
                    {
                        result.Warning($"Partition '{partitionBlock.DriveName}' renamed to '{name}'");
                    }
                }

                partitionBlock.DriveName = name;
                driveNames.Add(name);

                var startDivisible = entry.StartSector % cylinderBlocks == 0;
                var endDivisible = entry.EndSector % cylinderBlocks == 0;

                if (startDivisible && endDivisible)
                {
                    SetPartitionValue(partitionBlock, isNew, "surfaces", partitionBlock.Surfaces, geometry.Heads,
                        x => partitionBlock.Surfaces = x, result);
                    SetPartitionValue(partitionBlock, isNew, "blocks per track", partitionBlock.BlocksPerTrack,
                        geometry.Sectors, x => partitionBlock.BlocksPerTrack = x, result);
                    SetPartitionValue(partitionBlock, isNew, "low cylinder", partitionBlock.LowCyl,
                        (uint)(entry.StartSector / cylinderBlocks), x => partitionBlock.LowCyl = x, result);
                    SetPartitionValue(partitionBlock, isNew, "high cylinder", partitionBlock.HighCyl,
                        (uint)(entry.EndSector / cylinderBlocks - 1), x => partitionBlock.HighCyl = x, result);
                }
                else if (entry.FileSystem != null)
                {
                    // file system doesn't fit cylinder boundaries, use partition geometry with 1 head and
                    // a track per partition
                    result.Warning(
                        $"Partition '{name}' doesn't start or end at a cylinder boundary, partition uses own geometry");
                    var sectors = entry.EndSector - entry.StartSector;
                    var blocksPerTrack = Gcd(entry.StartSector, sectors);
                    SetPartitionValue(partitionBlock, isNew, "surfaces", partitionBlock.Surfaces, 1,
                        x => partitionBlock.Surfaces = x, result);
                    SetPartitionValue(partitionBlock, isNew, "blocks per track", partitionBlock.BlocksPerTrack,
                        (uint)blocksPerTrack, x => partitionBlock.BlocksPerTrack = x, result);
                    SetPartitionValue(partitionBlock, isNew, "low cylinder", partitionBlock.LowCyl,
                        (uint)(entry.StartSector / blocksPerTrack), x => partitionBlock.LowCyl = x, result);
                    SetPartitionValue(partitionBlock, isNew, "high cylinder", partitionBlock.HighCyl,
                        (uint)(entry.EndSector / blocksPerTrack - 1), x => partitionBlock.HighCyl = x, result);
                }
                else
                {
                    result.Warning($"Partition '{name}' uses geometry different from rigid disk block");
                }

                if (entry.FileSystem != null)
                {
                    ApplyFileSystem(partitionBlock, isNew, entry.FileSystem, options, result);
                }

                partitionBlock.BlockSize = SectorSize;
                partitionBlock.PartitionSize = (long)(partitionBlock.HighCyl - partitionBlock.LowCyl + 1) *
                                               partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * SectorSize;

                if (isNew)
                {
                    result.Info(
                        $"Added partition '{name}' with dos type '{partitionBlock.DosTypeFormatted}', low cylinder {partitionBlock.LowCyl} and high cylinder {partitionBlock.HighCyl} from {entry.FileSystem}");
                }

                partitionBlocks.Add(partitionBlock);
            }

            // set first partition bootable, if rebuilt and no partitions are bootable
            if (mode == RecoveryMode.Rebuild && partitionBlocks.Count > 0 && partitionBlocks.All(x => !x.Bootable))
            {
                partitionBlocks[0].Flags |= (uint)PartitionBlock.PartitionFlagsEnum.Bootable;
                result.Info($"Partition '{partitionBlocks[0].DriveName}' set bootable");
            }

            return partitionBlocks;
        }

        private static void ApplyFileSystem(PartitionBlock partitionBlock, bool isNew, FoundFileSystem fileSystem,
            RecoveryOptions options, RecoveryResult result)
        {
            var name = partitionBlock.DriveName;
            byte[] dosType;
            switch (fileSystem.Type)
            {
                case FileSystemType.ProfessionalFileSystem3:
                    dosType = IsPfs3DosType(partitionBlock.DosType)
                        ? partitionBlock.DosType
                        : ParseDosType(options.Pfs3DosType);
                    break;
                case FileSystemType.SmartFileSystem:
                    dosType = IsDosType(partitionBlock.DosType, "SFS")
                        ? partitionBlock.DosType
                        : ParseDosType(options.SfsDosType);
                    break;
                default:
                    dosType = fileSystem.DosType;
                    break;
            }

            if (isNew || partitionBlock.DosType == null || !partitionBlock.DosType.SequenceEqual(dosType))
            {
                if (!isNew)
                {
                    result.Warning(
                        $"Partition '{name}' dos type '{FormatDosType(partitionBlock.DosType)}' -> '{dosType.FormatDosType()}' from {fileSystem.Type}");
                }

                partitionBlock.DosType = dosType;
            }

            var sectorsPerBlock = Math.Max(1U, fileSystem.FileSystemBlockSize / SectorSize);
            SetPartitionValue(partitionBlock, isNew, "size block", partitionBlock.SizeBlock, SectorSize / 4,
                x => partitionBlock.SizeBlock = x, result);
            SetPartitionValue(partitionBlock, isNew, "sectors per block", partitionBlock.Sectors, sectorsPerBlock,
                x => partitionBlock.Sectors = x, result);
            partitionBlock.FileSystemBlockSize = sectorsPerBlock * SectorSize;

            if (fileSystem.Type == FileSystemType.SmartFileSystem || partitionBlock.Reserved == 0)
            {
                SetPartitionValue(partitionBlock, isNew, "reserved", partitionBlock.Reserved, fileSystem.Reserved,
                    x => partitionBlock.Reserved = x, result);
            }
        }

        private static void SetPartitionValue(PartitionBlock partitionBlock, bool isNew, string field, uint oldValue,
            uint newValue, Action<uint> set, RecoveryResult result)
        {
            if (oldValue == newValue)
            {
                return;
            }

            if (!isNew)
            {
                result.Warning($"Partition '{partitionBlock.DriveName}' {field} {oldValue} -> {newValue}");
            }

            set(newValue);
        }

        private static bool SetGeometry(RecoveryMode mode, RigidDiskBlock rigidDiskBlock,
            (uint Heads, uint Sectors) geometry, long diskSectors, IList<PartitionBlock> partitionBlocks,
            RecoveryResult result)
        {
            var cylinderBlocks = geometry.Heads * geometry.Sectors;
            var cylinders = (uint)(diskSectors / cylinderBlocks);

            // partitions using rigid disk block geometry
            var lastHighCyl = partitionBlocks
                .Where(x => x.Surfaces * x.BlocksPerTrack == cylinderBlocks)
                .Select(x => (long)x.HighCyl)
                .DefaultIfEmpty(-1)
                .Max();
            if (lastHighCyl >= cylinders)
            {
                result.Warning(
                    $"Partitions exceeds disk size of {cylinders} cylinders, rigid disk block is set to {lastHighCyl + 1} cylinders");
                cylinders = (uint)(lastHighCyl + 1);
            }

            if (cylinders == 0)
            {
                result.Error("Disk is smaller than one cylinder");
                return false;
            }

            var fix = mode == RecoveryMode.Fix;
            SetRdbValue(fix, "block size", rigidDiskBlock.BlockSize, SectorSize, x => rigidDiskBlock.BlockSize = x,
                result);
            SetRdbValue(fix, "heads", rigidDiskBlock.Heads, geometry.Heads, x => rigidDiskBlock.Heads = x, result);
            SetRdbValue(fix, "sectors", rigidDiskBlock.Sectors, geometry.Sectors, x => rigidDiskBlock.Sectors = x,
                result);
            SetRdbValue(fix, "cylinder blocks", rigidDiskBlock.CylBlocks, cylinderBlocks,
                x => rigidDiskBlock.CylBlocks = x, result);
            SetRdbValue(fix, "cylinders", rigidDiskBlock.Cylinders, cylinders, x => rigidDiskBlock.Cylinders = x,
                result);
            SetRdbValue(fix, "high cylinder", rigidDiskBlock.HiCylinder, cylinders - 1,
                x => rigidDiskBlock.HiCylinder = x, result);

            if (!fix || rigidDiskBlock.ParkingZone > cylinders)
            {
                rigidDiskBlock.ParkingZone = cylinders - 1;
            }

            if (!fix || rigidDiskBlock.ReducedWrite > cylinders)
            {
                rigidDiskBlock.ReducedWrite = cylinders;
            }

            if (!fix || rigidDiskBlock.WritePreComp > cylinders)
            {
                rigidDiskBlock.WritePreComp = cylinders;
            }

            rigidDiskBlock.DiskSize = (long)cylinders * cylinderBlocks * SectorSize;

            return true;
        }

        private static bool SetRdbArea(RigidDiskBlock rigidDiskBlock, IList<PartitionBlock> partitionBlocks,
            RecoveryResult result)
        {
            var cylinderBlocks = rigidDiskBlock.CylBlocks;

            // first sector used by partitions
            var firstPartitionSector = partitionBlocks
                .Select(x => (long)x.LowCyl * x.Surfaces * x.BlocksPerTrack)
                .DefaultIfEmpty((long)rigidDiskBlock.Cylinders * cylinderBlocks)
                .Min();

            // default low cylinder after default rigid disk block area of 2016 blocks
            var loCylinder = (long)Math.Ceiling((double)(rigidDiskBlock.RdbBlockLo + 2015) * SectorSize /
                                                ((long)cylinderBlocks * SectorSize));
            if (rigidDiskBlock.LoCylinder > 0 &&
                (long)rigidDiskBlock.LoCylinder * cylinderBlocks <= firstPartitionSector)
            {
                loCylinder = rigidDiskBlock.LoCylinder;
            }

            loCylinder = Math.Min(loCylinder, firstPartitionSector / cylinderBlocks);

            if (loCylinder <= 0)
            {
                result.Error("No space for rigid disk block before first partition");
                return false;
            }

            if (rigidDiskBlock.LoCylinder != loCylinder)
            {
                result.Info($"Rigid disk block low cylinder {rigidDiskBlock.LoCylinder} -> {loCylinder}");
                rigidDiskBlock.LoCylinder = (uint)loCylinder;
            }

            var rdbBlockHi = (uint)(loCylinder * cylinderBlocks - 1);
            if (rigidDiskBlock.RdbBlockHi != rdbBlockHi)
            {
                result.Info($"Rigid disk block high rdb block {rigidDiskBlock.RdbBlockHi} -> {rdbBlockHi}");
                rigidDiskBlock.RdbBlockHi = rdbBlockHi;
            }

            // update block pointers and check blocks fits in rigid disk block area
            BlockHelper.UpdateBlockPointers(rigidDiskBlock);
            if (rigidDiskBlock.HighRsdkBlock > rigidDiskBlock.RdbBlockHi)
            {
                result.Error(
                    $"Rigid disk block requires {rigidDiskBlock.HighRsdkBlock + 1} blocks, which exceeds rigid disk block area of {rigidDiskBlock.RdbBlockHi + 1} blocks");
                return false;
            }

            // check partitions overlap
            PartitionBlock previous = null;
            foreach (var partitionBlock in partitionBlocks)
            {
                if (previous != null && (long)partitionBlock.LowCyl * partitionBlock.Surfaces *
                    partitionBlock.BlocksPerTrack < (previous.HighCyl + 1L) * previous.Surfaces *
                    previous.BlocksPerTrack)
                {
                    result.Error($"Partition '{partitionBlock.DriveName}' overlaps partition '{previous.DriveName}'");
                }

                previous = partitionBlock;
            }

            return true;
        }

        private static void SetRdbValue(bool fix, string field, uint oldValue, uint newValue, Action<uint> set,
            RecoveryResult result)
        {
            if (oldValue == newValue)
            {
                return;
            }

            if (fix)
            {
                result.Warning($"Rigid disk block {field} {oldValue} -> {newValue}");
            }

            set(newValue);
        }

        /// <summary>
        /// Build rigid disk block area bytes by writing recovered rigid disk block and compare with disk to find
        /// changed sectors. Sectors in rigid disk block area with old blocks not used by recovered rigid disk block
        /// are cleared.
        /// </summary>
        private static async Task BuildRdbArea(Stream stream, Analysis analysis, LenientRigidDiskBlock lenient,
            CancellationToken cancellationToken)
        {
            var rigidDiskBlock = analysis.Result.RigidDiskBlock;
            var areaSectors = (long)rigidDiskBlock.RdbBlockHi + 1;

            var diskBytes = new byte[areaSectors * SectorSize];
            stream.Seek(0, SeekOrigin.Begin);
            await FileSystemScanner.ReadFully(stream, diskBytes, diskBytes.Length, cancellationToken);

            var areaBytes = new byte[diskBytes.Length];
            Array.Copy(diskBytes, areaBytes, diskBytes.Length);

            // clear old blocks in rigid disk block area
            foreach (var sector in lenient.BlockSectors.Where(x => x < areaSectors))
            {
                Array.Clear(areaBytes, (int)(sector * SectorSize), SectorSize);
            }

            using (var areaStream = new MemoryStream(areaBytes, true))
            {
                await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, areaStream);
            }

            for (long sector = 0; sector < areaSectors; sector++)
            {
                var offset = (int)(sector * SectorSize);
                for (var i = 0; i < SectorSize; i++)
                {
                    if (diskBytes[offset + i] == areaBytes[offset + i])
                    {
                        continue;
                    }

                    analysis.ChangedSectors.Add(sector);
                    break;
                }
            }

            analysis.RdbAreaBytes = areaBytes;
        }

        private static string NextDriveName(string prefix, ISet<string> driveNames, IEnumerable<PartitionEntry> entries)
        {
            var reserved = new HashSet<string>(entries.Where(x => x.PartitionBlock != null)
                .Select(x => x.PartitionBlock.DriveName ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            for (var i = 0;; i++)
            {
                var name = $"{prefix}{i}";
                if (!driveNames.Contains(name) && !reserved.Contains(name))
                {
                    return name;
                }
            }
        }

        private static bool IsPfs3DosType(byte[] dosType) =>
            IsDosType(dosType, "PFS") || IsDosType(dosType, "PDS");

        private static bool IsDosType(byte[] dosType, string identifier) =>
            dosType != null && dosType.Length == 4 && dosType[0] == identifier[0] && dosType[1] == identifier[1] &&
            dosType[2] == identifier[2];

        private static string FormatDosType(byte[] dosType) =>
            dosType == null || dosType.Length != 4 ? string.Empty : dosType.FormatDosType();

        private static byte[] ParseDosType(string dosType)
        {
            dosType = dosType.Replace("\\", string.Empty);
            return DosTypeHelper.FormatDosType(dosType);
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0)
            {
                var t = a % b;
                a = b;
                b = t;
            }

            return a;
        }
    }
}
