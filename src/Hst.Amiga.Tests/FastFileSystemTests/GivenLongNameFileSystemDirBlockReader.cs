﻿namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Threading.Tasks;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;
using File = System.IO.File;

public class GivenLongNameFileSystemDirBlockReader
{
    [Theory]
    [InlineData("dos7_dir-block-1.bin")]
    [InlineData("dos7_bs1024_dir-block-1.bin")]
    public async Task WhenReadBlockWithNameAndCommentThenNameAndCommentAreEqual(string blockFilename)
    {
        // arrange - read long name file system dir block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system dir block
        var longNameFileSystemDirBlock = LongNameFileSystemDirBlockReader.Read(blockBytes);

        // assert - long name file system file header block is equal
        var expectedIndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
        Assert.NotEqual(0U, longNameFileSystemDirBlock.HeaderKey);
        Assert.Equal(0U, longNameFileSystemDirBlock.HighSeq);
        Assert.Equal(expectedIndexSize, longNameFileSystemDirBlock.DataSize);
        Assert.Equal(0U, longNameFileSystemDirBlock.FirstData);
        Assert.Equal(0U, longNameFileSystemDirBlock.ByteSize);
        Assert.Equal("dir-comment1", longNameFileSystemDirBlock.Comment);
        Assert.Equal(0U, longNameFileSystemDirBlock.CommentBlock);
        Assert.Equal("DirectoryWithLongName123456789012345678901234567890", longNameFileSystemDirBlock.Name);
        Assert.Equal(new DateTime(2022, 10, 26, 13, 45, 13, DateTimeKind.Utc),
            longNameFileSystemDirBlock.Date.Trim(TimeSpan.TicksPerSecond));
    }

    [Theory]
    [InlineData("dos7_dir-block-2.bin")]
    [InlineData("dos7_bs1024_dir-block-2.bin")]
    public async Task WhenReadBlockWithLongNameAndNoCommentThenNameIsEqual(string blockFilename)
    {
        // arrange - read long name file system dir block bytes
        var blockBytes =
            await File.ReadAllBytesAsync(Path.Combine("TestData", "FastFileSystems", blockFilename));

        // act - read long name file system dir block
        var longNameFileSystemDirBlock = LongNameFileSystemDirBlockReader.Read(blockBytes);
        
        // assert - long name file system file header block is equal
        var expectedIndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
        Assert.NotEqual(0U, longNameFileSystemDirBlock.HeaderKey);
        Assert.Equal(0U, longNameFileSystemDirBlock.HighSeq);
        Assert.Equal(expectedIndexSize, longNameFileSystemDirBlock.DataSize);
        Assert.Equal(0U, longNameFileSystemDirBlock.FirstData);
        Assert.Equal(0U, longNameFileSystemDirBlock.ByteSize);
        Assert.Equal(string.Empty, longNameFileSystemDirBlock.Comment);
        Assert.NotEqual(0U, longNameFileSystemDirBlock.CommentBlock);
        Assert.Equal(
            "2ndDirectoryWithLongName123456789012345678901234567890123333333-------444444444444444444444444445555",
            longNameFileSystemDirBlock.Name);
        Assert.Equal(new DateTime(2022, 10, 26, 23, 55, 20, DateTimeKind.Utc),
            longNameFileSystemDirBlock.Date.Trim(TimeSpan.TicksPerSecond));
    }
}