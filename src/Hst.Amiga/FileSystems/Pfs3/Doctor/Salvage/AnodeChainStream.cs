namespace Hst.Amiga.FileSystems.Pfs3.Doctor.Salvage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only stream reading file data from the data blocks of an anode chain. The stream ends early, if the
    /// anode chain is broken or data blocks can't be read, which is indicated by <see cref="Complete"/>.
    /// </summary>
    internal class AnodeChainStream : Stream
    {
        /// <summary>
        /// Maximum number of blocks read at a time.
        /// </summary>
        private const int MaxBlocksPerRead = 128;

        private readonly Volume volume;
        private readonly Func<uint, (bool, uint, uint, uint)> getAnode;
        private readonly long length;
        private readonly byte[] chunk;
        private uint guard;

        private uint nextAnodeNr;
        private uint blockNr;
        private uint blocksLeftInAnode;

        private int chunkOffset;
        private int chunkLength;

        private long position;
        private bool failed;

        /// <param name="volume">Volume to read data blocks from.</param>
        /// <param name="getAnode">Get anode, returns found, cluster size, block number and next anode.</param>
        /// <param name="anodeNr">First anode of chain.</param>
        /// <param name="length">Length of file.</param>
        public AnodeChainStream(Volume volume, Func<uint, (bool, uint, uint, uint)> getAnode, uint anodeNr,
            long length)
        {
            this.volume = volume;
            this.getAnode = getAnode;
            this.length = length;
            nextAnodeNr = anodeNr;
            chunk = new byte[MaxBlocksPerRead * volume.BlockSize];
            guard = volume.DiskSize;
        }

        /// <summary>
        /// All data was read, if stream has been read to the end.
        /// </summary>
        public bool Complete => !failed && position >= length;

        /// <summary>
        /// Reason data couldn't be fully read.
        /// </summary>
        public string FailureReason { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            var total = 0;
            while (total < count && position < length && !failed)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (chunkOffset >= chunkLength && !await FillChunk())
                {
                    break;
                }

                var bytes = (int)Math.Min(Math.Min(count - total, chunkLength - chunkOffset), length - position);
                Array.Copy(chunk, chunkOffset, buffer, offset + total, bytes);
                chunkOffset += bytes;
                position += bytes;
                total += bytes;
            }

            return total;
        }

        private async Task<bool> FillChunk()
        {
            while (blocksLeftInAnode == 0)
            {
                if (nextAnodeNr == 0)
                {
                    return Fail("anode chain ends before end of file");
                }

                if (guard-- == 0)
                {
                    return Fail("anode chain does not end");
                }

                var (found, clusterSize, anodeBlockNr, next) = getAnode(nextAnodeNr);
                if (!found)
                {
                    return Fail($"anode {nextAnodeNr} not found");
                }

                if (clusterSize == 0 || anodeBlockNr == 0)
                {
                    return Fail($"anode {nextAnodeNr} is empty");
                }

                blockNr = anodeBlockNr;
                blocksLeftInAnode = clusterSize;
                nextAnodeNr = next;
            }

            var blocksNeeded = (length - position + volume.BlockSize - 1) / volume.BlockSize;
            var blocks = (int)Math.Min(Math.Min(blocksLeftInAnode, MaxBlocksPerRead), blocksNeeded);

            if (blockNr >= volume.DiskSize || blocks > volume.DiskSize - blockNr)
            {
                return Fail($"data block {blockNr} is outside partition");
            }

            if (await volume.GetRawBlocks(chunk, blocks, volume.FirstBlock + blockNr) != Pfs3.Doctor.Error.None)
            {
                return Fail($"data block {blockNr} could not be read");
            }

            blockNr += (uint)blocks;
            blocksLeftInAnode -= (uint)blocks;
            chunkOffset = 0;
            chunkLength = blocks * (int)volume.BlockSize;
            return true;
        }

        private bool Fail(string error)
        {
            failed = true;
            FailureReason = error;
            return false;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
