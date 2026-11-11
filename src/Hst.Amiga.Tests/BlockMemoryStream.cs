namespace Hst.Amiga.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Sparse memory stream storing data in blocks of block size at block aligned offsets.
/// Reads and writes are byte accurate and unwritten areas within length are read as zeroes.
/// </summary>
public class BlockMemoryStream : Stream
{
    private readonly int blockSize;
    private readonly IDictionary<long, byte[]> blocks;
    private long length;
    private long position;

    /// <summary>
    /// Blocks written indexed by block aligned offset.
    /// </summary>
    public readonly ReadOnlyDictionary<long, byte[]> Blocks;

    public BlockMemoryStream(int blockSize = 512)
    {
        if (blockSize <= 0 || blockSize % 512 != 0)
        {
            throw new ArgumentException("Block size must be a positive multiple of 512", nameof(blockSize));
        }

        this.blockSize = blockSize;
        this.blocks = new Dictionary<long, byte[]>();
        this.Blocks = new ReadOnlyDictionary<long, byte[]>(this.blocks);
        this.length = 0;
        this.position = 0;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        if (position >= length)
        {
            return 0;
        }

        var bytesToRead = (int)Math.Min(count, length - position);
        var bytesRead = 0;

        while (bytesRead < bytesToRead)
        {
            var blockOffset = GetBlockOffset(position);
            var offsetInBlock = (int)(position - blockOffset);
            var bytesToCopy = Math.Min(blockSize - offsetInBlock, bytesToRead - bytesRead);

            if (blocks.TryGetValue(blockOffset, out var block))
            {
                Array.Copy(block, offsetInBlock, buffer, offset + bytesRead, bytesToCopy);
            }
            else
            {
                // block doesn't exist, read as zeroes
                Array.Clear(buffer, offset + bytesRead, bytesToCopy);
            }

            position += bytesToCopy;
            bytesRead += bytesToCopy;
        }

        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        var bytesWritten = 0;

        while (bytesWritten < count)
        {
            var blockOffset = GetBlockOffset(position);
            var offsetInBlock = (int)(position - blockOffset);
            var bytesToCopy = Math.Min(blockSize - offsetInBlock, count - bytesWritten);

            // get existing block to preserve bytes not written or create new zeroed block
            if (!blocks.TryGetValue(blockOffset, out var block))
            {
                block = new byte[blockSize];
                blocks[blockOffset] = block;
            }

            Array.Copy(buffer, offset + bytesWritten, block, offsetInBlock, bytesToCopy);

            position += bytesToCopy;
            bytesWritten += bytesToCopy;
        }

        if (position > length)
        {
            length = position;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0)
        {
            throw new IOException("Seek before beginning of stream");
        }

        position = newPosition;

        return position;
    }

    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (value < length)
        {
            // remove blocks starting at or after new length
            foreach (var blockOffset in blocks.Keys.Where(x => x >= value).ToList())
            {
                blocks.Remove(blockOffset);
            }

            // clear bytes after new length in last block, so they are read as zeroes if length is increased
            var lastBlockOffset = GetBlockOffset(value);
            if (blocks.TryGetValue(lastBlockOffset, out var lastBlock))
            {
                var offsetInBlock = (int)(value - lastBlockOffset);
                Array.Clear(lastBlock, offsetInBlock, blockSize - offsetInBlock);
            }
        }

        length = value;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            position = value;
        }
    }

    /// <summary>
    /// Write blocks to stream at their offsets and set stream length to length of block memory stream.
    /// </summary>
    public async Task WriteTo(Stream stream)
    {
        stream.SetLength(length);
        foreach (var block in blocks.OrderBy(x => x.Key))
        {
            stream.Seek(block.Key, SeekOrigin.Begin);
            var bytesToWrite = (int)Math.Min(blockSize, length - block.Key);
            await stream.WriteAsync(block.Value, 0, bytesToWrite);
        }
    }

    private long GetBlockOffset(long offset) => offset / blockSize * blockSize;
}
