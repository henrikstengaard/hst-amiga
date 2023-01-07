namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using RigidDiskBlocks;
using Xunit;

//[Trait("Category", "PFS3")]
public class GivenPfs3Formatter
{
    private readonly byte[] pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    
    [Fact]
    public async Task WhenFormatting100MbPartitionAtStartOfHardDiskFileThenPfs3BlocksAreCreated()
    {
        var size = 100.MB().ToUniversalSize();
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        await using var stream = new BlockMemoryStream();
        
        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await System.IO.File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToStream(stream);

        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");
        
        // TODO - assert pfs3 blocks are created as expected
        Assert.NotEmpty(stream.Blocks);
    }
    
    [Fact]
    public async Task WhenFormatting1GbPartitionAtStartOfHardDiskFileThenPfs3BlocksAreCreated()
    {
        var size = 10.GB().ToUniversalSize();
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        await using var stream = new BlockMemoryStream();
        
        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await System.IO.File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToStream(stream);

        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");

        // assert - stream has block written at boot block offset 
        var rootBlockOffset = (long)partition.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors * 512;
        Assert.True(stream.Blocks.ContainsKey(rootBlockOffset));
        
        // TODO - assert pfs3 blocks are created as expected
        Assert.NotEmpty(stream.Blocks);
    }
    
    [Fact]
    public async Task WhenFormatting1GbPartitionAfter30GbInHardDiskFileThenPfs3BlocksAreCreated()
    {
        // arrange - create rigid disk block with size 64gb
        var rigidDiskBlock = RigidDiskBlock.Create(64.GB());

        // arrange - create partition 1 with size 30gb
        var partitionBlock1 =
            PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("PFS3"), "DH0", 30.GB());
        rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock1 }).ToList();

        // arrange - create partition 2 with size 1gb
        var partitionBlock2 =
            PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("PFS3"), "DH0", 1.GB());
        rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock2 }).ToList();

        // act - format second partition using pfs3 formatter
        var stream = new BlockMemoryStream();
        await Pfs3Formatter.FormatPartition(stream, partitionBlock2, "Work");

        // assert - stream has block written at boot block offset 
        var rootBlockOffset = (long)partitionBlock2.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors * 512;
        Assert.True(stream.Blocks.ContainsKey(rootBlockOffset));
    }
}