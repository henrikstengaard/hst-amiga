namespace Hst.Amiga.Tests;

using System;
using System.IO;

public class BlockFileStream : Stream
{
    private readonly string path;
    private long position;

    public BlockFileStream(string path)
    {
        this.path = path;
        this.position = 0;
    }

    private string GetBlockPath()
    {
        return Path.Combine(path, $"{position}.bin");
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var blockPath = GetBlockPath();
        if (!File.Exists(blockPath))
        {
            return count;
        }

        var blockBytes = File.ReadAllBytes(blockPath);
        var bytesRead = Math.Min(blockBytes.Length, count);
        Array.Copy(blockBytes, 0, buffer, offset, bytesRead);

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        position = origin switch
        {
            SeekOrigin.End => throw new NotSupportedException("Block file stream doesn't support seek end"),
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
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var blockPath = GetBlockPath();
        var blockBytes = new byte[count];
        Array.Copy(buffer, offset, blockBytes, 0, count);
        File.WriteAllBytes(blockPath, blockBytes);
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