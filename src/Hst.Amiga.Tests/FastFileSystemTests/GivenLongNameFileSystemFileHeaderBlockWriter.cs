namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.Collections.Generic;
using Core.Converters;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;

public class GivenLongNameFileSystemFileHeaderBlockWriter
{
    private readonly FileHeaderBlock fileHeaderBlock = new FileHeaderBlock
    {
        HeaderKey = 1U,
        HighSeq = 2U,
        FirstData = 3U,
        DataSize = Constants.INDEX_SIZE,
        DataBlocks = new uint[Constants.INDEX_SIZE],
        Access = Constants.ACCMASK_R,
        ByteSize = 0,
        Comment = "comment for entry",
        CommentBlock = 0U,
        Date = DateTime.UtcNow.Date,
        Name = "file entry",
        RealEntry = 4U,
        NextLink = 5U,
        NextSameHash = 6U,
        Parent = 7U,
        Extension = 8U
    };
    
    [Fact]
    public void WhenWriteBlockAndReadPropertiesFromBytesThenPropertiesAreEqualToBlock()
    {
        // act - write long name file system file header block
        var blockBytes = LongNameFileSystemFileHeaderBlockWriter.Build(fileHeaderBlock, 512);

        // act - read file header block properties from block bytes
        var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
        var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var dataSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
        var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
        
        var dataBlocks = new List<uint>();
        for (var i = 0; i < Constants.HT_SIZE; i++)
        {
            dataBlocks.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.ULong)));
        }
            
        var access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x140);
        var name = blockBytes.ReadStringWithLength(0x148, Constants.LNFSNAMECMMTLEN);
        var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - name.Length + 1;
        var comment = blockBytes.ReadStringWithLength(0x148 + name.Length + 1, nameAndCommendSpaceLeft);
        var date = DateHelper.ReadDate(blockBytes, 0x1c4);
        var realEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1d4);
        var nextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1d8);
        var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f0);
        var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f4);
        var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f8);
        var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0 + (SizeOf.Long * 3));
        
        // assert - dir block properties read from block bytes are equal to dir block
        Assert.Equal(Constants.T_HEADER, type);
        Assert.Equal(fileHeaderBlock.HeaderKey, headerKey);
        Assert.Equal(fileHeaderBlock.HighSeq, highSeq);
        Assert.Equal((uint)Constants.INDEX_SIZE, dataSize);
        Assert.Equal(fileHeaderBlock.FirstData, firstData);
        Assert.Equal(fileHeaderBlock.Checksum, checksum);
        Assert.Equal(fileHeaderBlock.DataBlocks, dataBlocks);
        Assert.Equal(fileHeaderBlock.Access, access);
        Assert.Equal(fileHeaderBlock.Name, name);
        Assert.Equal(fileHeaderBlock.Comment, comment);
        Assert.Equal(fileHeaderBlock.Date, date);
        Assert.Equal(fileHeaderBlock.RealEntry, realEntry);
        Assert.Equal(fileHeaderBlock.NextLink, nextLink);
        Assert.Equal(fileHeaderBlock.NextSameHash, nextSameHash);
        Assert.Equal(fileHeaderBlock.Parent, parent);
        Assert.Equal(fileHeaderBlock.Extension, extension);
        Assert.Equal(Constants.ST_FILE, secType);
    }

    [Fact]
    public void WhenWriteAndReadBlockThenBlockIsEqual()
    {
        // act - write long name file system file header block
        var blockBytes = LongNameFileSystemFileHeaderBlockWriter.Build(fileHeaderBlock, 512);

        // act - read long name file system file header block
        var actualFileHeaderBlock = LongNameFileSystemFileHeaderBlockReader.Parse(blockBytes);

        // assert - block read is equal to block written
        Assert.Equal(Constants.T_HEADER, actualFileHeaderBlock.Type);
        Assert.Equal(fileHeaderBlock.HeaderKey, actualFileHeaderBlock.HeaderKey);
        Assert.Equal(fileHeaderBlock.HighSeq, actualFileHeaderBlock.HighSeq);
        Assert.Equal((uint)Constants.HT_SIZE, actualFileHeaderBlock.HashTableSize);
        Assert.Equal(fileHeaderBlock.FirstData, actualFileHeaderBlock.FirstData);
        Assert.Equal(fileHeaderBlock.Checksum, actualFileHeaderBlock.Checksum);
        Assert.Equal(fileHeaderBlock.HashTable, actualFileHeaderBlock.HashTable);
        Assert.Equal(fileHeaderBlock.Access, actualFileHeaderBlock.Access);
        Assert.Equal(fileHeaderBlock.Name, actualFileHeaderBlock.Name);
        Assert.Equal(fileHeaderBlock.Comment, actualFileHeaderBlock.Comment);
        Assert.Equal(fileHeaderBlock.Date, actualFileHeaderBlock.Date);
        Assert.Equal(fileHeaderBlock.RealEntry, actualFileHeaderBlock.RealEntry);
        Assert.Equal(fileHeaderBlock.NextLink, actualFileHeaderBlock.NextLink);
        Assert.Equal(fileHeaderBlock.NextSameHash, actualFileHeaderBlock.NextSameHash);
        Assert.Equal(fileHeaderBlock.Parent, actualFileHeaderBlock.Parent);
        Assert.Equal(fileHeaderBlock.Extension, actualFileHeaderBlock.Extension);
        Assert.Equal(Constants.ST_FILE, actualFileHeaderBlock.SecType);
    }
}