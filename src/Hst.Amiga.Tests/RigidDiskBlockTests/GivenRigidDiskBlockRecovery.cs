namespace Hst.Amiga.Tests.RigidDiskBlockTests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Converters;
using Core.Extensions;
using Extensions;
using FileSystems;
using FileSystems.FastFileSystem;
using FileSystems.Pfs3;
using FileSystems.SmartFileSystem;
using RigidDiskBlocks;
using RigidDiskBlocks.Recovery;
using Xunit;
using ChecksumHelper = FileSystems.ChecksumHelper;

public class GivenRigidDiskBlockRecovery
{
    private const int SectorSize = 512;
    private const long DiskSize = 300L * 1024 * 1024;

    private static readonly byte[] FfsDosType = { 0x44, 0x4f, 0x53, 0x3 };
    private static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    private static readonly byte[] SfsDosType = { 0x53, 0x46, 0x53, 0x0 };

    private static readonly string[] DirectoryNames = { "FfsDir", "Pfs3Dir", "SfsDir" };

    [Fact]
    public async Task WhenAnalyzeValidRigidDiskBlockThenRigidDiskBlockIsNotModified()
    {
        // arrange - create disk with ffs, pfs3 and sfs partitions
        var (stream, expected) = await CreateDisk();

        // act - analyze disk
        var result = await RigidDiskBlockRecovery.Analyze(stream);

        // assert - rigid disk block is valid and not modified
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.False(result.Modified, string.Join(Environment.NewLine, result.Messages));
        Assert.Equal(RecoveryMode.Fix, result.Mode);

        // assert - file systems found
        Assert.Equal(new[]
        {
            FileSystemType.FastFileSystem, FileSystemType.ProfessionalFileSystem3, FileSystemType.SmartFileSystem
        }, result.FileSystems.Select(x => x.Type));
        AssertPartitions(expected, result.RigidDiskBlock);
    }

    [Fact]
    public async Task WhenAnalyzeBrokenRigidDiskBlockThenDiskIsNotChanged()
    {
        // arrange - create disk and corrupt rigid disk block checksum
        var (stream, _) = await CreateDisk();
        await ModifySector(stream, 0, bytes => bytes[0x10] ^= 0xff, updateChecksum: false);
        var before = await ReadSectors(stream, 0, 2016);

        // act - analyze disk
        var result = await RigidDiskBlockRecovery.Analyze(stream);

        // assert - rigid disk block is modified, but disk is not changed
        Assert.True(result.Modified);
        Assert.False(result.Written);
        Assert.Equal(before, await ReadSectors(stream, 0, 2016));
    }

    [Fact]
    public async Task WhenRepairRigidDiskBlockWithInvalidChecksumThenRigidDiskBlockIsFixed()
    {
        // arrange - create disk and corrupt rigid disk block checksum
        var (stream, expected) = await CreateDisk();
        await ModifySector(stream, 0, bytes => BigEndianConverter.ConvertInt32ToBytes(12345, bytes, 8),
            updateChecksum: false);
        await Assert.ThrowsAnyAsync<Exception>(() => RigidDiskBlockReader.Read(stream));

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - rigid disk block is fixed
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.True(result.Written);
        Assert.Equal(RecoveryMode.Fix, result.Mode);
        await AssertDisk(stream, expected);
    }

    [Fact]
    public async Task WhenRepairBrokenPartitionListThenAllPartitionsAreRecovered()
    {
        // arrange - create disk and break next partition block pointer of first partition block
        var (stream, expected) = await CreateDisk();
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        await ModifySector(stream, rigidDiskBlock.PartitionList,
            bytes => BigEndianConverter.ConvertUInt32ToBytes(1500, bytes, 0x10), updateChecksum: false);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - all partitions are recovered
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        await AssertDisk(stream, expected);
    }

    [Fact]
    public async Task WhenRepairMissingPartitionBlockThenPartitionIsAddedFromFileSystem()
    {
        // arrange - create disk and zero pfs3 partition block
        var (stream, expected) = await CreateDisk();
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var pfs3PartitionBlock = rigidDiskBlock.PartitionBlocks.ElementAt(0).NextPartitionBlock;
        await ModifySector(stream, pfs3PartitionBlock, bytes => Array.Clear(bytes), updateChecksum: false);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - pfs3 partition is added with name, dos type and cylinders from file system
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        await AssertDisk(stream, expected);
    }

