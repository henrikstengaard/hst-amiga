namespace Hst.Amiga.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

public class BlockMemoryStream : Stream
{
    private readonly IDictionary<long, byte[]> blocks;
    private long position;

    public readonly ReadOnlyDictionary<long, byte[]> Blocks;

    public BlockMemoryStream()
    {
        this.blocks = new Dictionary<long, byte[]>();
        this.Blocks = new ReadOnlyDictionary<long, byte[]>(this.blocks);
        this.position = 0;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!blocks.ContainsKey(position))
        {
            return count;
        }

        var blockBytes = blocks[position];
        var bytesRead = Math.Min(blockBytes.Length, count);
        Array.Copy(blockBytes, 0, buffer, offset, bytesRead);

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
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var blockBytes = new byte[count];
        Array.Copy(buffer, offset, blockBytes, 0, count);
        blocks[position] = blockBytes;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => -1;

    public override long Position
    {
        get => position;
        set => position = value;
    }
}