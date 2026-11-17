namespace Hst.Amiga.Tests.PartitioningTests;

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileSystems.Pfs3.Resizer;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;

public class GivenPfs3Resizer : PartitioningTestBase
{
    [Theory]
    [InlineData(30U, 59U)]
    [InlineData(22U, 49U)]
    [InlineData(22U, 59U)]
    [InlineData(30U, 39U)]
    [InlineData(40U, 49U)]
    [InlineData(36U, 45U)]
    [InlineData(22U, 31U)]
    public async Task When_ResizingPartition_Then_PartitionIsResizedAndContentIsUnchanged(uint newLowCyl,
        uint newHighCyl)
    {
        // arrange - disk with content in partition
        var stream = await CreateDisk(TestFileSystem.Pfs3);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Pfs3);
        var expected = await Snapshot(stream, TestPartitionNumber);
        var dh0Hash = await HashPartition(stream, 1);
        var dh2Hash = await HashPartition(stream, 3);

        // act - resize partition
        var result = await Pfs3Resizer.Resize(stream, TestPartitionNumber,
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
        await using (var volume = await Mount(stream, TestPartitionNumber))
        {
            Assert.Equal(PartitionGeometry.GetSize(partitionBlock), volume.Size);
            await WriteFile(volume, "after.bin", CreateTestData(50000, 42));
        }

        AssertSuccess(await Pfs3Resizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl, DryRun = true }));
    }

    [Theory]
    [InlineData(30U, 41U)]
    [InlineData(38U, 49U)]
    public async Task When_ShrinkingPartitionWithDataAtEnd_Then_DataIsRelocated(uint newLowCyl, uint newHighCyl)
    {
        // arrange - disk with data at end of partition
        var stream = await CreateDisk(TestFileSystem.Pfs3);
        await WriteContentAtEnd(stream, TestPartitionNumber, smallFiles: 300);
        var expected = await Snapshot(stream, TestPartitionNumber);

        // act - shrink partition
        var result = await Pfs3Resizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewLowCyl = newLowCyl, NewHighCyl = newHighCyl });

        // assert - data is relocated and content is unchanged
        AssertSuccess(result);
        var messages = string.Join("\n", result.Messages);
        var match = Regex.Match(messages, @"relocate (\d+) data blocks");
        Assert.True(match.Success, messages);
        Assert.True(int.Parse(match.Groups[1].Value) > 0, match.Value);
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));

        // assert - files can be written after resize
        await using var volume = await Mount(stream, TestPartitionNumber);
        await WriteFile(volume, "after.bin", CreateTestData(50000, 42));
    }

    [Fact]
    public async Task When_ShrinkingPartitionWithDeletedFileAtEnd_Then_DeletedFileIsRemoved()
    {
        // arrange - disk with deleted file at end of partition
        var stream = await CreateDisk(TestFileSystem.Pfs3);
        await using (var volume = await Mount(stream, TestPartitionNumber))
        {
            await WriteFile(volume, "keep.bin", CreateTestData(100 * 1024, 1));
            for (var i = 0; i < 14; i++)
            {
                await WriteFile(volume, $"fill{i}.bin", CreateTestData(512 * 1024, 10 + i));
            }

            await WriteFile(volume, "deleted.bin", CreateTestData(512 * 1024, 2));
            for (var i = 0; i < 14; i++)
            {
                await volume.Delete($"fill{i}.bin", true);
            }

            await volume.Delete("deleted.bin", true);
            await volume.Flush();
        }

        var expected = await Snapshot(stream, TestPartitionNumber);

        // act - shrink partition
        var result = await Pfs3Resizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewHighCyl = 39 });

        // assert - deleted file is removed and content is unchanged
        AssertSuccess(result);
        var messages = string.Join("\n", result.Messages);
        var match = Regex.Match(messages, @"remove (\d+) deleted files");
        Assert.True(match.Success, messages);
        Assert.True(int.Parse(match.Groups[1].Value) > 0, match.Value);
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_ShrinkingPartitionSmallerThanUsedBlocks_Then_ResizeFails()
    {
        // arrange - disk with 3mb data in partition
        var stream = await CreateDisk(TestFileSystem.Pfs3);
        await WriteContent(stream, TestPartitionNumber, TestFileSystem.Pfs3, 10, 300 * 1024);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - shrink partition to 4 cylinders, approx 2mb
        var result = await Pfs3Resizer.Resize(stream, TestPartitionNumber,
            new PartitionResizeOptions { NewHighCyl = 33 });

        // assert - resize failed and partition is unchanged
        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("doesn't fit") || x.Contains("too small"));
        Assert.Equal(49U, (await ReadPartitionBlock(stream, TestPartitionNumber)).HighCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_ExpandingRigidDiskBlockAndLastPartition_Then_PartitionUsesExpandedDisk()
    {
        // arrange - disk with stream 4 times the size of rigid disk block
        var stream = await CreateDisk(TestFileSystem.Pfs3, DiskSize * 4);
        await WriteContent(stream, 3, TestFileSystem.Pfs3);
        var expected = await Snapshot(stream, 3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);

        // act - expand rigid disk block and last partition to end of disk
        var result = await Pfs3Resizer.Resize(stream, 3, new PartitionResizeOptions
        {
            NewHighCyl = maxCylinders - 1,
            RigidDiskBlockCylinders = maxCylinders
        });

        // assert - rigid disk block and partition are expanded
        AssertSuccess(result);
        Assert.Equal(maxCylinders, (await RigidDiskBlockReader.Read(stream)).Cylinders);
        Assert.Equal(maxCylinders - 1, (await ReadPartitionBlock(stream, 3)).HighCyl);
        AssertSnapshotsEqual(expected, await Snapshot(stream, 3));

        // assert - files can be written in expanded area
        await using var volume = await Mount(stream, 3);
        await WriteFile(volume, "after.bin", CreateTestData(20 * 1024 * 1024, 42));
    }
}
