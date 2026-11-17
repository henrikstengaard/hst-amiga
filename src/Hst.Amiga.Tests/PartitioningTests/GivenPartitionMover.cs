namespace Hst.Amiga.Tests.PartitioningTests;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileSystems.FastFileSystem.Mover;
using FileSystems.Pfs3.Mover;
using FileSystems.SmartFileSystem.Mover;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;

public class GivenPartitionMover : PartitioningTestBase
{
    private static Task<PartitionOperationResult> Move(Stream stream, TestFileSystem fileSystem,
        PartitionMoveOptions options) => fileSystem switch
    {
        TestFileSystem.Pfs3 => Pfs3Mover.Move(stream, TestPartitionNumber, options, CancellationToken.None),
        TestFileSystem.Sfs => SmartFileSystemMover.Move(stream, TestPartitionNumber, options),
        _ => FastFileSystemMover.Move(stream, TestPartitionNumber, options)
    };

    [Theory]
    [InlineData(TestFileSystem.Dos0, 33U)]
    [InlineData(TestFileSystem.Dos3, 33U)]
    [InlineData(TestFileSystem.Dos5, 26U)]
    [InlineData(TestFileSystem.Dos7, 22U)]
    [InlineData(TestFileSystem.Pfs3, 33U)]
    [InlineData(TestFileSystem.Pfs3, 26U)]
    [InlineData(TestFileSystem.Sfs, 33U)]
    [InlineData(TestFileSystem.Sfs, 26U)]
    [InlineData(TestFileSystem.Dos3, 85U)]
    [InlineData(TestFileSystem.Pfs3, 85U)]
    [InlineData(TestFileSystem.Sfs, 85U)]
    public async Task When_MovingPartition_Then_PartitionIsMovedAndContentIsUnchanged(TestFileSystem fileSystem,
        uint newLowCyl)
    {
        // arrange - disk with content in partition
        var stream = await CreateDisk(fileSystem);
        await WriteContent(stream, TestPartitionNumber, fileSystem);
        var expected = await Snapshot(stream, TestPartitionNumber);
        var dh0Hash = await HashPartition(stream, 1);
        var dh2Hash = await HashPartition(stream, 3);

        // act - move partition
        var result = await Move(stream, fileSystem, new PartitionMoveOptions { NewLowCyl = newLowCyl });

        // assert - partition is moved
        AssertSuccess(result);
        var partitionBlock = await ReadPartitionBlock(stream, TestPartitionNumber);
        Assert.Equal(newLowCyl, partitionBlock.LowCyl);
        Assert.Equal(newLowCyl + 19, partitionBlock.HighCyl);

        // assert - content is unchanged and other partitions are not changed
        AssertSnapshotsEqual(expected, await Snapshot(stream, TestPartitionNumber));
        Assert.Equal(dh0Hash, await HashPartition(stream, 1));
        Assert.Equal(dh2Hash, await HashPartition(stream, 3));
    }

