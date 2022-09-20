namespace Hst.Amiga.Tests.RigidDiskBlockTests;

using System.IO;
using System.Threading.Tasks;
using RigidDiskBlocks;
using Xunit;

public class GivenLoadSegBlockReader : RigidDiskBlockTestBase
{
    [Theory]
    [InlineData("lseg_block.bin")]
    [InlineData("lseg_last_block.bin")]
    public async Task WhenParseExistingBlockBytesThenLoadSegBlockIsNotNull(string fileName)
    {
        // arrange - read load seg block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", fileName));

        // act - parse load seg block bytes
        var loadSegBlock = await LoadSegBlockReader.Parse(blockBytes);

        // assert - load seg block is not null
        Assert.NotNull(loadSegBlock);
    }
}