    [Fact]
    public async Task WhenRepairPartitionWithWrongHighCylinderThenHighCylinderIsCorrected()
    {
        // arrange - create disk and change high cylinder of sfs partition
        var (stream, expected) = await CreateDisk();
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var sfsPartitionBlock = rigidDiskBlock.PartitionBlocks.ElementAt(1).NextPartitionBlock;
        var sfsHighCyl = rigidDiskBlock.PartitionBlocks.ElementAt(2).HighCyl;
        await ModifySector(stream, sfsPartitionBlock,
            bytes => BigEndianConverter.ConvertUInt32ToBytes(sfsHighCyl - 10, bytes, 0xa8));
        Assert.Equal(sfsHighCyl - 10, (await RigidDiskBlockReader.Read(stream)).PartitionBlocks.ElementAt(2).HighCyl);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - high cylinder is corrected from smart file system root block
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        await AssertDisk(stream, expected);
    }

    [Fact]
    public async Task WhenRepairDiskWithRigidDiskBlockAreaClearedThenRigidDiskBlockIsRebuilt()
    {
        // arrange - create disk and clear rigid disk block area
        var (stream, expected) = await CreateDisk();
        await ClearSectors(stream, 0, 2016);
        Assert.Null(await RigidDiskBlockReader.Read(stream));

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - rigid disk block is rebuilt with partitions from file systems found
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.Equal(RecoveryMode.Rebuild, result.Mode);
        Assert.True(result.Written);

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.NotNull(rigidDiskBlock);
        Assert.Empty(rigidDiskBlock.FileSystemHeaderBlocks);
        Assert.Equal(new[] { "DH0", "DH1", "DH2" }, rigidDiskBlock.PartitionBlocks.Select(x => x.DriveName));
        Assert.True(rigidDiskBlock.PartitionBlocks.First().Bootable);
        AssertPartitions(expected, rigidDiskBlock);
        await AssertVolumes(stream, rigidDiskBlock);
    }

    [Fact]
    public async Task WhenRepairDiskWithRigidDiskBlockMissingThenFileSystemsAreKept()
    {
        // arrange - create disk and clear rigid disk block, partition and file system blocks are intact
        var (stream, expected) = await CreateDisk();
        await ClearSectors(stream, 0, 1);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - rigid disk block is rebuilt with partitions and file systems
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.Equal(RecoveryMode.Rebuild, result.Mode);

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.NotNull(rigidDiskBlock);
        Assert.Equal(expected.FileSystemHeaderBlocks.Select(x => x.DosType),
            rigidDiskBlock.FileSystemHeaderBlocks.Select(x => x.DosType));
        Assert.Equal(expected.FileSystemHeaderBlocks.Select(x => x.FileSystemSize),
            rigidDiskBlock.FileSystemHeaderBlocks.Select(x => x.FileSystemSize));
        await AssertDisk(stream, expected);
    }

    [Fact]
    public async Task WhenRepairDiskWithOldFileSystemThenNewestFileSystemsAreUsed()
    {
        // arrange - create disk with smart file system partition starting at 50mb, which is within fast file
        // system partition created later and not overwritten by format of fast file system
        var stream = new BlockMemoryStream();
        var oldRigidDiskBlock = RigidDiskBlock.Create(DiskSize.ToUniversalSize());
        stream.SetLength(oldRigidDiskBlock.DiskSize);
        oldRigidDiskBlock
            .AddPartition(FfsDosType, "DH0", 50 * 1024 * 1024, bootable: true)
            .AddPartition(SfsDosType, "DH1");
        await RigidDiskBlockWriter.WriteBlock(oldRigidDiskBlock, stream);
        await SmartFileSystemFormatter.FormatPartition(stream, oldRigidDiskBlock.PartitionBlocks.Last(), "Old");

        // arrange - wait for creation date of new file systems to be newer and partition disk again
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var (_, expected) = await CreateDisk(stream);

        // arrange - clear rigid disk block area
        await ClearSectors(stream, 0, 2016);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - old smart file system root block overlapping new file systems is skipped
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.Contains(result.Messages,
            x => x.Level == RecoveryMessageLevel.Warning && x.Text.StartsWith("Skipped SmartFileSystem"));

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        AssertPartitions(expected, rigidDiskBlock);
        await AssertVolumes(stream, rigidDiskBlock);
    }

