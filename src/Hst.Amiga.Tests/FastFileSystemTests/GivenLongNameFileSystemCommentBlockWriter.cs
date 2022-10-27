namespace Hst.Amiga.Tests.FastFileSystemTests;

using Core.Converters;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;

public class GivenLongNameFileSystemCommentBlockWriter
{
    private readonly LongNameFileSystemCommentBlock commentBlock = new LongNameFileSystemCommentBlock
    {
        OwnKey = 1U,
        HeaderKey = 2U,
        Comment = "comment for entry"
    };
    
    [Fact]
    public void WhenWriteBlockAndReadPropertiesFromBytesThenPropertiesAreEqualToBlock()
    {
        // act - write long name file system comment block
        var blockBytes = LongNameFileSystemCommentBlockWriter.Build(commentBlock, 512);

        // act - read comment block properties from block bytes
        var ownKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var comment = blockBytes.ReadStringWithLength(0x18, Constants.MAXCMMTLEN);

        // assert - comment block properties read from block bytes are equal to comment block
        Assert.Equal(commentBlock.OwnKey, ownKey);
        Assert.Equal(commentBlock.HeaderKey, headerKey);
        Assert.Equal(commentBlock.Comment, comment);
    }
    
    [Fact]
    public void WhenWriteAndReadBlockThenBlockIsEqual()
    {
        // act - write long name file system comment block
        var blockBytes = LongNameFileSystemCommentBlockWriter.Build(commentBlock, 512);

        // act - read long name file system comment block
        var actualCommentBlock = LongNameFileSystemCommentBlockReader.Parse(blockBytes);

        // assert - block read is equal to block written
        Assert.Equal(Constants.TYPE_COMMENT, actualCommentBlock.Type);
        Assert.Equal(commentBlock.OwnKey, actualCommentBlock.OwnKey);
        Assert.Equal(commentBlock.HeaderKey, actualCommentBlock.HeaderKey);
        Assert.Equal(commentBlock.Checksum, actualCommentBlock.Checksum);
        Assert.Equal(commentBlock.Comment, actualCommentBlock.Comment);
    }
}