namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using RigidDiskBlocks;
using Xunit;
using Disk = Disk;

public class GivenAdfFileWithBitmapBitsSetAfterLastBlock
{
    private const int FreeBlocks = 3;

    private static async Task<Stream> CreateAdfStream()
    {
        // arrange - adf stream
        var stream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);

        // arrange - format adf
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl,
            FloppyDiskConstants.DoubleDensity.ReservedBlocks, FloppyDiskConstants.DoubleDensity.Heads,
            FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.FileSystemBlockSize,
            DosTypeHelper.FormatDosType("DOS1"), "Workbench");

        // arrange - create bitmap block with 3 blocks free and all bits after the last block (1759) set,
        // like the Workbench 3.1 floppy disk. bitmap starts at block 2, so block 1759 is bit 1757 (map entry 54, bit 29)
        var bitmapBlock = new BitmapBlock(FloppyDiskConstants.BlockSize)
        {
            Offset = 881,
            Map = new uint[127]
        };
        bitmapBlock.Map[10] = 0x7;
        bitmapBlock.Map[54] = 0xc0000000;
        for (var i = 55; i < bitmapBlock.Map.Length; i++)
        {
            bitmapBlock.Map[i] = 0xffffffff;
        }

        // arrange - write bitmap block at offset 881
        stream.Seek((long)bitmapBlock.Offset * FloppyDiskConstants.BlockSize, SeekOrigin.Begin);
        await Disk.WriteBlock(stream, BitmapBlockBuilder.Build(bitmapBlock, FloppyDiskConstants.BlockSize));

        return stream;
    }

    [Fact]
    public async Task WhenGetFreeThenOnlyBlocksInVolumeAreCounted()
    {
        // arrange - create adf stream
        var stream = await CreateAdfStream();

        // act - mount adf volume
        await using var volume = await FastFileSystemVolume.MountAdf(stream);

        // assert - free is the free blocks in the volume
        Assert.Equal(FreeBlocks * FloppyDiskConstants.FileSystemBlockSize, volume.Free);
        Assert.Equal(FloppyDiskConstants.DoubleDensity.Size, volume.Size);
    }
}
