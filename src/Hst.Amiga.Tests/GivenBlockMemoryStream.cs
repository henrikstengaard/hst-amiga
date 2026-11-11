namespace Hst.Amiga.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        
        // assert - stream position is increased by bytes read/written
        Assert.Equal(100, bytesRead);
        Assert.Equal(100, stream.Position);
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
        
        // assert - stream position is increased by bytes read/written
        Assert.Equal(1000, stream.Position);
    }

    [Fact]
    public void WhenReadAtEndOfStreamThenZeroBytesAreRead()
    {
        // arrange - block memory stream with 1024 bytes
        var stream = new BlockMemoryStream();
        stream.Write(new byte[1024], 0, 1024);

        // act - read at end of stream
        var buffer = new byte[512];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // assert - no bytes are read
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void WhenReadUnwrittenBlocksThenZeroesAreReadAndPositionIsIncreased()
    {
        // arrange - block memory stream with length of 2 blocks and no blocks written
        var stream = new BlockMemoryStream();
        stream.SetLength(1024);

        // act - read 2 blocks into buffer filled with non-zero bytes
        var buffer = new byte[1024];
        Array.Fill<byte>(buffer, 1);
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // assert - zeroes are read and position is at end of stream
        Assert.Equal(1024, bytesRead);
        Assert.Equal(new byte[1024], buffer);
        Assert.Equal(1024, stream.Position);
    }

    [Fact]
    public void WhenReadWithOffsetThenOnlyCountBytesAreRead()
    {
        // arrange - block memory stream with 1 block of 1's
        var data = new byte[512];
        Array.Fill<byte>(data, 1);
        var stream = new BlockMemoryStream();
        stream.Write(data, 0, data.Length);
        stream.Position = 0;

        // act - read 100 bytes at offset 10
        var buffer = new byte[1024];
        var bytesRead = stream.Read(buffer, 10, 100);

        // assert - only 100 bytes are read at offset 10
        Assert.Equal(100, bytesRead);
        Assert.All(buffer.Take(10), x => Assert.Equal(0, x));
        Assert.All(buffer.Skip(10).Take(100), x => Assert.Equal(1, x));
        Assert.All(buffer.Skip(110), x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task WhenCopyToStreamThenDataIsCopiedAndCopyEndsAtLength()
    {
        // arrange - sparse block memory stream of 10mb with data in first and last block
        const int size = 10 * 1024 * 1024;
        var firstBlock = new byte[512];
        Array.Fill<byte>(firstBlock, 1);
        var lastBlock = new byte[512];
        Array.Fill<byte>(lastBlock, 2);
        var stream = new BlockMemoryStream();
        stream.SetLength(size);
        stream.Write(firstBlock, 0, firstBlock.Length);
        stream.Position = size - 512;
        stream.Write(lastBlock, 0, lastBlock.Length);
        stream.Position = 0;

        // act - copy to memory stream
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination);

        // assert - destination has same length and data
        var bytes = destination.ToArray();
        Assert.Equal(size, bytes.Length);
        Assert.Equal(firstBlock, bytes.Take(512));
        Assert.Equal(lastBlock, bytes.Skip(size - 512));
        Assert.All(bytes.Skip(512).Take(size - 1024), x => Assert.Equal(0, x));
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

    [Theory]
    [InlineData(0)]
    [InlineData(-512)]
    [InlineData(100)]
    public void WhenCreateWithInvalidBlockSizeThenExceptionIsThrown(int blockSize)
    {
        Assert.Throws<ArgumentException>(() => new BlockMemoryStream(blockSize));
    }

    [Fact]
    public void WhenWritePartialBlockThenRemainingBytesInBlockArePreserved()
    {
        // arrange - block memory stream with 1 block of 1's
        var data = new byte[512];
        Array.Fill<byte>(data, 1);
        var stream = new BlockMemoryStream();
        stream.Write(data, 0, data.Length);

        // act - write 4 bytes of 2's at offset 100
        stream.Position = 100;
        stream.Write(new byte[] { 2, 2, 2, 2 }, 0, 4);

        // assert - only 4 bytes are changed, position and length are byte accurate
        var expected = new byte[512];
        Array.Fill<byte>(expected, 1);
        Array.Fill<byte>(expected, 2, 100, 4);
        Assert.Equal(expected, stream.Blocks[0]);
        Assert.Equal(104, stream.Position);
        Assert.Equal(512, stream.Length);
    }

    [Fact]
    public void WhenWriteAndReadUnalignedAcrossBlocksThenDataIsEqualAndBlocksAreAligned()
    {
        // arrange - data spanning 3 blocks
        var data = new byte[1000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251 + 1);
        }

        var stream = new BlockMemoryStream();

        // act - write data at unaligned offset 300
        stream.Position = 300;
        stream.Write(data, 0, data.Length);

        // act - read data at unaligned offset 300
        stream.Position = 300;
        var actual = new byte[1000];
        var bytesRead = stream.Read(actual, 0, actual.Length);

        // assert - data is equal, length is byte accurate and blocks are stored at aligned offsets
        Assert.Equal(1000, bytesRead);
        Assert.Equal(data, actual);
        Assert.Equal(1300, stream.Length);
        Assert.Equal(new long[] { 0, 512, 1024 }, stream.Blocks.Keys.OrderBy(x => x));

        // assert - bytes before written data are zeroes
        stream.Position = 0;
        var before = new byte[300];
        stream.Read(before, 0, before.Length);
        Assert.Equal(new byte[300], before);
    }

    [Fact]
    public void WhenReadPastLengthThenOnlyBytesUntilLengthAreRead()
    {
        // arrange - block memory stream with 1000 bytes
        var stream = new BlockMemoryStream();
        stream.Write(new byte[1000], 0, 1000);

        // act - read 512 bytes at position 900
        stream.Position = 900;
        var bytesRead = stream.Read(new byte[512], 0, 512);

        // assert - only 100 bytes are read
        Assert.Equal(100, bytesRead);
        Assert.Equal(1000, stream.Position);
    }

    [Fact]
    public void WhenSeekFromEndThenPositionIsRelativeToLength()
    {
        var stream = new BlockMemoryStream();
        stream.SetLength(1000);

        Assert.Equal(990, stream.Seek(-10, SeekOrigin.End));
        Assert.Equal(1000, stream.Seek(0, SeekOrigin.End));
    }

    [Fact]
    public void WhenSeekOrSetPositionBeforeBeginningThenExceptionIsThrown()
    {
        var stream = new BlockMemoryStream();

        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void WhenShrinkAndExtendLengthThenTruncatedBytesAreReadAsZeroes()
    {
        // arrange - block memory stream with 2 blocks of 1's
        var data = new byte[1024];
        Array.Fill<byte>(data, 1);
        var stream = new BlockMemoryStream();
        stream.Write(data, 0, data.Length);

        // act - shrink to 100 bytes and extend to 1024 bytes again
        stream.SetLength(100);
        stream.SetLength(1024);

        // assert - second block is removed and bytes after 100 are zeroes
        Assert.False(stream.Blocks.ContainsKey(512));
        stream.Position = 0;
        var actual = new byte[1024];
        Assert.Equal(1024, stream.Read(actual, 0, actual.Length));
        Assert.All(actual.Take(100), x => Assert.Equal(1, x));
        Assert.All(actual.Skip(100), x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task WhenWriteToStreamThenDestinationLengthIsEqualToLength()
    {
        // arrange - block memory stream with 1000 bytes of 1's
        var data = new byte[1000];
        Array.Fill<byte>(data, 1);
        var stream = new BlockMemoryStream();
        stream.Write(data, 0, data.Length);

        // act - write to memory stream
        var destination = new MemoryStream();
        await stream.WriteTo(destination);

        // assert - destination is equal to data and not extended to block size
        Assert.Equal(data, destination.ToArray());
    }
}
