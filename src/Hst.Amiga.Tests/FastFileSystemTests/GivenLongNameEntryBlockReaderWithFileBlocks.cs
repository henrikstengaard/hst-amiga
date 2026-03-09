namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Threading.Tasks;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;
using File = System.IO.File;

public class GivenLongNameEntryBlockReaderWithFileBlocks
{
    [Theory]
    [InlineData("dos7_file-header-block-1.bin")]
    [InlineData("dos7_bs1024_file-header-block-1.bin")]
    public async Task WhenReadBlockWithNameAndCommentThenNameAndCommentAreEqual(string blockFilename)
    {
        // arrange - read long name file system file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system file header block
        var entryBlock = LongNameEntryBlockReader.Read(blockBytes);

        // assert - long name file system file header block is equal
        var expectedIndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
        Assert.NotEqual(0U, entryBlock.HeaderKey);
        Assert.NotEqual(0U, entryBlock.HighSeq);
        Assert.Equal(expectedIndexSize, entryBlock.DataSize);
        Assert.NotEqual(0U, entryBlock.FirstData);
        Assert.Equal(14U, entryBlock.ByteSize);
        Assert.Equal("comment1", entryBlock.Comment);
        Assert.Equal(0U, entryBlock.CommentBlock);
        Assert.Equal("12345678901234567890123456789012345678901234567890.txt", entryBlock.Name);
        Assert.Equal(new DateTime(2022, 10, 25, 20, 56, 37, DateTimeKind.Utc),
            entryBlock.Date.Trim(TimeSpan.TicksPerSecond));
    }

    [Theory]
    [InlineData("dos7_file-header-block-2.bin")]
    [InlineData("dos7_bs1024_file-header-block-2.bin")]
    public async Task WhenReadBlockWithLongNameAndNoCommentThenNameIsEqual(string blockFilename)
    {
        // arrange - read long name file system file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system file header block
        var entryBlock = LongNameEntryBlockReader.Read(blockBytes);

        // assert - long name file system file header block is equal
        var expectedIndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
        Assert.NotEqual(0U, entryBlock.HeaderKey);
        Assert.NotEqual(0U, entryBlock.HighSeq);
        Assert.Equal(expectedIndexSize, entryBlock.DataSize);
        Assert.NotEqual(0U, entryBlock.FirstData);
        Assert.Equal(18U, entryBlock.ByteSize);
        Assert.Equal(string.Empty, entryBlock.Comment);
        Assert.NotEqual(0U, entryBlock.CommentBlock);
        Assert.Equal(
            "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890.txt",
            entryBlock.Name);
        Assert.Equal(new DateTime(2022, 10, 26, 14, 54, 45, DateTimeKind.Utc),
            entryBlock.Date.Trim(TimeSpan.TicksPerSecond));
    }
}