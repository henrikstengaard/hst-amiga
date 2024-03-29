﻿namespace Hst.Amiga.Tests;

using System;
using System.IO;
using System.Linq;
using Xunit;

public class GivenBlockMemoryStream
{
    [Fact]
    public void WhenWriteDataOver2BlocksAndReadBlocksThenDataIsEqualAndPositionIsIncreasedByCountRead()
    {
        // arrange - block bytes to write
        var blockBytes = new byte[1024];
        for (var i = 0; i < blockBytes.Length; i++)
        {
            blockBytes[i] = (byte)(i < 512 ? 1 : 2);
        }

        // arrange - block memory stream
        var stream = new BlockMemoryStream();
        stream.Write(blockBytes, 0, blockBytes.Length);
        
        // act - read first block bytes
        stream.Seek(0, SeekOrigin.Begin);
        var actualBlockBytes = new byte[512];
        var bytesRead = stream.Read(actualBlockBytes, 0, actualBlockBytes.Length);

        // assert - first block bytes area read
        Assert.Equal(512, bytesRead);
        Assert.Equal(blockBytes.Take(512), actualBlockBytes);

        // assert - stream position is equal to second block position dividable by block size
        Assert.Equal(512, stream.Position);
        
        // act - read second block bytes
        stream.Seek(512, SeekOrigin.Begin);
        bytesRead = stream.Read(actualBlockBytes, 0, actualBlockBytes.Length);
        
        // assert - second block bytes area read
        Assert.Equal(512, bytesRead);
        Assert.Equal(blockBytes.Skip(512).Take(512), actualBlockBytes);

        // assert - stream position is equal to third block position dividable by block size
        Assert.Equal(1024, stream.Position);
    }
    
    [Fact]
    public void WhenReadBufferThenPositionIsIncreasedByCountRead()
    {
        // arrange - block bytes to write
        var blockBytes = new byte[1000];

        // arrange - block memory stream
        var stream = new BlockMemoryStream();
        stream.Write(blockBytes, 0, blockBytes.Length);
        stream.Seek(0, SeekOrigin.Begin);
        
        // act - read block bytes
        blockBytes = new byte[100];
        var bytesRead = stream.Read(blockBytes, 0, blockBytes.Length);
        
        // assert - stream position is equal to next position dividable by block size
        Assert.Equal(100, bytesRead);
        Assert.Equal(512, stream.Position);
    }
    
    [Fact]
    public void WhenWriteBufferLargerThanBlockSizeThenPositionIsIncreasedByCountWritten()
    {
        // arrange - block bytes to write
        var blockBytes = new byte[1000];

        // arrange - block memory stream
        var stream = new BlockMemoryStream();
        
        // act - write block bytes
        stream.Write(blockBytes, 0, blockBytes.Length);
        
        // assert - stream position is equal to next position dividable by block size
        Assert.Equal(1024, stream.Position);
    }

    [Fact]
    public void WhenWriteBufferLargerThanBlockSizeThenBufferIsWrittenInChunksOfBlockSize()
    {
        var blockBytes = new byte[1000];
        Array.Fill<byte>(blockBytes, 1, 0, 512);
        Array.Fill<byte>(blockBytes, 2, 512, 1000 - 512);

        var stream = new BlockMemoryStream();
        stream.Write(blockBytes, 0, blockBytes.Length);
        
        // assert - block 1 is written at offset 0
        Assert.True(stream.Blocks.ContainsKey(0));
        
        // assert - block 2 contains 1 all 512 bytes
        var expectedBlock1 = new byte[512];
        Array.Fill<byte>(expectedBlock1, 1, 0, 512);
        Assert.Equal(expectedBlock1, stream.Blocks[0]);
        
        // assert - block 2 is written at offset 512
        Assert.True(stream.Blocks.ContainsKey(512));

        // assert - block 2 contains 2 for first 488 bytes and 0 for remaining bytes
        var expectedBlock2 = new byte[512];
        Array.Fill<byte>(expectedBlock2, 2, 0, 1000 - 512);
        Assert.Equal(expectedBlock2, stream.Blocks[512]);
    }
}