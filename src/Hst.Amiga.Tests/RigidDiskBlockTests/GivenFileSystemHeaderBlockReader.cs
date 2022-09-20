namespace Hst.Amiga.Tests.RigidDiskBlockTests;

using System.IO;
using System.Threading.Tasks;
using RigidDiskBlocks;
using Xunit;

public class GivenFileSystemHeaderBlockReader : RigidDiskBlockTestBase
{
    [Fact]
    public async Task WhenParseExistingBlockBytesThenFileSystemHeaderBlockIsNotNull()
    {
        // arrange - read file system header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "fshd_block.bin"));

        // act - parse file system header block bytes
        var fileSystemHeaderBlock = await FileSystemHeaderBlockReader.Parse(blockBytes);

        // assert - file system header is not null
        Assert.NotNull(fileSystemHeaderBlock);
    }
}