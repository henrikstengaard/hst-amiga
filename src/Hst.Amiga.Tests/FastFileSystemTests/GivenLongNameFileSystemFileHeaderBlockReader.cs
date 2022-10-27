namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Threading.Tasks;
using Extensions;
using FileSystems.FastFileSystem.Blocks;
using Xunit;
using File = System.IO.File;

public class GivenLongNameFileSystemFileHeaderBlockReader
{
    [Fact]
    public async Task WhenReadBlockWithNameAndCommentThenNameAndCommentAreEqual()
    {
        // arrange - read long name file system file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems",
                "dos7_file-header-block-1.bin"));

        // act - read long name file system file header block
        var longNameFileSystemFileHeader = LongNameFileSystemFileHeaderBlockReader.Parse(blockBytes);

        // assert - long name file system file header block is equal
        Assert.NotEqual(0U, longNameFileSystemFileHeader.HeaderKey);
        Assert.NotEqual(0U, longNameFileSystemFileHeader.HighSeq);
        Assert.Equal(0U, longNameFileSystemFileHeader.DataSize);
        Assert.NotEqual(0U, longNameFileSystemFileHeader.FirstData);
        Assert.Equal(14U, longNameFileSystemFileHeader.ByteSize);
        Assert.Equal("comment1", longNameFileSystemFileHeader.Comment);
        Assert.Equal(0U, longNameFileSystemFileHeader.CommentBlock);
        Assert.Equal("12345678901234567890123456789012345678901234567890.txt", longNameFileSystemFileHeader.Name);
        Assert.Equal(new DateTime(2022, 10, 25, 20, 56, 37, DateTimeKind.Utc),
            longNameFileSystemFileHeader.Date.Trim(TimeSpan.TicksPerSecond));
    }

    [Fact]
    public async Task WhenReadBlockWithLongNameAndNoCommentThenNameIsEqual()
    {
        // arrange - read long name file system file header block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems",
                "dos7_file-header-block-2.bin"));

        // act - read long name file system file header block
        var longNameFileSystemFileHeader = LongNameFileSystemFileHeaderBlockReader.Parse(blockBytes);

        // assert - long name file system file header block is equal
        Assert.NotEqual(0U, longNameFileSystemFileHeader.HeaderKey);
        Assert.NotEqual(0U, longNameFileSystemFileHeader.HighSeq);
        Assert.Equal(0U, longNameFileSystemFileHeader.DataSize);
        Assert.NotEqual(0U, longNameFileSystemFileHeader.FirstData);
        Assert.Equal(18U, longNameFileSystemFileHeader.ByteSize);
        Assert.Equal(string.Empty, longNameFileSystemFileHeader.Comment);
        Assert.NotEqual(0U, longNameFileSystemFileHeader.CommentBlock);
        Assert.Equal(
            "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890.txt",
            longNameFileSystemFileHeader.Name);
        Assert.Equal(new DateTime(2022, 10, 26, 14, 54, 45, DateTimeKind.Utc),
            longNameFileSystemFileHeader.Date.Trim(TimeSpan.TicksPerSecond));
    }
}