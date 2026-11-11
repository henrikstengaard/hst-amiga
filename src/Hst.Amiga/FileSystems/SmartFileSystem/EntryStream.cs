namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    /// <summary>
    /// Stream for reading and writing data of a smart file system file using extents.
    /// </summary>
    public class EntryStream : Stream
    {
        private readonly Volume volume;
        private readonly uint objectNode;
        private readonly bool writable;
        private readonly List<ExtentBNode> extents;
        private long allocatedBlocks;
        private long size;
        private long position;
        private bool dirty;
        private bool disposed;

        private EntryStream(Volume volume, uint objectNode, bool writable, long size, IEnumerable<ExtentBNode> extents)
        {
            this.volume = volume;
            this.objectNode = objectNode;
            this.writable = writable;
            this.size = size;
            this.extents = extents.ToList();
            allocatedBlocks = this.extents.Sum(x => (long)x.Blocks);
            position = 0;
            dirty = false;
        }

        /// <summary>
        /// Open entry stream for file object.
        /// </summary>
        public static async Task<EntryStream> Open(Volume volume, uint objectNode, bool writable)
        {
            var objectInfo = await Objects.ReadObject(volume, objectNode);
            if (!objectInfo.Object.IsFile)
            {
                throw new NotAFileException($"Object '{objectInfo.Object.Name}' is not a file");
            }

            var extents = await Objects.ReadExtents(volume, objectInfo.Object.Data);
            return new EntryStream(volume, objectNode, writable, objectInfo.Object.Size, extents);
        }

        public override bool CanRead => !disposed;
        public override bool CanSeek => !disposed;
        public override bool CanWrite => !disposed && writable;
        public override long Length => size;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            FlushInternal().GetAwaiter().GetResult();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await FlushInternal();
        }

        private async Task FlushInternal()
        {
            if (!dirty)
            {
                return;
            }

            var newSize = (uint)size;
            await Objects.UpdateObject(volume, objectNode, o =>
            {
                o.Size = newSize;
                o.DateModified = DateTime.Now;
                o.Protection &= ~(uint)ProtectionBits.Archive;
            });
            dirty = false;
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

        private async Task<int> ReadInternal(byte[] buffer, int offset, int count)
        {
            if (position >= size || count <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, size - position);
            var bytesRead = 0;
            while (bytesRead < bytesToRead)
            {
                var blockIndex = position / volume.BlockSize;
                var offsetInBlock = (int)(position % volume.BlockSize);
                var length = Math.Min(volume.BlockSize - offsetInBlock, bytesToRead - bytesRead);

                var blockBytes = await Disk.ReadBlock(volume, MapBlock(blockIndex));
                Array.Copy(blockBytes, offsetInBlock, buffer, offset + bytesRead, length);

                bytesRead += length;
                position += length;
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = size + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            if (newPosition < 0)
            {
                throw new IOException("Seek before beginning of stream");
            }

            position = newPosition;
            return position;
        }

        public override void SetLength(long value)
        {
            SetLengthInternal(value).GetAwaiter().GetResult();
        }

        private async Task SetLengthInternal(long value)
        {
            EnsureWritable();

            if (value < 0 || value > Constants.MaxFileSize)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value > size)
            {
                await WriteZeros(size, value - size);
            }
            else if (value < size)
            {
                await Truncate(value);
            }

            size = value;
            dirty = true;
            if (position > size)
            {
                position = size;
            }

            await FlushInternal();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteInternal(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteInternal(buffer, offset, count);
        }

        private async Task WriteInternal(byte[] buffer, int offset, int count)
        {
            EnsureWritable();

            if (count <= 0)
            {
                return;
            }

            if (position + count > Constants.MaxFileSize)
            {
                throw new IOException($"File size can't exceed {Constants.MaxFileSize} bytes");
            }

            // fill gap between end of file and position with zeros
            if (position > size)
            {
                await WriteZeros(size, position - size);
            }

            await WriteAt(position, buffer, offset, count);
            position += count;
        }

        private async Task WriteZeros(long start, long length)
        {
            var zeros = new byte[Math.Min(length, 64 * volume.BlockSize)];
            while (length > 0)
            {
                var chunk = (int)Math.Min(length, zeros.Length);
                await WriteAt(start, zeros, 0, chunk);
                start += chunk;
                length -= chunk;
            }
        }

        private async Task WriteAt(long writePosition, byte[] buffer, int offset, int count)
        {
            var end = writePosition + count;
            var blocksNeeded = (end + volume.BlockSize - 1) / volume.BlockSize;
            if (blocksNeeded > allocatedBlocks)
            {
                await AllocateBlocks(blocksNeeded - allocatedBlocks);
            }

            var bytesWritten = 0;
            while (bytesWritten < count)
            {
                var blockIndex = writePosition / volume.BlockSize;
                var offsetInBlock = (int)(writePosition % volume.BlockSize);
                var length = Math.Min(volume.BlockSize - offsetInBlock, count - bytesWritten);
                var block = MapBlock(blockIndex);

                byte[] blockBytes;
                if (length == volume.BlockSize)
                {
                    blockBytes = new byte[volume.BlockSize];
                }
                else if (blockIndex * volume.BlockSize < size)
                {
                    blockBytes = await Disk.ReadBlock(volume, block);
                }
                else
                {
                    blockBytes = new byte[volume.BlockSize];
                }

                Array.Copy(buffer, offset + bytesWritten, blockBytes, offsetInBlock, length);
                await Disk.WriteBlock(volume, block, blockBytes);

                bytesWritten += length;
                writePosition += length;
                if (writePosition > size)
                {
                    size = writePosition;
                }
            }

            dirty = true;
        }

        /// <summary>
        /// Map file block index to disk block using extents.
        /// </summary>
        private uint MapBlock(long blockIndex)
        {
            var start = 0L;
            foreach (var extent in extents)
            {
                if (blockIndex < start + extent.Blocks)
                {
                    return extent.Key + (uint)(blockIndex - start);
                }

                start += extent.Blocks;
            }

            throw new IOException($"Block index {blockIndex} is outside extents of file");
        }

        /// <summary>
        /// Allocate blocks and add them to extents of file, addblocks() in smart file system source.
        /// </summary>
        private async Task AllocateBlocks(long blocks)
        {
            if (blocks > volume.FreeBlocks)
            {
                throw new DiskFullException("Disk full, not enough free blocks");
            }

            while (blocks > 0)
            {
                var last = extents.LastOrDefault();
                var hint = last != null ? last.Key + last.Blocks : volume.RovingPointer;
                var run = Bitmap.FindRun(volume, hint, (uint)Math.Min(blocks, Constants.MaxExtentBlocks));
                if (run == null)
                {
                    throw new DiskFullException("Disk full, not enough free blocks");
                }

                await Bitmap.MarkUsed(volume, run.Value.Start, run.Value.Length);
                await AddBlocks(run.Value.Start, (ushort)run.Value.Length);
                volume.RovingPointer = run.Value.Start + run.Value.Length;
                blocks -= run.Value.Length;
            }
        }

        private async Task AddBlocks(uint start, ushort blocks)
        {
            var last = extents.LastOrDefault();
            if (last != null && last.Key + last.Blocks == start && last.Blocks + blocks <= Constants.MaxExtentBlocks)
            {
                // extend last extent
                var result = await ExtentBTree.FindExtent(volume, last.Key);
                var extent = (ExtentBNode)result.Node;
                extent.Blocks += blocks;
                await Disk.WriteBNodeContainer(volume, result.Container);
                last.Blocks = extent.Blocks;
            }
            else
            {
                var newExtent = new ExtentBNode
                {
                    Key = start,
                    Next = 0,
                    Prev = last?.Key ?? (objectNode | Constants.ExtentPrevObjectNodeFlag),
                    Blocks = blocks
                };
                await ExtentBTree.CreateBNode(volume, volume.RootBlock.ExtentBNodeRoot, newExtent.Clone());

                if (last != null)
                {
                    var result = await ExtentBTree.FindExtent(volume, last.Key);
                    ((ExtentBNode)result.Node).Next = start;
                    await Disk.WriteBNodeContainer(volume, result.Container);
                    last.Next = start;
                }
                else
                {
                    await Objects.UpdateObject(volume, objectNode, o => o.Data = start);
                }

                extents.Add(newExtent);
            }

            allocatedBlocks += blocks;
        }

        /// <summary>
        /// Truncate file to length and free blocks no longer used.
        /// </summary>
        private async Task Truncate(long length)
        {
            var keepBlocks = (length + volume.BlockSize - 1) / volume.BlockSize;
            var start = 0L;
            var keptExtents = new List<ExtentBNode>();

            foreach (var extent in extents)
            {
                if (start >= keepBlocks)
                {
                    await Bitmap.MarkFree(volume, extent.Key, extent.Blocks);
                    await ExtentBTree.DeleteBNode(volume, volume.RootBlock.ExtentBNodeRoot, extent.Key);
                }
                else if (start + extent.Blocks > keepBlocks)
                {
                    var keep = (ushort)(keepBlocks - start);
                    await Bitmap.MarkFree(volume, extent.Key + keep, (uint)(extent.Blocks - keep));
                    extent.Blocks = keep;
                    keptExtents.Add(extent);
                }
                else
                {
                    keptExtents.Add(extent);
                }

                start += extent.Blocks;
            }

            if (keptExtents.Count > 0)
            {
                var last = keptExtents[keptExtents.Count - 1];
                last.Next = 0;
                var result = await ExtentBTree.FindExtent(volume, last.Key);
                var lastExtent = (ExtentBNode)result.Node;
                lastExtent.Blocks = last.Blocks;
                lastExtent.Next = 0;
                await Disk.WriteBNodeContainer(volume, result.Container);
            }
            else if (extents.Count > 0)
            {
                await Objects.UpdateObject(volume, objectNode, o => o.Data = 0);
            }

            extents.Clear();
            extents.AddRange(keptExtents);
            allocatedBlocks = extents.Sum(x => (long)x.Blocks);
        }

        private void EnsureWritable()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(EntryStream));
            }

            if (!writable)
            {
                throw new NotSupportedException("Stream is not writable");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                FlushInternal().GetAwaiter().GetResult();
            }

            disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                await FlushInternal();
            }

            disposed = true;
            await base.DisposeAsync();
        }
    }
}