    [Theory]
    [InlineData(TestFileSystem.Dos3, 45U)]
    [InlineData(TestFileSystem.Dos3, 15U)]
    [InlineData(TestFileSystem.Sfs, 0U)]
    [InlineData(TestFileSystem.Pfs3, 110U)]
    public async Task When_MovingPartitionOverOtherPartitionOrOutsideRigidDiskBlock_Then_MoveFails(
        TestFileSystem fileSystem, uint newLowCyl)
    {
        // arrange - disk
        var stream = await CreateDisk(fileSystem);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - move partition
        var result = await Move(stream, fileSystem, new PartitionMoveOptions { NewLowCyl = newLowCyl });

        // assert - move failed and partition is unchanged
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        var partitionBlock = await ReadPartitionBlock(stream, TestPartitionNumber);
        Assert.Equal(30U, partitionBlock.LowCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Theory]
    [InlineData(TestFileSystem.Dos3)]
    [InlineData(TestFileSystem.Pfs3)]
    [InlineData(TestFileSystem.Sfs)]
    public async Task When_MovingPartitionWithFileSystemErrors_Then_MoveFails(TestFileSystem fileSystem)
    {
        // arrange - disk with corrupt file system in partition
        var stream = await CreateDisk(fileSystem);
        var partitionBlock = await ReadPartitionBlock(stream, TestPartitionNumber);
        var start = PartitionGeometry.GetStartOffset(partitionBlock);
        var blocks = PartitionGeometry.GetSize(partitionBlock) / 512;
        var corruptBlocks = fileSystem switch
        {
            // fast file system root block in middle of partition
            TestFileSystem.Dos3 => new[] { blocks / 2 },
            // pfs3 boot block
            TestFileSystem.Pfs3 => new[] { 0L },
            // sfs root blocks
            _ => new[] { 0L, blocks - 1 }
        };
        foreach (var block in corruptBlocks)
        {
            stream.Seek(start + block * 512, System.IO.SeekOrigin.Begin);
            await stream.WriteAsync(new byte[512]);
        }

        // act - move partition
        var result = await Move(stream, fileSystem, new PartitionMoveOptions { NewLowCyl = 33 });

        // assert - move failed and partition is not moved
        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("has errors before move"));
        Assert.Equal(30U, (await ReadPartitionBlock(stream, TestPartitionNumber)).LowCyl);
    }

    [Fact]
    public async Task When_MovingPartitionWithDryRun_Then_NothingIsChanged()
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);
        var dh1Hash = await HashPartition(stream, TestPartitionNumber);

        // act - move partition with dry run
        var result = await FastFileSystemMover.Move(stream, TestPartitionNumber,
            new PartitionMoveOptions { NewLowCyl = 33, DryRun = true });

        // assert - dry run succeeded and partition is unchanged
        AssertSuccess(result);
        var partitionBlock = await ReadPartitionBlock(stream, TestPartitionNumber);
        Assert.Equal(30U, partitionBlock.LowCyl);
        Assert.Equal(dh1Hash, await HashPartition(stream, TestPartitionNumber));
    }

    [Fact]
    public async Task When_MovingPartitionWithOtherFileSystem_Then_MoveFails()
    {
        // arrange - disk with sfs partitions
        var stream = await CreateDisk(TestFileSystem.Sfs);

        // act - move partition with fast file system mover
        var result = await FastFileSystemMover.Move(stream, TestPartitionNumber,
            new PartitionMoveOptions { NewLowCyl = 33 });

        // assert - move failed
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(TestFileSystem.Dos3)]
    [InlineData(TestFileSystem.Sfs)]
    [InlineData(TestFileSystem.Pfs3)]
    public async Task When_MovingLastPartitionToExpandedRigidDiskBlock_Then_PartitionIsMovedToEndOfDisk(
        TestFileSystem fileSystem)
    {
        // arrange - disk with stream larger than rigid disk block
        var stream = await CreateDisk(fileSystem, DiskSize * 2);
        await WriteContent(stream, 3, fileSystem);
        var expected = await Snapshot(stream, 3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);

        // act - expand rigid disk block and move last partition to end of disk
        var options = new PartitionMoveOptions
        {
            NewLowCyl = maxCylinders - 20,
            RigidDiskBlockCylinders = maxCylinders
        };
        var result = fileSystem switch
        {
            TestFileSystem.Pfs3 => await Pfs3Mover.Move(stream, 3, options),
            TestFileSystem.Sfs => await SmartFileSystemMover.Move(stream, 3, options),
            _ => await FastFileSystemMover.Move(stream, 3, options)
        };

        // assert - rigid disk block is expanded and partition is moved
        AssertSuccess(result);
        rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.Equal(maxCylinders, rigidDiskBlock.Cylinders);
        Assert.Equal(maxCylinders - 1, rigidDiskBlock.HiCylinder);
        var partitionBlock = await ReadPartitionBlock(stream, 3);
        Assert.Equal(maxCylinders - 20, partitionBlock.LowCyl);
        Assert.Equal(maxCylinders - 1, partitionBlock.HighCyl);
        AssertSnapshotsEqual(expected, await Snapshot(stream, 3));
    }
}
