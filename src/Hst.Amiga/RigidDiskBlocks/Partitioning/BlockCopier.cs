namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Copies bytes within a stream. Source and destination can overlap.
    /// </summary>
    public static class BlockCopier
    {
        /// <summary>
        /// Default size of buffer used to copy.
        /// </summary>
        public const int DefaultBufferSize = 1024 * 1024;

        /// <summary>
        /// Copy length bytes from source offset to destination offset. Chunks are copied from the end when
        /// destination is after source and from the start when destination is before source, so overlapping
        /// areas are copied correctly.
        /// </summary>
        public static async Task Copy(Stream stream, long sourceOffset, long destinationOffset, long length,
            int bufferSize = DefaultBufferSize, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (sourceOffset < 0) throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (destinationOffset < 0) throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            if (length == 0 || sourceOffset == destinationOffset)
            {
                return;
            }

            var buffer = new byte[(int)Math.Min(bufferSize, length)];
            var backwards = destinationOffset > sourceOffset;
            var remaining = length;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunk = (int)Math.Min(buffer.Length, remaining);
                var position = backwards ? remaining - chunk : length - remaining;

                stream.Seek(sourceOffset + position, SeekOrigin.Begin);
                await ReadExactly(stream, buffer, chunk, cancellationToken);

                stream.Seek(destinationOffset + position, SeekOrigin.Begin);
                await stream.WriteAsync(buffer, 0, chunk, cancellationToken);

                remaining -= chunk;
            }

            await stream.FlushAsync(cancellationToken);
        }

        private static async Task ReadExactly(Stream stream, byte[] buffer, int count,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken);
                if (read == 0)
                {
                    // past end of stream, remaining bytes are zero
                    Array.Clear(buffer, offset, count - offset);
                    return;
                }

                offset += read;
            }
        }
    }
}
