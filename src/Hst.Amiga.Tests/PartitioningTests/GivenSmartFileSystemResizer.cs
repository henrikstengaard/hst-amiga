namespace Hst.Amiga.Tests.PartitioningTests;

using System.Threading.Tasks;
using FileSystems.SmartFileSystem.Resizer;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;

public class GivenSmartFileSystemResizer : PartitioningTestBase
{
    [Theory]
    [InlineData(30U, 59U)]
    [InlineData(22U, 49U)]
    [InlineData(22U, 59U)]
    [InlineData(30U, 39U)]
    [InlineData(40U, 49U)]
    [InlineData(36U, 45U)]
    [InlineData(22U, 31U)]
    [InlineData(30U, 32U)]
    public async Task When_ResizingPartition_Then_PartitionIsResizedAndContentIsUnchanged(uint newLowCyl,
        uint newHighCyl)
    {
        // arrange - disk with content in partition
        var stream = await CreateDisk(TestFileSystem.Sfs);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Sfs);
        var expected = await Snapshot(stream, TestPartitionNumber);
        var dh0Hash = await HashPartition(stream, 1);
        var dh2Hash = await HashPartition(stream, 3);

        // act - resize partition
        var result = await SmartFileSystemResizer.Resize(stream, TestPartitionNumber,
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
        Assert.Equal(PartitionGeometry.GetSize(partitionBlock), volume.Size);
        await WriteFile(volume, "after.bin", CreateTestData(50000, 42));
    }

    [Theory]
    [InlineData(30U, 41U)]
    [InlineData(38U, 49U)]
    public async Task When_ShrinkingPartitionWithDataAndAdminBlocksAtEnd_Then_BlocksAreRelocated(uint newLowCyl,
        uint newHighCyl)
    {
        // arrange - disk with data and administration blocks at end of partition
        var stream = await CreateDisk(TestFileSystem.Sfs);
        await WriteContentAtEnd(stream, TestPartitionNumber);
        var expected = await Snapshot(stream, TestPartitionNumber);

        // act - shrink partition
        var result = await SmartFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl });

        // assert - administration blocks and data blocks are relocated and content is unchanged
        AssertSuccess(result);
        var messages = string.Join("\n", result.Messages);
        var match = System.Text.RegularExpressions.Regex.Match(messages,
            @"relocate (\d+) administration blocks and (\d+) data blocks");
        Assert.True(match.Success, messages);
        Assert.True(int.Parse(match.Groups[1].Value) > 0, match.Value);
        Assert.True(int.Parse(match.Groups[2].Value) > 0, match.Value);
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));

        // assert - files can be written after resize
        await using var volume = await Mount(stream, TestPartitionNumber);
        await WriteFile(volume, "after.bin", CreateTestData(50000, 42));
    }

    [Fact]
    public async Task When_ShrinkingPartitionSmallerThanUsedBlocks_Then_ResizeFails()
    {
        // arrange - disk with 3mb data in partition
        var stream = await CreateDisk(TestFileSystem.Sfs);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Sfs, 10, 300 * 1024);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - shrink partition to 2 cylinders, approx 1mb
        var result = await SmartFileSystemResizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewHighCyl = 31 });

        // assert - resize failed and partition is unchanged
        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("doesn't fit"));
        Assert.Equal(49U, (await ReadPartitionBlock(stream, TestPartitionNumber)).HighCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_ExpandingRigidDiskBlockAndLastPartition_Then_PartitionUsesExpandedDisk()
    {
        // arrange - disk with stream twice the size of rigid disk block
        var stream = await CreateDisk(TestFileSystem.Sfs, DiskSize * 2);
        await WriteContent(stream, 3, TestFileSystem.Sfs);
        var expected = await Snapshot(stream, 3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);

        // act - expand rigid disk block and last partition to end of disk
        var result = await SmartFileSystemResizer.Resize(stream, 3, new PartitionResizeOptions
        {
            NewHighCyl = maxCylinders - 1,
            RigidDiskBlockCylinders = maxCylinders
        });

        // assert - rigid disk block and partition are expanded
        AssertSuccess(result);
        Assert.Equal(maxCylinders, (await RigidDiskBlockReader.Read(stream)).Cylinders);
        Assert.Equal(maxCylinders - 1, (await ReadPartitionBlock(stream, 3)).HighCyl);
        AssertSnapshotsEqual(expected, await Snapshot(stream, 3));
    }
}
