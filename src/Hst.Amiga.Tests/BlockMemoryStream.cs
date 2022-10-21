namespace Hst.Amiga.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

public class BlockMemoryStream : Stream
{
    private readonly int blockSize;
    private readonly IDictionary<long, byte[]> blocks;
    private long length;
    private long position;

    public readonly ReadOnlyDictionary<long, byte[]> Blocks;

    public BlockMemoryStream(int blockSize = 512)
    {
        if (blockSize % 512 != 0)
        {
            throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
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
        var bytesRead = 0;
        for (var i = offset; i < Math.Min(buffer.Length, offset + count); i += blockSize)
        {
            if (!blocks.ContainsKey(position))
            {
                break;
            }
            
            var block = blocks[position];

            var length = i + blockSize < buffer.Length  ? blockSize : buffer.Length - i;
            
            Array.Copy(block, 0, buffer, i, length);
            position += blockSize;

            bytesRead += length;
        }        
        
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        position = origin switch
        {
            SeekOrigin.End => throw new NotSupportedException("Block memory stream doesn't support seek end"),
            SeekOrigin.Begin => 0,
            _ => position
        };

        position += offset;

        return position;
    }

    public override void SetLength(long value)
    {
        length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (var i = offset; i < Math.Min(buffer.Length, offset + count); i += blockSize)
        {
            var blockBytes = new byte[blockSize];

            var length = i + blockSize < buffer.Length  ? blockSize : buffer.Length - i;
            
            Array.Copy(buffer, i, blockBytes, 0, length);
            blocks[position] = blockBytes;
            position += blockSize;
        }
        
        if (position > length)
        {
            length = position;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => position = value;
    }

    public async Task WriteTo(Stream stream)
    {
        stream.SetLength(length);
        foreach (var block in blocks)
        {
            stream.Seek(block.Key, SeekOrigin.Begin);
            await stream.WriteAsync(block.Value, 0, block.Value.Length);
        }
    }
}