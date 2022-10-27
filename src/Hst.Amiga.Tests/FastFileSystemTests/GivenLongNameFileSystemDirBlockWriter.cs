namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.Collections.Generic;
using Core.Converters;
using Extensions;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using Xunit;

public class GivenLongNameFileSystemDirBlockWriter
{
    private readonly DirBlock dirBlock = new DirBlock
    {
        HeaderKey = 1U,
        HighSeq = 2U,
        FirstData = 3U,
        HashTableSize = Constants.HT_SIZE,
        HashTable = new uint[Constants.HT_SIZE],
        Access = Constants.ACCMASK_R,
        ByteSize = 0,
        Comment = "comment for entry",
        CommentBlock = 0U,
        Date = DateTime.UtcNow.Date,
        Name = "dir entry",
        RealEntry = 4U,
        NextLink = 5U,
        NextSameHash = 6U,
        Parent = 7U,
        Extension = 8U
    };
    
    [Fact]
    public void WhenWriteBlockAndReadPropertiesFromBytesThenPropertiesAreEqualToBlock()
    {
        // act - write long name file system dir block
        var blockBytes = LongNameFileSystemDirBlockWriter.Build(dirBlock, 512);

        // act - read dir block properties from block bytes
        var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
        var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var hashTableSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
        var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
        
        var hashTable = new List<uint>();
        for (var i = 0; i < Constants.HT_SIZE; i++)
        {
            hashTable.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.ULong)));
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
        Assert.Equal(dirBlock.HeaderKey, headerKey);
        Assert.Equal(dirBlock.HighSeq, highSeq);
        Assert.Equal((uint)Constants.HT_SIZE, hashTableSize);
        Assert.Equal(dirBlock.FirstData, firstData);
        Assert.Equal(dirBlock.Checksum, checksum);
        Assert.Equal(dirBlock.HashTable, hashTable);
        Assert.Equal(dirBlock.Access, access);
        Assert.Equal(dirBlock.Name, name);
        Assert.Equal(dirBlock.Comment, comment);
        Assert.Equal(dirBlock.Date, date);
        Assert.Equal(dirBlock.RealEntry, realEntry);
        Assert.Equal(dirBlock.NextLink, nextLink);
        Assert.Equal(dirBlock.NextSameHash, nextSameHash);
        Assert.Equal(dirBlock.Parent, parent);
        Assert.Equal(dirBlock.Extension, extension);
        Assert.Equal(Constants.ST_DIR, secType);
    }

    [Fact]
    public void WhenWriteAndReadBlockThenBlockIsEqual()
    {
        // act - write long name file system dir block
        var blockBytes = LongNameFileSystemDirBlockWriter.Build(dirBlock, 512);

        // act - read long name file system dir block
        var actualDirBlock = LongNameFileSystemDirBlockReader.Read(blockBytes);

        // assert - block read is equal to block written
        Assert.Equal(Constants.T_HEADER, actualDirBlock.Type);
        Assert.Equal(dirBlock.HeaderKey, actualDirBlock.HeaderKey);
        Assert.Equal(dirBlock.HighSeq, actualDirBlock.HighSeq);
        Assert.Equal((uint)Constants.HT_SIZE, actualDirBlock.HashTableSize);
        Assert.Equal(dirBlock.FirstData, actualDirBlock.FirstData);
        Assert.Equal(dirBlock.Checksum, actualDirBlock.Checksum);
        Assert.Equal(dirBlock.HashTable, actualDirBlock.HashTable);
        Assert.Equal(dirBlock.Access, actualDirBlock.Access);
        Assert.Equal(dirBlock.Name, actualDirBlock.Name);
        Assert.Equal(dirBlock.Comment, actualDirBlock.Comment);
        Assert.Equal(dirBlock.Date, actualDirBlock.Date);
        Assert.Equal(dirBlock.RealEntry, actualDirBlock.RealEntry);
        Assert.Equal(dirBlock.NextLink, actualDirBlock.NextLink);
        Assert.Equal(dirBlock.NextSameHash, actualDirBlock.NextSameHash);
        Assert.Equal(dirBlock.Parent, actualDirBlock.Parent);
        Assert.Equal(dirBlock.Extension, actualDirBlock.Extension);
        Assert.Equal(Constants.ST_DIR, actualDirBlock.SecType);
    }
}