    [Theory]
    [InlineData(11, FileSystemScanner.DoubleDensityFloppySectors)]
    [InlineData(22, FileSystemScanner.HighDensityFloppySectors)]
    public async Task WhenRepairDiskWithFloppyImageAndSkipFloppyImagesThenFloppyImageIsSkipped(
        uint floppySectors, int floppySize)
    {
        // arrange - create disk, clear rigid disk block area and write floppy image within sfs partition
        var (stream, expected) = await CreateDisk();
        await ClearSectors(stream, 0, 2016);
        await WriteFloppyImage(stream, floppySectors);

        // act - repair rigid disk block skipping floppy images by default
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - floppy image is skipped and partitions are rebuilt
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.Contains(result.Messages, x => x.Text.StartsWith("Skipped FastFileSystem") &&
                                              x.Text.Contains($"size {floppySize} sectors") &&
                                              x.Text.EndsWith("appears to be a floppy image"));
        Assert.Equal(3, result.FileSystems.Count);

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        AssertPartitions(expected, rigidDiskBlock);
        await AssertVolumes(stream, rigidDiskBlock);
    }

    [Fact]
    public async Task WhenRepairDiskWithFragmentedFloppyImageAndSkipFloppyImagesThenFloppyImageIsSkipped()
    {
        // arrange - create disk, clear rigid disk block area and write double density floppy image within sfs
        // partition with root block moved from block 880 to 544 like a floppy image file stored fragmented
        var (stream, expected) = await CreateDisk();
        await ClearSectors(stream, 0, 2016);
        var floppyStartSector = await WriteFloppyImage(stream, 11);
        var rootBlockBytes = await ReadSectors(stream, floppyStartSector + 880, 1);
        await ClearSectors(stream, floppyStartSector + 880, 1);
        stream.Seek((floppyStartSector + 544) * SectorSize, SeekOrigin.Begin);
        await stream.WriteBytes(rootBlockBytes);

        // act - repair rigid disk block skipping floppy images by default
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - fragmented floppy image with estimated size 1088 sectors is skipped by bitmap block offset
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        Assert.Contains(result.Messages, x => x.Text.StartsWith("Skipped FastFileSystem 'Floppy'") &&
                                              x.Text.Contains("size 1088 sectors") &&
                                              x.Text.EndsWith("appears to be a floppy image"));

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        AssertPartitions(expected, rigidDiskBlock);
        await AssertVolumes(stream, rigidDiskBlock);
    }

    [Fact]
    public async Task WhenAnalyzeDiskWithFloppyImageAndNotSkipFloppyImagesThenFloppyImageIsNotSkipped()
    {
        // arrange - create disk and write double density floppy image within sfs partition
        var (stream, _) = await CreateDisk();
        await WriteFloppyImage(stream, 11);

        // act - analyze disk without skipping floppy images
        var result = await RigidDiskBlockRecovery.Analyze(stream, new RecoveryOptions { SkipFloppyImages = false });

        // assert - floppy image is found as file system
        Assert.DoesNotContain(result.Messages, x => x.Text.EndsWith("appears to be a floppy image"));
        Assert.Contains(result.Messages, x => x.Text.Contains("FastFileSystem 'Floppy'"));
    }

    /// <summary>
    /// Write floppy image formatted with fast file system within free area of sfs partition.
    /// </summary>
    private static async Task<long> WriteFloppyImage(Stream stream, uint sectorsPerTrack)
    {
        const uint heads = 2;
        const uint floppyStartSector = 500016;
        var lowCyl = floppyStartSector / (heads * sectorsPerTrack);
        await FastFileSystemFormatter.Format(stream, lowCyl, lowCyl + 79, 2, heads, sectorsPerTrack, 512, 512,
            new byte[] { 0x44, 0x4f, 0x53, 0x0 }, "Floppy");
        return (long)lowCyl * heads * sectorsPerTrack;
    }

