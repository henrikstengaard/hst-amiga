namespace Hst.Amiga.Tests.RigidDiskBlockTests;

using System.IO;
using System.Threading.Tasks;
using RigidDiskBlocks;
using Xunit;

public class GivenPartitionBlockReaderReader : RigidDiskBlockTestBase
{
    [Fact]
    public async Task WhenParseExistingBlockBytesThenPartitionBlockIsNotNull()
    {
        // arrange - read partition block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "part_block.bin"));

        // act - parse partition block bytes
        var partitionBlock = await PartitionBlockReader.Parse(blockBytes, 512);

        // assert - partition block is not null
        Assert.NotNull(partitionBlock);
    }
}