namespace Hst.Amiga.Tests.PartitioningTests;

using System.Threading.Tasks;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;

public class GivenRigidDiskBlockResizer : PartitioningTestBase
{
    [Fact]
    public async Task When_ExpandingRigidDiskBlockToDiskSize_Then_CylindersAreUpdated()
    {
        // arrange - disk with stream twice the size of rigid disk block
        var stream = await CreateDisk(TestFileSystem.Dos3, DiskSize * 2);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);
        Assert.True(maxCylinders > rigidDiskBlock.Cylinders);

        // act - expand rigid disk block
        var result = await RigidDiskBlockResizer.Resize(stream, maxCylinders);

        // assert - rigid disk block is expanded
        AssertSuccess(result);
        rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.Equal(maxCylinders, rigidDiskBlock.Cylinders);
        Assert.Equal(maxCylinders - 1, rigidDiskBlock.HiCylinder);
        Assert.Equal(maxCylinders - 1, rigidDiskBlock.ParkingZone);
        Assert.Equal(maxCylinders * PartitionGeometry.GetCylinderSize(rigidDiskBlock), rigidDiskBlock.DiskSize);
        Assert.Equal(3, System.Linq.Enumerable.Count(rigidDiskBlock.PartitionBlocks));
    }

    [Fact]
    public async Task When_ShrinkingRigidDiskBlockToLastPartition_Then_CylindersAreUpdated()
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);

        // act - shrink rigid disk block to end of last partition
        var result = await RigidDiskBlockResizer.Resize(stream, 80);

        // assert - rigid disk block is shrunk
        AssertSuccess(result);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        Assert.Equal(80U, rigidDiskBlock.Cylinders);
        Assert.Equal(79U, rigidDiskBlock.HiCylinder);
    }

    [Fact]
    public async Task When_ShrinkingRigidDiskBlockBeforeLastPartition_Then_ResizeFails()
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);
        var cylinders = (await RigidDiskBlockReader.Read(stream)).Cylinders;

        // act - shrink rigid disk block into last partition
        var result = await RigidDiskBlockResizer.Resize(stream, 79);

        // assert - resize failed and rigid disk block is unchanged
        Assert.False(result.Success);
        Assert.Equal(cylinders, (await RigidDiskBlockReader.Read(stream)).Cylinders);
    }

    [Fact]
    public async Task When_ExpandingRigidDiskBlockLargerThanDisk_Then_ResizeFails()
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        // act - expand rigid disk block beyond disk size
        var result = await RigidDiskBlockResizer.Resize(stream, rigidDiskBlock.Cylinders + 1);

        // assert - resize failed
        Assert.False(result.Success);
    }

    [Fact]
    public async Task When_GettingAvailableCylinders_Then_FreeCylindersBeforeAndAfterPartitionAreIncluded()
    {
        // arrange - disk
        var stream = await CreateDisk(TestFileSystem.Dos3);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        // act - get available cylinders for partitions
        var dh0 = PartitionGeometry.GetAvailableCylinders(rigidDiskBlock,
            System.Linq.Enumerable.ElementAt(rigidDiskBlock.PartitionBlocks, 0));
        var dh1 = PartitionGeometry.GetAvailableCylinders(rigidDiskBlock,
            System.Linq.Enumerable.ElementAt(rigidDiskBlock.PartitionBlocks, 1));
        var dh2 = PartitionGeometry.GetAvailableCylinders(rigidDiskBlock,
            System.Linq.Enumerable.ElementAt(rigidDiskBlock.PartitionBlocks, 2));

        // assert - available cylinders
        Assert.Equal(rigidDiskBlock.LoCylinder, dh0.LowCyl);
        Assert.Equal(29U, dh0.HighCyl);
        Assert.Equal(22U, dh1.LowCyl);
        Assert.Equal(59U, dh1.HighCyl);
        Assert.Equal(50U, dh2.LowCyl);
        Assert.Equal(rigidDiskBlock.HiCylinder, dh2.HighCyl);
    }
}