    [Fact]
    public async Task WhenRepairDiskWithDisplacedPfs3RootBlockThenPartitionIsAtVolumeStartAndRootBlockCanBeRestored()
    {
        // arrange - create disk, displace pfs3 boot and root blocks and clear rigid disk block area
        var (stream, expected) = await CreateDisk();
        var pfs3PartitionBlock = expected.PartitionBlocks.ElementAt(1);
        var volumeStart = (long)pfs3PartitionBlock.LowCyl * pfs3PartitionBlock.Surfaces *
                          pfs3PartitionBlock.BlocksPerTrack;
        const long displacedSectors = 100000;
        await DisplacePfs3RootBlock(stream, volumeStart, displacedSectors);
        await ClearSectors(stream, 0, 2016);

        // act - repair rigid disk block
        var result = await RigidDiskBlockRecovery.Repair(stream);

        // assert - pfs3 partition is placed at volume start and displaced boot and root blocks are reported
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Messages));
        var pfs3FileSystem = result.FileSystems.Single(x => x.Type == FileSystemType.ProfessionalFileSystem3);
        Assert.Equal(volumeStart, pfs3FileSystem.StartSector);
        Assert.Equal(displacedSectors, pfs3FileSystem.DisplacedSectors);
        Assert.Contains(result.Messages,
            x => x.Level == RecoveryMessageLevel.Warning && x.Text.Contains("displaced 100000 sectors"));

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        AssertPartitions(expected, rigidDiskBlock);

        // act - restore displaced boot and root blocks with pfs3 doctor
        var restoreResult =
            await FileSystems.Pfs3.Doctor.Pfs3Doctor.RestoreRootBlock(stream, rigidDiskBlock.PartitionBlocks.ElementAt(1));

        // assert - boot and root blocks are restored and all volumes can be mounted
        Assert.True(restoreResult.Restored, string.Join(Environment.NewLine, restoreResult.Messages));
        Assert.Equal(volumeStart + displacedSectors, restoreResult.SourceSector);
        Assert.Equal(displacedSectors, restoreResult.DisplacedSectors);
        await AssertVolumes(stream, rigidDiskBlock);

        // assert - restoring again finds valid root block
        restoreResult =
            await FileSystems.Pfs3.Doctor.Pfs3Doctor.RestoreRootBlock(stream, rigidDiskBlock.PartitionBlocks.ElementAt(1));
        Assert.True(restoreResult.RootBlockValid);
        Assert.False(restoreResult.Restored);
    }

    /// <summary>
    /// Displace pfs3 boot and root blocks by copying them to an offset from volume start and overwriting
    /// boot and root blocks at volume start with 0xff.
    /// </summary>
    private static async Task DisplacePfs3RootBlock(Stream stream, long volumeStart, long displacedSectors)
    {
        var rootBlockBytes = await ReadSectors(stream, volumeStart + 2, 1);
        var sectors = 2 + BigEndianConverter.ConvertBytesToUInt16(rootBlockBytes, 0x42);
        var bytes = await ReadSectors(stream, volumeStart, sectors);
        stream.Seek((volumeStart + displacedSectors) * SectorSize, SeekOrigin.Begin);
        await stream.WriteBytes(bytes);
        stream.Seek(volumeStart * SectorSize, SeekOrigin.Begin);
        await stream.WriteBytes(Enumerable.Repeat((byte)0xff, sectors * SectorSize).ToArray());
    }

    private static async Task<(BlockMemoryStream, RigidDiskBlock)> CreateDisk(BlockMemoryStream stream = null)
    {
        stream ??= new BlockMemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create(DiskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);

        rigidDiskBlock
            .AddFileSystem(Pfs3DosType, Encoding.ASCII.GetBytes("$VER: pfs3aio 19.2 (01/01/22)   "))
            .AddFileSystem(SfsDosType, Encoding.ASCII.GetBytes("$VER: SmartFilesystem 1.84 (01/01/22)   "))
            .AddPartition(FfsDosType, "DH0", 80 * 1024 * 1024, bootable: true)
            .AddPartition(Pfs3DosType, "DH1", 100 * 1024 * 1024)
            .AddPartition(SfsDosType, "DH2");
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        await FastFileSystemFormatter.FormatPartition(stream, partitionBlocks[0], "FFS");
        await Pfs3Formatter.FormatPartition(stream, partitionBlocks[1], "PFS3");
        await SmartFileSystemFormatter.FormatPartition(stream, partitionBlocks[2], "SFS");

        // create directory in each volume to verify volumes after recovery
        await using (var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlocks[0]))
        {
            await volume.CreateDirectory(DirectoryNames[0]);
        }

        await using (var volume = await Pfs3Volume.Mount(stream, partitionBlocks[1]))
        {
            await volume.CreateDirectory(DirectoryNames[1]);
        }

        await using (var volume = await SmartFileSystemVolume.Mount(stream, partitionBlocks[2]))
        {
            await volume.CreateDirectory(DirectoryNames[2]);
        }

        return (stream, await RigidDiskBlockReader.Read(stream));
    }

    private static async Task AssertDisk(Stream stream, RigidDiskBlock expected)
    {
        // read rigid disk block with checksum verification
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.NotNull(rigidDiskBlock);
        Assert.Equal(expected.Cylinders, rigidDiskBlock.Cylinders);
        Assert.Equal(expected.Heads, rigidDiskBlock.Heads);
        Assert.Equal(expected.Sectors, rigidDiskBlock.Sectors);
        Assert.Equal(expected.HiCylinder, rigidDiskBlock.HiCylinder);
        Assert.Equal(expected.PartitionBlocks.Select(x => x.DriveName),
            rigidDiskBlock.PartitionBlocks.Select(x => x.DriveName));
        Assert.Equal(expected.PartitionBlocks.Select(x => x.Bootable),
            rigidDiskBlock.PartitionBlocks.Select(x => x.Bootable));
        AssertPartitions(expected, rigidDiskBlock);
        await AssertVolumes(stream, rigidDiskBlock);
    }

    private static void AssertPartitions(RigidDiskBlock expected, RigidDiskBlock actual)
    {
        var expectedPartitions = expected.PartitionBlocks.ToList();
        var actualPartitions = actual.PartitionBlocks.ToList();
        Assert.Equal(expectedPartitions.Count, actualPartitions.Count);

        for (var i = 0; i < expectedPartitions.Count; i++)
        {
            Assert.Equal(expectedPartitions[i].LowCyl, actualPartitions[i].LowCyl);
            Assert.Equal(expectedPartitions[i].HighCyl, actualPartitions[i].HighCyl);
            Assert.Equal(expectedPartitions[i].Surfaces, actualPartitions[i].Surfaces);
            Assert.Equal(expectedPartitions[i].BlocksPerTrack, actualPartitions[i].BlocksPerTrack);
            Assert.Equal(expectedPartitions[i].DosType, actualPartitions[i].DosType);
            Assert.Equal(expectedPartitions[i].FileSystemBlockSize, actualPartitions[i].FileSystemBlockSize);
        }
    }

    private static async Task AssertVolumes(Stream stream, RigidDiskBlock rigidDiskBlock)
    {
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

        await using (var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlocks[0]))
        {
            await AssertVolume(volume, DirectoryNames[0]);
        }

        await using (var volume = await Pfs3Volume.Mount(stream, partitionBlocks[1]))
        {
            await AssertVolume(volume, DirectoryNames[1]);
        }

        await using (var volume = await SmartFileSystemVolume.Mount(stream, partitionBlocks[2]))
        {
            await AssertVolume(volume, DirectoryNames[2]);
        }
    }

    private static async Task AssertVolume(IFileSystemVolume volume, string directoryName)
    {
        var entries = (await volume.ListEntries()).ToList();
        Assert.Contains(entries, x => x.Name == directoryName && x.Type == EntryType.Dir);
    }

    private static async Task ModifySector(Stream stream, long sector, Action<byte[]> modify,
        bool updateChecksum = true)
    {
        var bytes = await ReadSectors(stream, sector, 1);
        modify(bytes);

        if (updateChecksum)
        {
            var size = BigEndianConverter.ConvertBytesToUInt32(bytes, 4);
            ChecksumHelper.UpdateChecksum(bytes, 8, (int)size * 4);
        }

        stream.Seek(sector * SectorSize, SeekOrigin.Begin);
        await stream.WriteBytes(bytes);
    }

    private static async Task ClearSectors(Stream stream, long sector, int sectors)
    {
        stream.Seek(sector * SectorSize, SeekOrigin.Begin);
        await stream.WriteBytes(new byte[sectors * SectorSize]);
    }

    private static async Task<byte[]> ReadSectors(Stream stream, long sector, int sectors)
    {
        stream.Seek(sector * SectorSize, SeekOrigin.Begin);
        return await stream.ReadBytes(sectors * SectorSize);
    }
}
