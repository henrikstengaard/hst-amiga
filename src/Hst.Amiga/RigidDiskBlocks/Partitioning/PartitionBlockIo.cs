namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Raw block access to file system blocks in a partition. Block numbers are relative to start of partition.
    /// </summary>
    internal class PartitionBlockIo
    {
        private readonly Stream stream;
        private readonly CancellationToken cancellationToken;

        public PartitionBlockIo(Stream stream, long partitionStartOffset, int blockSize,
            CancellationToken cancellationToken)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            PartitionStartOffset = partitionStartOffset;
            BlockSize = blockSize;
            this.cancellationToken = cancellationToken;
        }

        public long PartitionStartOffset { get; }
        public int BlockSize { get; }

        public Task<byte[]> Read(uint block) => Read(block, 1);

        public async Task<byte[]> Read(uint block, int count)
        {
            var size = count * BlockSize;
            var buffer = new byte[size];
            stream.Seek(PartitionStartOffset + (long)block * BlockSize, SeekOrigin.Begin);
            var offset = 0;
            while (offset < size)
            {
                var read = await stream.ReadAsync(buffer, offset, size - offset, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return buffer;
        }

        public async Task Write(uint block, byte[] data)
        {
            stream.Seek(PartitionStartOffset + (long)block * BlockSize, SeekOrigin.Begin);
            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }

        /// <summary>
        /// Copy blocks within partition. Source and destination must not overlap.
        /// </summary>
        public async Task Copy(uint sourceBlock, uint destinationBlock, int count = 1)
        {
            var data = await Read(sourceBlock, count);
            await Write(destinationBlock, data);
        }

        public Task Flush() => stream.FlushAsync(cancellationToken);
    }
}
