namespace Hst.Amiga.BlockDevices
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Block device using seek and read or write on a stream
    /// </summary>
    internal class StreamBlockDevice : BlockDevice
    {
        private readonly Stream stream;

        public StreamBlockDevice(Stream stream)
        {
            this.stream = stream;
        }

        public override Stream Stream => stream;

        public override async Task<int> ReadAsync(long offset, byte[] buffer, int bufferOffset, int count)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var bytesRead = 0;
            while (bytesRead < count)
            {
                var n = await stream.ReadAsync(buffer, bufferOffset + bytesRead, count - bytesRead);
                if (n == 0)
                {
                    break;
                }

                bytesRead += n;
            }

            return bytesRead;
        }

        public override async Task WriteAsync(long offset, byte[] buffer, int bufferOffset, int count)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            await stream.WriteAsync(buffer, bufferOffset, count);
        }

        public override Task FlushAsync() => stream.FlushAsync();
    }
}
