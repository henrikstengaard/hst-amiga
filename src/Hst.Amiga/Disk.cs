namespace Hst.Amiga
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Disk
    {
        public static async Task<byte[]> ReadBlock(Stream stream, int blockSize)
        {
            if (blockSize % 512 != 0)
            {
                throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
            }

            var blockBytes = new byte[blockSize];
            await ReadExactly(stream, blockBytes, 0, blockBytes.Length);
            return blockBytes;
        }

        /// <summary>
        /// Read exactly count bytes from stream into buffer. Streams can return fewer bytes than requested,
        /// so read is repeated until all bytes are read or end of stream is reached.
        /// </summary>
        /// <exception cref="IOException">End of stream reached before count bytes were read</exception>
        public static async Task ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            while (bytesRead < count)
            {
                var n = await stream.ReadAsync(buffer, offset + bytesRead, count - bytesRead);
                if (n <= 0)
                {
                    throw new IOException($"Read block bytes only returned {bytesRead} bytes, but expected {count} bytes");
                }

                bytesRead += n;
            }
        }

        public static async Task WriteBlock(Stream stream, byte[] blockBytes)
        {
            if (blockBytes.Length % 512 != 0)
            {
                throw new ArgumentException("Block bytes must be dividable by 512", nameof(blockBytes));
            }

            await stream.WriteAsync(blockBytes, 0, blockBytes.Length);
        }

        public static async Task FindUsedSectors(Stream stream, int sectorSize, Func<long, byte[], Task> handler, CancellationToken token)
        {
            if (sectorSize % 512 != 0)
            {
                throw new ArgumentException("Sector size must be dividable by 512", nameof(sectorSize));
            }
            
            var sectorBytes = new byte[sectorSize];
            int bytesRead;
            do
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }
                
                var offset = stream.Position;
                bytesRead = await stream.ReadAsync(sectorBytes, 0, sectorBytes.Length, token);

                // skip unused sector if all bytes are zero
                if (sectorBytes.All(x => x == 0))
                {
                    continue;
                }

                var bytes = new byte[sectorSize];
                Array.Copy(sectorBytes, 0, bytes, 0, bytesRead);
                await handler(offset, bytes);
            } while (bytesRead == sectorSize);
        }
    }
}