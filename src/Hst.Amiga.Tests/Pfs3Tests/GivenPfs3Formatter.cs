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

    [Theory]
    [InlineData(1024 * 1024 * 16000L)] // 16GB
    [InlineData(1024 * 1024 * 32000L)] // 32GB
    [InlineData(1024 * 1024 * 64000L)] // 64GB
    // 128GB: Experimental! Size is larger than 512 sector size bytes *
    // 213021952 sectors (max sectors for super mode and reserved block size of 1K) =
    // 109067239424 bytes ~ 102.4GB
    [InlineData(1024 * 1024 * 128000L)] 
    public async Task When_FormattingLargePartition_Then_PartitionCanBeRead(long size)
    {
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        await using var stream = new BlockMemoryStream();
        
        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await System.IO.File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true, size: size)
            .WriteToStream(stream);

        // arrange - get partitions
        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");

        // assert - formatted partition contains pfs3 volume
        await using var pfs3Volume1 = await Pfs3Volume.Mount(stream, partition);
        Assert.NotNull(pfs3Volume1);
        Assert.Equal("Workbench", pfs3Volume1.Name);
    }

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