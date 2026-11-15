namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Read only stream reading file data from extents (runs of continuous blocks) in a volume.
    /// </summary>
    internal class ExtentDataStream : Stream
    {
        private readonly Stream stream;
        private readonly long partitionOffset;
        private readonly int blockSize;
        private readonly IList<(uint Start, uint Blocks)> extents;
        private readonly long length;
        private long position;

        /// <param name="stream">Stream with volume.</param>
        /// <param name="partitionOffset">Byte offset of volume in stream.</param>
        /// <param name="blockSize">Size of blocks in bytes.</param>
        /// <param name="extents">Extents with first block and number of blocks.</param>
        /// <param name="length">Length of data, must not exceed size of extents.</param>
        public ExtentDataStream(Stream stream, long partitionOffset, int blockSize,
            IList<(uint Start, uint Blocks)> extents, long length)
        {
            this.stream = stream;
            this.partitionOffset = partitionOffset;
            this.blockSize = blockSize;
            this.extents = extents;
            this.length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => position = Math.Max(0, Math.Min(value, length));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (count > 0 && position < length)
            {
                // find extent containing position
                long extentOffset = 0;
                var found = false;
                foreach (var extent in extents)
                {
                    var extentSize = (long)extent.Blocks * blockSize;
                    if (position < extentOffset + extentSize)
                    {
                        var within = position - extentOffset;
                        var toRead = (int)Math.Min(Math.Min(count, extentSize - within), length - position);
                        stream.Seek(partitionOffset + (long)extent.Start * blockSize + within, SeekOrigin.Begin);
                        var read = stream.Read(buffer, offset, toRead);
                        if (read <= 0)
                        {
                            return total;
                        }

                        position += read;
                        offset += read;
                        count -= read;
                        total += read;
                        found = true;
                        break;
                    }

                    extentOffset += extentSize;
                }

                if (!found)
                {
                    break;
                }
            }

            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position = position + offset;
                    break;
                case SeekOrigin.End:
                    Position = length + offset;
                    break;
            }

            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
