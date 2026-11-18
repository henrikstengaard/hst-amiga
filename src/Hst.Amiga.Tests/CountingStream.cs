namespace Hst.Amiga.Tests;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Stream counting reads and writes to inner stream.
/// </summary>
public class CountingStream : Stream
{
    private readonly Stream stream;

    public CountingStream(Stream stream)
    {
        this.stream = stream;
    }

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public void Reset()
    {
        Reads = 0;
        Writes = 0;
    }

    public override void Flush() => stream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        Reads++;
        return stream.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Reads++;
        return stream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        Writes++;
        stream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Writes++;
        return stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }
}
