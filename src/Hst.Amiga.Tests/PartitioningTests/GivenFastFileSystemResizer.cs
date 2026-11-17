namespace Hst.Amiga.Tests.PartitioningTests;

using System.Threading.Tasks;
using FileSystems.FastFileSystem.Resizer;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;

public class GivenFastFileSystemResizer : PartitioningTestBase
{
    [Theory]
    [InlineData(TestFileSystem.Dos0, 30U, 59U)]
    [InlineData(TestFileSystem.Dos3, 30U, 59U)]
    [InlineData(TestFileSystem.Dos5, 30U, 59U)]
    [InlineData(TestFileSystem.Dos7, 30U, 59U)]
    [InlineData(TestFileSystem.Dos3, 22U, 49U)]
    [InlineData(TestFileSystem.Dos5, 22U, 59U)]
    [InlineData(TestFileSystem.Dos0, 30U, 39U)]
    [InlineData(TestFileSystem.Dos3, 30U, 39U)]
    [InlineData(TestFileSystem.Dos5, 30U, 39U)]
    [InlineData(TestFileSystem.Dos7, 30U, 39U)]
    [InlineData(TestFileSystem.Dos3, 40U, 49U)]
    [InlineData(TestFileSystem.Dos7, 36U, 45U)]
    [InlineData(TestFileSystem.Dos3, 22U, 31U)]
    [InlineData(TestFileSystem.Dos3, 30U, 32U)]
    public async Task When_ResizingPartition_Then_PartitionIsResizedAndContentIsUnchanged(
        TestFileSystem fileSystem, uint newLowCyl, uint newHighCyl)
    {
        // arrange - disk with content in partition
        var stream = await CreateDisk(fileSystem);
        await WriteContent(stream, TestPartitionNumber, fileSystem);
        var expected = await Snapshot(stream, TestPartitionNumber);
        var dh0Hash = await HashPartition(stream, 1);
        var dh2Hash = await HashPartition(stream, 3);

        // act - resize partition
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl });

        // assert - partition is resized and file system has no errors
        AssertSuccess(result);
        var partitionBlock = await ReadPartitionBlock(stream, TestPartitionNumber);
        Assert.Equal(newLowCyl, partitionBlock.LowCyl);
        Assert.Equal(newHighCyl, partitionBlock.HighCyl);

        // assert - content is unchanged and other partitions are not changed
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));
        Assert.Equal(dh0Hash, await HashPartition(stream, 1));
        Assert.Equal(dh2Hash, await HashPartition(stream, 3));

        // assert - volume size matches partition size and files can be written
        await using var volume = await Mount(stream, TestPartitionNumber);
        Assert.True(volume.Free > 0);
        await WriteFile(volume, "after.bin", CreateTestData(50000, 42));
    }

    [Theory]
    [InlineData(TestFileSystem.Dos0)]
    [InlineData(TestFileSystem.Dos5)]
    [InlineData(TestFileSystem.Dos7)]
    public async Task When_ShrinkingPartitionWithBlocksUsedInAreaCutOff_Then_BlocksAreRelocated(
        TestFileSystem fileSystem)
    {
        // arrange - disk with content in partition, data is placed after root block in the middle of partition
        var stream = await CreateDisk(fileSystem);
        await WriteContent(stream, TestPartitionNumber, fileSystem);
        var expected = await Snapshot(stream, TestPartitionNumber);

        // act - shrink partition to first half
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewHighCyl = 39 });

        // assert - blocks are relocated and content is unchanged
        AssertSuccess(result);
        var match = System.Text.RegularExpressions.Regex.Match(string.Join("\n", result.Messages),
            @"relocate (\d+) blocks");
        Assert.True(match.Success);
        Assert.True(int.Parse(match.Groups[1].Value) > 1000, match.Value);
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));
    }

    [Theory]
    [InlineData(TestFileSystem.Dos3, 30U, 41U)]
    [InlineData(TestFileSystem.Dos5, 38U, 49U)]
    public async Task When_ShrinkingPartitionWithDataAtEnd_Then_DataIsRelocated(TestFileSystem fileSystem,
        uint newLowCyl, uint newHighCyl)
    {
        // arrange - disk with data and many files at end of partition
        var stream = await CreateDisk(fileSystem);
        await WriteContentAtEnd(stream, TestPartitionNumber, smallFiles: 500);
        var expected = await Snapshot(stream, TestPartitionNumber);

        // act - shrink partition
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl });

        // assert - content is unchanged
        AssertSuccess(result);
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_ShrinkingPartitionSmallerThanUsedBlocks_Then_ResizeFails()
    {
        // arrange - disk with 3mb data in partition
        var stream = await CreateDisk(TestFileSystem.Dos3);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Dos3, 10, 300 * 1024);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - shrink partition to 2 cylinders, approx 1mb
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewHighCyl = 31 });

        // assert - resize failed and partition is unchanged
        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("doesn't fit"));
        Assert.Equal(49U, (await ReadPartitionBlock(stream, TestPartitionNumber)).HighCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Theory]
    [InlineData(21U, 49U)]
    [InlineData(30U, 60U)]
    public async Task When_ResizingPartitionOutsideAvailableCylinders_Then_ResizeFails(uint newLowCyl,
        uint newHighCyl)
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);

        // act - resize partition over other partition
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl });

        // assert - resize failed
        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("outside cylinders"));
    }

    [Fact]
    public async Task When_ShrinkingPartitionWithDryRun_Then_NothingIsChanged()
    {
        // arrange - disk with content
        var stream = await CreateDisk(TestFileSystem.Dos3);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Dos3);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - shrink partition with dry run
        var result = await FastFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = 35, NewHighCyl = 42, DryRun = true });

        // assert - dry run succeeded and nothing is changed
        AssertSuccess(result);
        Assert.Equal(49U, (await ReadPartitionBlock(stream, TestPartitionNumber)).HighCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_ExpandingRigidDiskBlockAndLastPartition_Then_PartitionUsesExpandedDisk()
    {
        // arrange - disk with stream twice the size of rigid disk block
        var stream = await CreateDisk(TestFileSystem.Dos3, DiskSize * 2);
        await WriteContent(stream, 3, TestFileSystem.Dos3);
        var expected = await Snapshot(stream, 3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);

        // act - expand rigid disk block and last partition to end of disk
        var result = await FastFileSystemResizer.Resize(stream, 3, new PartitionResizeOptions
        {
            NewHighCyl = maxCylinders - 1,
            RigidDiskBlockCylinders = maxCylinders
        });

        // assert - rigid disk block and partition are expanded
        AssertSuccess(result);
        rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.Equal(maxCylinders, rigidDiskBlock.Cylinders);
        Assert.Equal(maxCylinders - 1, (await ReadPartitionBlock(stream, 3)).HighCyl);
        AssertSnapshotsEqual(expected, await Snapshot(stream, 3));
    }
}
