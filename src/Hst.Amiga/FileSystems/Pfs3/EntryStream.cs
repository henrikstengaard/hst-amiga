namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class EntryStream : Stream
#if NET6_0
        , IAsyncDisposable
#endif
    {
        private readonly fileentry fileEntry;
        private readonly globaldata g;
        private bool dataWritten;

        public EntryStream(fileentry fileEntry, globaldata g)
        {
            this.fileEntry = fileEntry;
            this.g = g;
            this.dataWritten = false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Close(fileEntry, g).GetAwaiter().GetResult();
            if (this.dataWritten)
            {
                Disk.UpdateDataCache(g).GetAwaiter().GetResult();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadInternal(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return await ReadInternal(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (Disk.SeekInObject(fileEntry, (int)offset, GetMode(origin), g).GetAwaiter().GetResult() < 0)
            {
                throw new IOException($"Seek to offset {offset} from {origin} failed");
            }

            return fileEntry.offset;
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    $"Offset '{offset}' and count '{count}' is outside buffer size '{buffer.Length}'");
            }
        }

        private async Task<int> ReadInternal(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);

            if (count == 0)
            {
                return 0;
            }

            // read from object only supports reading into start of buffer
            var readBuffer = offset == 0 ? buffer : new byte[count];
            var result = await Directory.ReadFromObject(fileEntry, readBuffer, (uint)count, g);
            if (result == uint.MaxValue)
            {
                throw new IOException("Read failed");
            }

            var bytesRead = (int)result;
            if (offset != 0 && bytesRead > 0)
            {
                Array.Copy(readBuffer, 0, buffer, offset, bytesRead);
            }

            return bytesRead;
        }

        private async Task WriteInternal(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);

            if (count == 0)
            {
                return;
            }

            // write to object only supports writing from start of buffer
            var writeBuffer = buffer;
            if (offset != 0)
            {
                writeBuffer = new byte[count];
                Array.Copy(buffer, offset, writeBuffer, 0, count);
            }

            this.dataWritten = true;
            await Directory.WriteToObject(fileEntry, writeBuffer, (uint)count, g);
        }

        private int GetMode(SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return Constants.OFFSET_BEGINNING;
                case SeekOrigin.End:
                    return Constants.OFFSET_END;
                case SeekOrigin.Current:
                    return Constants.OFFSET_CURRENT;
                default:
                    return Constants.OFFSET_BEGINNING;
            }
        }

        public override void SetLength(long value)
        {
            throw new System.NotSupportedException("Entry stream doesn't support set length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteInternal(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteInternal(buffer, offset, count);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => fileEntry.originalsize;

        public override long Position
        {
            get => fileEntry.offset;
            set => Seek(value, SeekOrigin.Begin);
        }

#if NET6_0
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
        }
#endif
    }
}