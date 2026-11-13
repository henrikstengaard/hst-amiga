namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only stream reading file data from a list of data blocks. For OFS, data is read after the data block
    /// header. Blocks are read from volume stream, when stream is read.
    /// </summary>
    internal class DataBlockStream : Stream
    {
        private readonly DoctorVolume volume;
        private readonly IList<uint> blocks;
        private readonly bool ofs;
        private readonly long length;
        private readonly byte[] block;

        private int blockIndex;
        private int blockOffset;
        private int blockLength;
        private long position;

        /// <param name="volume">Volume to read data blocks from.</param>
        /// <param name="blocks">Data blocks in sequence order.</param>
        /// <param name="ofs">Data blocks are OFS data blocks with header.</param>
        /// <param name="length">Length of data.</param>
        public DataBlockStream(DoctorVolume volume, IList<uint> blocks, bool ofs, long length)
        {
            this.volume = volume;
            this.blocks = blocks;
            this.ofs = ofs;
            this.length = length;
            block = new byte[volume.BlockSize];
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (count > 0 && position < length)
            {
                if (blockOffset >= blockLength)
                {
                    if (blockIndex >= blocks.Count)
                    {
                        break;
                    }

                    ReadNextBlock();
                    continue;
                }

                var bytes = (int)Math.Min(Math.Min(count, blockLength - blockOffset), length - position);
                Array.Copy(block, blockOffset, buffer, offset, bytes);
                blockOffset += bytes;
                offset += bytes;
                count -= bytes;
                total += bytes;
                position += bytes;
            }

            return total;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        private void ReadNextBlock()
        {
            var blockNumber = blocks[blockIndex++];
            volume.Stream.Seek(volume.PartitionStartOffset + (long)blockNumber * volume.BlockSize, SeekOrigin.Begin);
            var read = 0;
            while (read < block.Length)
            {
                var bytesRead = volume.Stream.Read(block, read, block.Length - read);
                if (bytesRead == 0)
                {
                    break;
                }

                read += bytesRead;
            }

            if (ofs)
            {
                var dataSize = (int)Math.Min(Core.Converters.BigEndianConverter.ConvertBytesToUInt32(block, 0xc),
                    (uint)(volume.BlockSize - 24));
                Array.Copy(block, 24, block, 0, dataSize);
                blockLength = dataSize;
            }
            else
            {
                blockLength = block.Length;
            }

            blockOffset = 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
