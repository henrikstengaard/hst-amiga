namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Threading.Tasks;
using FileSystems.Exceptions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using RigidDiskBlocks;
using Xunit;
using Disk = Disk;

public class GivenAdfFileWithNoBlocksFree
{
    private static async Task<Stream> CreateAdfStream()
    {
        const string dosType = "DOS3";
        const string diskName = "ADF";

        // arrange - adf stream
        var stream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);

        // act - format adf
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl,
            FloppyDiskConstants.DoubleDensity.ReservedBlocks, FloppyDiskConstants.DoubleDensity.Heads,
            FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.FileSystemBlockSize,
            DosTypeHelper.FormatDosType(dosType), diskName);

        // arrange - create bitmap block with block 879 free and all other blocks used
        var bitmapBlock = new BitmapBlock(FloppyDiskConstants.BlockSize)
        {
            Offset = 881,
            Map = new uint[127]
        };
        //bitmapBlock.Map[27] = 8192;
            
        // arrange - write bitmap block at offset 881
        stream.Seek(
            (long)bitmapBlock.Offset * FloppyDiskConstants.BlockSize, SeekOrigin.Begin);
        await Disk.WriteBlock(stream, BitmapBlockBuilder.Build(bitmapBlock, FloppyDiskConstants.BlockSize));

        return stream;
    }
        
    [Fact]
    public async Task WhenCreateDirectoryThenDiskFullExceptionIsThrown()
    {
        // arrange - create adf stream
        var stream = await CreateAdfStream();
            
        // arrange - mount adf volume
        await using var volume = await FastFileSystemVolume.MountAdf(stream);
            
        // act - create new file
        await Assert.ThrowsAsync<DiskFullException>(async () => await volume.CreateDirectory("NewDir"));
    }
        
    [Fact]
    public async Task WhenCreateFileThenDiskFullExceptionIsThrown()
    {
        // arrange - create adf stream
        var stream = await CreateAdfStream();
            
        // arrange - mount adf volume
        await using var volume = await FastFileSystemVolume.MountAdf(stream);
            
        // act - create new file
        await Assert.ThrowsAsync<DiskFullException>(async () => await volume.CreateFile("NewFile"));
    }
}