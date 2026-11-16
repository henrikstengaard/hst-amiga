namespace Hst.Amiga.Vm;

using System;
using System.IO;

/// <summary>
/// Read only stream removing carriage returns, used to convert host console input to Amiga line feeds.
/// </summary>
internal class CarriageReturnFilterStream : Stream
{
    private readonly Stream stream;

    public CarriageReturnFilterStream(Stream stream)
    {
        this.stream = stream;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (true)
        {
            var read = stream.Read(buffer, offset, count);
            if (read <= 0)
            {
                return read;
            }

            var length = 0;
            for (var i = 0; i < read; i++)
            {
                if (buffer[offset + i] != '\r')
                {
                    buffer[offset + length++] = buffer[offset + i];
                }
            }

            if (length > 0)
            {
                return length;
            }
        }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
