namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.Collections.Generic;
using Core.Converters;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;

public class GivenLongNameEntryBlockWriterWithFileBlocks
{
    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void WhenWriteBlockAndReadPropertiesFromBytesThenPropertiesAreEqualToBlock(int blockSize)
    {
        // arrange - create file header block
        var fileEntryBlock = CreateFileEntryBlock(blockSize);
        
        // act - write long name file system file header block
        var blockBytes = LongNameEntryBlockWriter.Build(fileEntryBlock, blockSize);

        // act - read file header block properties from block bytes
        var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
        var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var dataSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
        var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
        
        var dataBlocks = new List<uint>();
        for (var i = 0; i < dataSize; i++)
        {
            dataBlocks.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.ULong)));
        }
            
        var access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc0);
        var byteSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xbc);
        var name = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8, Constants.LNFSNAMECMMTLEN);
        var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - name.Length + 1;
        var comment = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8 + name.Length + 1, nameAndCommendSpaceLeft);
        var commentBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x48);
        var date = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x3c);
        var realEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x2c);
        var nextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x28);
        var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x10);
        var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc);
        var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x8);
        var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x4);
        
        // assert - dir block properties read from block bytes are equal to dir block
        var expectedDataSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
        Assert.Equal(Constants.T_HEADER, type);
        Assert.Equal(fileEntryBlock.HeaderKey, headerKey);
        Assert.Equal(fileEntryBlock.HighSeq, highSeq);
        Assert.Equal(expectedDataSize, dataSize);
        Assert.Equal(fileEntryBlock.FirstData, firstData);
        Assert.Equal(fileEntryBlock.Checksum, checksum);
        Assert.Equal(fileEntryBlock.DataBlocks, dataBlocks);
        Assert.Equal(fileEntryBlock.Access, access);
        Assert.Equal(fileEntryBlock.ByteSize, byteSize);
        Assert.Equal(fileEntryBlock.Name, name);
        Assert.Equal(fileEntryBlock.Comment, comment);
        Assert.Equal(fileEntryBlock.CommentBlock, commentBlock);
        Assert.Equal(fileEntryBlock.Date, date);
        Assert.Equal(fileEntryBlock.RealEntry, realEntry);
        Assert.Equal(fileEntryBlock.NextLink, nextLink);
        Assert.Equal(fileEntryBlock.NextSameHash, nextSameHash);
        Assert.Equal(fileEntryBlock.Parent, parent);
        Assert.Equal(fileEntryBlock.Extension, extension);
        Assert.Equal(Constants.ST_FILE, secType);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void WhenWriteAndReadBlockThenBlockIsEqual(int blockSize)
    {
        // arrange - create file header block
        var fileHeaderBlock = CreateFileEntryBlock(blockSize);
        
        // act - write long name file system file header block
        var blockBytes = LongNameEntryBlockWriter.Build(fileHeaderBlock, blockSize);

        // act - read long name file system file header block
        var actualEntryBlock = LongNameEntryBlockReader.Read(blockBytes);

        // assert - block read is equal to block written
        var expectedDataSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
        Assert.Equal(Constants.T_HEADER, actualEntryBlock.Type);
        Assert.Equal(fileHeaderBlock.HeaderKey, actualEntryBlock.HeaderKey);
        Assert.Equal(fileHeaderBlock.HighSeq, actualEntryBlock.HighSeq);
        Assert.Equal(expectedDataSize, actualEntryBlock.HashTableSize);
        Assert.Equal(fileHeaderBlock.FirstData, actualEntryBlock.FirstData);
        Assert.Equal(fileHeaderBlock.Checksum, actualEntryBlock.Checksum);
        Assert.Equal(fileHeaderBlock.HashTable, actualEntryBlock.HashTable);
        Assert.Equal(fileHeaderBlock.Access, actualEntryBlock.Access);
        Assert.Equal(fileHeaderBlock.ByteSize, actualEntryBlock.ByteSize);
        Assert.Equal(fileHeaderBlock.Name, actualEntryBlock.Name);
        Assert.Equal(fileHeaderBlock.Comment, actualEntryBlock.Comment);
        Assert.Equal(fileHeaderBlock.CommentBlock, actualEntryBlock.CommentBlock);
        Assert.Equal(fileHeaderBlock.Date, actualEntryBlock.Date);
        Assert.Equal(fileHeaderBlock.RealEntry, actualEntryBlock.RealEntry);
        Assert.Equal(fileHeaderBlock.NextLink, actualEntryBlock.NextLink);
        Assert.Equal(fileHeaderBlock.NextSameHash, actualEntryBlock.NextSameHash);
        Assert.Equal(fileHeaderBlock.Parent, actualEntryBlock.Parent);
        Assert.Equal(fileHeaderBlock.Extension, actualEntryBlock.Extension);
        Assert.Equal(Constants.ST_FILE, actualEntryBlock.SecType);
    }
    
    private static EntryBlock CreateFileEntryBlock(int blockSize) => new EntryBlock(blockSize)
    {
        HeaderKey = 1U,
        HighSeq = 2U,
        FirstData = 3U,
        Access = Constants.ACCMASK_R,
        ByteSize = 9U,
        Comment = "comment for entry",
        CommentBlock = 0U,
        Date = DateTime.UtcNow.Date,
        Name = "file entry",
        RealEntry = 4U,
        NextLink = 5U,
        NextSameHash = 6U,
        Parent = 7U,
        Extension = 8U,
        SecType = Constants.ST_FILE
    };
}