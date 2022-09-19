namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using RigidDiskBlocks;
using Xunit;

public class GivenPfs3Formatter
{
    private readonly byte[] pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    
    [Fact]
    public async Task WhenFormatting100MbHardDiskFileThenPfs3BlocksAreCreated()
    {
        var size = 100.MB().ToUniversalSize();
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        await using var stream = new BlockMemoryStream();
        
        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToStream(stream);

        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");
        
        // TODO - assert pfs3 blocks are created as expected
        Assert.NotEmpty(stream.Blocks);
    }    
    
    [Fact]
    public async Task WhenFormatting10GbHardDiskFileThenPfs3BlocksAreCreated()
    {
        var size = 10.GB().ToUniversalSize();
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        await using var stream = new BlockMemoryStream();
        
        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToStream(stream);

        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");
        
        // TODO - assert pfs3 blocks are created as expected
        Assert.NotEmpty(stream.Blocks);
    }    
}