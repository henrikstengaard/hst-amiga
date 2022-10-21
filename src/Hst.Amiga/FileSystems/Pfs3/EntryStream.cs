namespace Hst.Amiga.FileSystems.Pfs3
{
#if NET6_0
    using System;
#endif
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

        public EntryStream(fileentry fileEntry, globaldata g)
        {
            this.fileEntry = fileEntry;
            this.g = g;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Close(fileEntry, g).GetAwaiter().GetResult();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return (int)Directory.ReadFromObject(fileEntry, buffer, (uint)buffer.Length, g).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return (int)await Directory.ReadFromObject(fileEntry, buffer, (uint)buffer.Length, g);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Disk.SeekInObject(fileEntry, (int)offset, GetMode(origin), g).GetAwaiter().GetResult();
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
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Directory.WriteToObject(fileEntry, buffer, (uint)count, g).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Directory.WriteToObject(fileEntry, buffer, (uint)count, g);
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
            await File.Close(fileEntry, g);
            await base.DisposeAsync();
        }
#endif
    }
}