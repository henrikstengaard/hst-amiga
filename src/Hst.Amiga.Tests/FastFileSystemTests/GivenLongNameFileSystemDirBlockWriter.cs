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
    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void WhenWriteBlockAndReadPropertiesFromBytesThenPropertiesAreEqualToBlock(int blockSize)
    {
        // arrange - create dir block
        var dirBlock = CreateDirBlock(blockSize);
        
        // act - write long name file system dir block
        var blockBytes = LongNameFileSystemDirBlockWriter.Build(dirBlock, blockSize);

        // act - read dir block properties from block bytes
        var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
        var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var hashTableSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
        var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
        
        var hashTable = new List<uint>();
        for (var i = 0; i < hashTableSize; i++)
        {
            hashTable.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.ULong)));
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
        Assert.Equal(dirBlock.HeaderKey, headerKey);
        Assert.Equal(dirBlock.HighSeq, highSeq);
        Assert.Equal(expectedDataSize, hashTableSize);
        Assert.Equal(dirBlock.FirstData, firstData);
        Assert.Equal(dirBlock.Checksum, checksum);
        Assert.Equal(dirBlock.HashTable, hashTable);
        Assert.Equal(dirBlock.Access, access);
        Assert.Equal(dirBlock.ByteSize, byteSize);
        Assert.Equal(dirBlock.Name, name);
        Assert.Equal(dirBlock.Comment, comment);
        Assert.Equal(dirBlock.CommentBlock, commentBlock);
        Assert.Equal(dirBlock.Date, date);
        Assert.Equal(dirBlock.RealEntry, realEntry);
        Assert.Equal(dirBlock.NextLink, nextLink);
        Assert.Equal(dirBlock.NextSameHash, nextSameHash);
        Assert.Equal(dirBlock.Parent, parent);
        Assert.Equal(dirBlock.Extension, extension);
        Assert.Equal(Constants.ST_DIR, secType);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void WhenWriteAndReadBlockThenBlockIsEqual(int blockSize)
    {
        // arrange - create dir block
        var dirBlock = CreateDirBlock(blockSize);
        
        // act - write long name file system dir block
        var blockBytes = LongNameFileSystemDirBlockWriter.Build(dirBlock, blockSize);

        // act - read long name file system dir block
        var actualDirBlock = LongNameFileSystemDirBlockReader.Read(blockBytes);

        // assert - block read is equal to block written
        var expectedDataSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
        Assert.Equal(Constants.T_HEADER, actualDirBlock.Type);
        Assert.Equal(dirBlock.HeaderKey, actualDirBlock.HeaderKey);
        Assert.Equal(dirBlock.HighSeq, actualDirBlock.HighSeq);
        Assert.Equal(expectedDataSize, actualDirBlock.HashTableSize);
        Assert.Equal(dirBlock.FirstData, actualDirBlock.FirstData);
        Assert.Equal(dirBlock.Checksum, actualDirBlock.Checksum);
        Assert.Equal(dirBlock.HashTable, actualDirBlock.HashTable);
        Assert.Equal(dirBlock.Access, actualDirBlock.Access);
        Assert.Equal(dirBlock.ByteSize, actualDirBlock.ByteSize);
        Assert.Equal(dirBlock.Name, actualDirBlock.Name);
        Assert.Equal(dirBlock.Comment, actualDirBlock.Comment);
        Assert.Equal(dirBlock.CommentBlock, actualDirBlock.CommentBlock);
        Assert.Equal(dirBlock.Date, actualDirBlock.Date);
        Assert.Equal(dirBlock.RealEntry, actualDirBlock.RealEntry);
        Assert.Equal(dirBlock.NextLink, actualDirBlock.NextLink);
        Assert.Equal(dirBlock.NextSameHash, actualDirBlock.NextSameHash);
        Assert.Equal(dirBlock.Parent, actualDirBlock.Parent);
        Assert.Equal(dirBlock.Extension, actualDirBlock.Extension);
        Assert.Equal(Constants.ST_DIR, actualDirBlock.SecType);
    }
    
    private static DirBlock CreateDirBlock(int blockSize) => new DirBlock(blockSize)
    {
        HeaderKey = 1U,
        HighSeq = 2U,
        FirstData = 3U,
        Access = Constants.ACCMASK_R,
        ByteSize = 0,
        Comment = "comment for entry",
        CommentBlock = 9U,
        Date = DateTime.UtcNow.Date,
        Name = "dir entry",
        RealEntry = 4U,
        NextLink = 5U,
        NextSameHash = 6U,
        Parent = 7U,
        Extension = 8U
    };
}