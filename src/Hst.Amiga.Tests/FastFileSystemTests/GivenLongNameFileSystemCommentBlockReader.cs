namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;
using File = System.IO.File;

public class GivenLongNameFileSystemCommentBlockReader
{
    [Theory]
    [InlineData("dos7_comment-block-1.bin")]
    [InlineData("dos7_bs1024_comment-block-1.bin")]
    public async Task WhenReadBlockWithFileCommentThenCommentIsEqual(string blockFilename)
    {
        // arrange - read long filename file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system comment block
        var longNameFileSystemCommentBlock = LongNameFileSystemCommentBlockReader.Parse(blockBytes);

        // assert - long name file system comment block is equal
        Assert.Equal(Constants.TYPE_COMMENT, longNameFileSystemCommentBlock.Type);
        Assert.NotEqual(0U, longNameFileSystemCommentBlock.OwnKey);
        Assert.NotEqual(0U, longNameFileSystemCommentBlock.HeaderKey);
        Assert.Equal("1234567890123456789012345678901234567890123456789012345678901234567890123456789",
            longNameFileSystemCommentBlock.Comment);
    }

    [Theory]
    [InlineData("dos7_comment-block-2.bin")]
    [InlineData("dos7_bs1024_comment-block-2.bin")]
    public async Task WhenReadBlockWithDirCommentThenCommentIsEqual(string blockFilename)
    {
        // arrange - read long filename file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system comment block
        var longNameFileSystemCommentBlock = LongNameFileSystemCommentBlockReader.Parse(blockBytes);

        // assert - long name file system comment block is equal
        Assert.Equal(Constants.TYPE_COMMENT, longNameFileSystemCommentBlock.Type);
        Assert.NotEqual(0U, longNameFileSystemCommentBlock.OwnKey);
        Assert.NotEqual(0U, longNameFileSystemCommentBlock.HeaderKey);
        Assert.Equal("dir-comment", longNameFileSystemCommentBlock.Comment);
    }
}