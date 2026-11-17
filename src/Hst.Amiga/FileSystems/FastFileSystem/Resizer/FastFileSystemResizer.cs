namespace Hst.Amiga.FileSystems.FastFileSystem.Resizer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using Doctor;
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;
    using Constants = FastFileSystem.Constants;

    /// <summary>
    /// Resize partition with Fast File System. File system is expanded or shrunk within the cylinders available
    /// before and after the partition. When shrinking, blocks used in the area cut off are relocated to free
    /// blocks. Root block is moved to the middle of the resized volume and bitmap is rebuilt.
    /// </summary>
    public static class FastFileSystemResizer
    {

        private const int OffsetChecksum = 0x14;

        /// <summary>
        /// Resize partition to new cylinders. File system is checked before and after the resize.
        /// </summary>
        /// <param name="stream">Stream with disk containing rigid disk block.</param>
        /// <param name="partitionNumber">Partition number starting from 1.</param>
        /// <param name="options">Resize options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task<PartitionOperationResult> Resize(Stream stream, int partitionNumber,
            PartitionResizeOptions options, CancellationToken cancellationToken = default) =>
            PartitionOperation.Resize(stream, FastFileSystemPartition.Instance, partitionNumber, options,
                cancellationToken);

        internal static async Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders,
            bool dryRun, IList<string> messages, CancellationToken cancellationToken)
        {
            var oldVolume = DoctorVolume.Open(stream, partitionBlock);
            var blockSize = oldVolume.BlockSize;
            var io = new PartitionBlockIo(stream, oldVolume.PartitionStartOffset, blockSize, cancellationToken);
            var dosType = await ReadDosType(io, partitionBlock);

            var newVolume = DoctorVolume.Open(stream, partitionBlock.LowCyl, partitionBlock.LowCyl + newCylinders - 1,
                partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
                partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize, dosType);

            var reserved = oldVolume.Reserved;
            var oldBlocks = oldVolume.Blocks;
            var newBlocks = newVolume.Blocks;
            var oldRoot = oldVolume.RootBlock;
            var newRoot = newVolume.RootBlock;
            var maxBlocks = Math.Max(oldBlocks, newBlocks);

            if (newBlocks <= reserved + 2)
            {
                throw new PartitionOperationException($"New size of {newBlocks} blocks is too small");
            }

            /* pass 1: find blocks used */
            var walker = new FastFileSystemBlockWalker(io, dosType, oldRoot, oldBlocks);
            var used = new BitArray((int)maxBlocks);
            long usedBlocks = 0;
            await walker.Walk(block =>
            {
                if (block < reserved || used[(int)block])
                {
                    throw new PartitionOperationException(
                        $"Block {block} is used more than once or in reserved area, run doctor to repair");
                }

                used[(int)block] = true;
                usedBlocks++;
            }, null, cancellationToken);

            /* calculate bitmap blocks for new size */
            var bitmapLongsPerBlock = blockSize / 4 - 1;
            var bitmapBitsPerBlock = bitmapLongsPerBlock * 32;
            var bitmapPointersPerExtensionBlock = blockSize / 4 - 1;
            var bitmapCount = (int)((newBlocks + (long)bitmapBitsPerBlock - 1) / bitmapBitsPerBlock);
            var extensionCount = bitmapCount > Constants.BM_SIZE
                ? (bitmapCount - Constants.BM_SIZE + bitmapPointersPerExtensionBlock - 1) /
                  bitmapPointersPerExtensionBlock
                : 0;

            var required = usedBlocks + 1 + bitmapCount + extensionCount;
            var available = newBlocks - reserved;
            if (required > available)
            {
                throw new PartitionOperationException(
                    $"Fast File System uses {required} blocks incl. root and bitmap blocks, which doesn't fit in new size with {available} blocks");
            }

            /* blocks to relocate: used blocks after new end and block used at new root block */
            var relocate = new List<uint>();
            for (var block = newBlocks; block < maxBlocks; block++)
            {
                if (used[(int)block])
                {
                    relocate.Add(block);
                }
            }

            if (newRoot != oldRoot && used[(int)newRoot])
            {
                relocate.Add(newRoot);
            }

            /* blocks used after resize */
            var final = new BitArray((int)newBlocks);
            for (var block = reserved; block < newBlocks; block++)
            {
                final[(int)block] = used[(int)block];
            }

            final[(int)newRoot] = true;

            uint Allocate()
            {
                for (var block = newRoot + 1; block < newBlocks; block++)
                {
                    if (!final[(int)block])
                    {
                        final[(int)block] = true;
                        return block;
                    }
                }

                for (var block = reserved; block < newRoot; block++)
                {
                    if (!final[(int)block])
                    {
                        final[(int)block] = true;
                        return block;
                    }
                }

                throw new PartitionOperationException("No free blocks to relocate blocks");
            }

            var bitmapBlocks = new uint[bitmapCount];
            for (var i = 0; i < bitmapCount; i++)
            {
                bitmapBlocks[i] = Allocate();
            }

            var extensionBlocks = new uint[extensionCount];
            for (var i = 0; i < extensionCount; i++)
            {
                extensionBlocks[i] = Allocate();
            }

            var map = new Dictionary<uint, uint>();
            foreach (var block in relocate)
            {
                map[block] = Allocate();
            }

            if (oldRoot != newRoot)
            {
                map[oldRoot] = newRoot;
            }

            messages.Add(
                $"Resize Fast File System from {oldBlocks} to {newBlocks} blocks, root block {oldRoot} -> {newRoot}, relocate {relocate.Count} blocks, {bitmapCount} bitmap blocks");

            if (dryRun)
            {
                return;
            }

            /* pass 2: find block pointers to relocated blocks */
            var references = new List<(uint Holder, int Offset, uint Target)>();
            await walker.Walk(null, (holder, offset, target) =>
            {
                if (map.ContainsKey(target))
                {
                    references.Add((holder, offset, target));
                }
            }, cancellationToken);

            /* changed blocks by new block number */
            var changed = new Dictionary<uint, byte[]>();

            async Task<byte[]> GetForChange(uint block)
            {
                var newBlock = map.TryGetValue(block, out var mapped) ? mapped : block;
                if (!changed.TryGetValue(newBlock, out var data))
                {
                    data = await io.Read(block);
                    changed[newBlock] = data;
                }

                return data;
            }

            foreach (var reference in references)
            {
                var data = await GetForChange(reference.Holder);
                SetUInt32(data, reference.Offset, map[reference.Target]);
            }

            /* root block */
            var root = await GetForChange(oldRoot);
            if (!walker.UseDirCache && GetUInt32(root, blockSize - 0x08) == oldRoot)
            {
                /* root block extension set to root block by formatter is kept pointing to root block */
                SetUInt32(root, blockSize - 0x08, newRoot);
            }

            for (var i = 0; i < Constants.BM_SIZE; i++)
            {
                SetUInt32(root, blockSize - 0xc4 + i * 4, i < bitmapCount ? bitmapBlocks[i] : 0);
            }

            SetUInt32(root, blockSize - 0x60, extensionCount > 0 ? extensionBlocks[0] : 0);
            SetUInt32(root, blockSize - 0xc8, Constants.BM_VALID);

            foreach (var data in changed.Values)
            {
                ChecksumHelper.UpdateChecksum(data, OffsetChecksum);
            }

            /* bitmap blocks, bit set means free */
            var bitmapData = new byte[bitmapCount][];
            var bitsInBitmap = newBlocks - reserved;
            for (var i = 0; i < bitmapCount; i++)
            {
                var data = new byte[blockSize];
                var start = (long)i * bitmapBitsPerBlock;
                for (var bit = 0; bit < bitmapBitsPerBlock && start + bit < bitsInBitmap; bit++)
                {
                    if (final[(int)(reserved + start + bit)])
                    {
                        continue;
                    }

                    var offset = 4 + bit / 32 * 4;
                    SetUInt32(data, offset, GetUInt32(data, offset) | (1U << (bit % 32)));
                }

                ChecksumHelper.UpdateChecksum(data, 0);
                bitmapData[i] = data;
            }

            /* bitmap extension blocks */
            var extensionData = new byte[extensionCount][];
            for (var e = 0; e < extensionCount; e++)
            {
                var data = new byte[blockSize];
                for (var i = 0; i < bitmapPointersPerExtensionBlock; i++)
                {
                    var index = Constants.BM_SIZE + e * bitmapPointersPerExtensionBlock + i;
                    if (index >= bitmapCount)
                    {
                        break;
                    }

                    SetUInt32(data, i * 4, bitmapBlocks[index]);
                }

                SetUInt32(data, blockSize - 4, e < extensionCount - 1 ? extensionBlocks[e + 1] : 0);
                extensionData[e] = data;
            }

            /* boot block root pointer */
            byte[] bootBlock = null;
            if (oldRoot != newRoot && reserved * blockSize >= 1024)
            {
                bootBlock = await io.Read(0, 1024 / blockSize);
                if (GetUInt32(bootBlock, 8) == oldRoot)
                {
                    var checksumValid = BootBlockChecksumHelper.CalculateChecksum(bootBlock) ==
                                        BigEndianConverter.ConvertBytesToInt32(bootBlock, 4);
                    SetUInt32(bootBlock, 8, newRoot);
                    if (checksumValid)
                    {
                        BootBlockChecksumHelper.UpdateChecksum(bootBlock, 4);
                    }
                }
                else
                {
                    bootBlock = null;
                }
            }

            /* write relocated blocks not changed, e.g. data blocks. destination blocks are free */
            foreach (var entry in map.Where(x => !changed.ContainsKey(x.Value)).OrderBy(x => x.Key))
            {
                await io.Copy(entry.Key, entry.Value);
            }

            /* write changed blocks, root block is written last */
            foreach (var entry in changed.Where(x => x.Key != newRoot).OrderBy(x => x.Key))
            {
                await io.Write(entry.Key, entry.Value);
            }

            for (var e = 0; e < extensionCount; e++)
            {
                await io.Write(extensionBlocks[e], extensionData[e]);
            }

            for (var i = 0; i < bitmapCount; i++)
            {
                await io.Write(bitmapBlocks[i], bitmapData[i]);
            }

            await io.Write(newRoot, changed[newRoot]);

            if (bootBlock != null)
            {
                await io.Write(0, bootBlock);
            }

            await io.Flush();
        }

        /// <summary>
        /// Read dos type from boot block, partition dos type is used if boot block doesn't have a Fast File
        /// System dos type.
        /// </summary>
        private static async Task<uint> ReadDosType(PartitionBlockIo io, PartitionBlock partitionBlock)
        {
            var bootBlock = await io.Read(0);
            var dosType = GetUInt32(bootBlock, 0);
            if ((dosType & 0xffffff00) == 0x444f5300 && (dosType & 0xff) <= 7)
            {
                return dosType;
            }

            return BigEndianConverter.ConvertBytesToUInt32(partitionBlock.DosType);
        }

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static void SetUInt32(byte[] data, int offset, uint value) =>
            BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);
    }
}
