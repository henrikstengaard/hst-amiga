namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems.SmartFileSystem;
using Xunit;
using Disk = FileSystems.SmartFileSystem.Disk;

public class GivenSmartFileSystemFormatter : SmartFileSystemTestBase
{
    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task WhenFormattingPartitionThenBlocksAreCreatedAtExpectedLocations(int blockSize)
    {
        // arrange - format partition
        var stream = await CreateSmartFileSystemFormattedDisk(fileSystemBlockSize: blockSize);
        var partitionBlock = await ReadPartitionBlock(stream);

        // act - mount volume
        var volume = await SmartFileSystemHelper.Mount(stream, partitionBlock);

        // assert - blocks are created at locations used by smart file system format
        var reserved = System.Math.Max(partitionBlock.Reserved, 1);
        var rootBlock = volume.RootBlock;
        Assert.Equal(Constants.StructureVersion, rootBlock.Version);
        Assert.Equal((uint)blockSize, rootBlock.BlockSize);
        Assert.Equal(volume.TotalBlocks, rootBlock.TotalBlocks);
        Assert.Equal(reserved, rootBlock.AdminSpaceContainer);
        Assert.Equal(reserved + 1, rootBlock.RootObjectContainer);
        Assert.Equal(reserved + 4, rootBlock.ExtentBNodeRoot);
        Assert.Equal(reserved + 5, rootBlock.ObjectNodeRoot);
        Assert.Equal(reserved + 32, rootBlock.BitmapBase);
        Assert.True(rootBlock.Recycled);
        Assert.False(rootBlock.CaseSensitive);

        var firstByte = (ulong)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
        Assert.Equal(firstByte, rootBlock.FirstByte);

        // assert - transaction ok block
        Disk.CheckBlock(await Disk.ReadBlock(volume, reserved + 3), reserved + 3, Constants.TransactionOkId);

        // assert - root object container contains root directory with volume name
        var rootContainer = await Disk.ReadObjectContainer(volume, rootBlock.RootObjectContainer);
        var root = Assert.Single(rootContainer.Objects);
        Assert.Equal("Workbench", root.Name);
        Assert.Equal(Constants.RootNode, root.ObjectNode);
        Assert.True(root.IsDir);
        Assert.Equal(reserved + 2, root.HashTable);
        Assert.Equal(reserved + 6, root.FirstDirBlock);
        Assert.Equal(volume.FreeBlocks, rootContainer.RootInfo.FreeBlocks);

        // assert - recycled directory is hidden and listed in root hash table
        var recycled = (await Objects.ReadObject(volume, Constants.RecycledNode)).Object;
        Assert.Equal(".recycled", recycled.Name);
        Assert.Equal(0x87, recycled.Bits);
        Assert.Equal(0x0cU, recycled.Protection);
        var hashTable = await Disk.ReadHashTable(volume, root.HashTable);
        Assert.Equal(Constants.RecycledNode,
            hashTable.HashEntries[HashHelper.HashChain(HashHelper.Hash(".recycled", false), blockSize)]);

        // assert - free blocks
        var bitmapBlocks = Bitmap.CalculateBitmapBlocks(volume.TotalBlocks, blockSize);
        var expectedFree = volume.TotalBlocks - reserved - 32 - (uint)bitmapBlocks -
                           System.Math.Max(partitionBlock.PreAlloc, 1);
        Assert.Equal(expectedFree, volume.FreeBlocks);

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenFormattingPartitionCaseSensitiveWithoutRecycledThenRootBitsAreSet()
    {
        // arrange - format partition
        var stream = await CreateSmartFileSystemFormattedDisk(caseSensitive: true, recycled: false);

        // act - mount volume
        await using var sfsVolume = await MountVolume(stream);

        // assert
        Assert.True(sfsVolume.Volume.RootBlock.CaseSensitive);
        Assert.False(sfsVolume.Volume.RootBlock.Recycled);
        Assert.Empty(await sfsVolume.ListRawEntries());
        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenBlockIsMarkedUsedWithoutBeingReferencedThenVerifierReportsError()
    {
        // arrange - format partition and mark a free block used without referencing it
        var stream = await CreateSmartFileSystemFormattedDisk();
        var partitionBlock = await ReadPartitionBlock(stream);
        var volume = await SmartFileSystemHelper.Mount(stream, partitionBlock);
        var block = Bitmap.FindSpace(volume, 1, volume.RovingPointer, volume.TotalBlocks).Value;
        await Bitmap.MarkUsed(volume, block, 1);

        // act
        var errors = await SmartFileSystemVerifier.Verify(volume, partitionBlock.PreAlloc);

        // assert
        Assert.Contains(errors, x => x.Contains($"Block {block} is used in bitmap"));
    }

    [Fact]
    public async Task WhenMountingFormattedVolumeThenVolumeIsEmpty()
    {
        // arrange - format partition
        var stream = await CreateSmartFileSystemFormattedDisk();

        // act - mount volume
        await using var volume = await MountVolume(stream);

        // assert - volume is empty, only hidden recycled directory exists
        Assert.Equal("Workbench", volume.Name);
        Assert.Empty(await volume.ListEntries());
        Assert.Single(await volume.ListRawEntries());
        Assert.True(volume.Free > 0);
        Assert.True(volume.Size > volume.Free);
        Assert.Equal("/", await volume.GetCurrentPath());
    }
}
