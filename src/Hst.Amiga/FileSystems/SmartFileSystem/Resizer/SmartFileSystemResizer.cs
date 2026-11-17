namespace Hst.Amiga.FileSystems.SmartFileSystem.Resizer
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
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;
    using Constants = SmartFileSystem.Constants;

    /// <summary>
    /// Resize partition with Smart File System within the cylinders available before and after the partition.
    /// When shrinking, data extents and administration blocks in the area cut off are relocated to free blocks and
    /// the extent B-tree is rebuilt. When expanding, the bitmap is moved, if it needs more blocks than are free
    /// after it. Both root blocks are updated with the new size.
    /// </summary>
    public static class SmartFileSystemResizer
    {
        private const int CopyChunkBlocks = 2048;

        /// <summary>
        /// Resize partition to new cylinders. File system is checked before and after the resize.
        /// </summary>
        /// <param name="stream">Stream with disk containing rigid disk block.</param>
        /// <param name="partitionNumber">Partition number starting from 1.</param>
        /// <param name="options">Resize options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task<PartitionOperationResult> Resize(Stream stream, int partitionNumber,
            PartitionResizeOptions options, CancellationToken cancellationToken = default) =>
            PartitionOperation.Resize(stream, SmartFileSystemPartition.Instance, partitionNumber, options,
                cancellationToken);

        internal static async Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders,
            bool dryRun, IList<string> messages, CancellationToken cancellationToken)
        {
            var geometry = SmartFileSystemHelper.GetGeometry(partitionBlock);
            var blockSize = geometry.BlockSize;
            var io = new PartitionBlockIo(stream, geometry.FirstByte, blockSize, cancellationToken);

            var oldTotal = (uint)((geometry.LastByte - geometry.FirstByte) / blockSize);
            var newSize = newCylinders * PartitionGeometry.GetCylinderSize(partitionBlock);
            var newTotalLong = newSize / blockSize;
            var reserved = Math.Max(partitionBlock.Reserved, 1);
            var preAlloc = Math.Max(partitionBlock.PreAlloc, 1);
            var shifts = SmartFileSystemHelper.Log2(blockSize) - Constants.BlckFAccuracy;

            if (newTotalLong > uint.MaxValue || ((ulong)newTotalLong << shifts) > uint.MaxValue)
            {
                throw new PartitionOperationException(
                    "New size is too large for Smart File System, max partition size is 128 GB");
            }

            var newTotal = (uint)newTotalLong;
            var newBitmapCount = Bitmap.CalculateBitmapBlocks(newTotal, blockSize);
            if (newTotal <= reserved + Constants.BlocksAdmin + newBitmapCount + preAlloc + Constants.BlocksAdmin)
            {
                throw new PartitionOperationException($"New size of {newTotal} blocks is too small");
            }

            var layout = await SmartFileSystemBlockWalker.Load(io, oldTotal, cancellationToken);
            var cut = newTotal - preAlloc;

            /* blocks used after resize */
            var final = new BitArray((int)newTotal);
            var searchStart = reserved;

            void Mark(uint start, uint count)
            {
                for (var block = start; block < start + count && block < newTotal; block++)
                {
                    final[(int)block] = true;
                }
            }

            bool IsFree(uint start, uint count)
            {
                if ((ulong)start + count > cut)
                {
                    return false;
                }

                for (var block = start; block < start + count; block++)
                {
                    if (final[(int)block])
                    {
                        return false;
                    }
                }

                return true;
            }

            uint FindRun(uint count, string purpose)
            {
                uint run = 0;
                for (var block = reserved; block < cut; block++)
                {
                    run = final[(int)block] ? 0 : run + 1;
                    if (run == count)
                    {
                        var start = block - count + 1;
                        Mark(start, count);
                        return start;
                    }
                }

                throw new PartitionOperationException(
                    $"Smart File System doesn't fit in new size, no {count} free blocks for {purpose}");
            }

            /* allocate up to count free blocks, returns start and number of blocks allocated */
            (uint Start, uint Count) AllocateRun(uint count)
            {
                while (searchStart < cut && final[(int)searchStart])
                {
                    searchStart++;
                }

                if (searchStart >= cut)
                {
                    throw new PartitionOperationException(
                        "Smart File System doesn't fit in new size, not enough free blocks to relocate data");
                }

                var start = searchStart;
                uint allocated = 0;
                while (allocated < count && searchStart < cut && !final[(int)searchStart])
                {
                    final[(int)searchStart] = true;
                    searchStart++;
                    allocated++;
                }

                return (start, allocated);
            }

            Mark(0, reserved);
            Mark(cut, preAlloc);

            /* admin spaces inside new size are kept, admin spaces in area cut off are removed */
            var keptSpaces = layout.AdminSpaces.Where(x => x.Space + Constants.BlocksAdmin <= cut).ToList();
            var removedSpaces = layout.AdminSpaces.Where(x => x.Space + Constants.BlocksAdmin > cut).ToList();
            foreach (var adminSpace in keptSpaces)
            {
                Mark(adminSpace.Space, Constants.BlocksAdmin);
            }

            /* data blocks inside new size are kept */
            foreach (var extent in layout.Extents.Values)
            {
                var end = Math.Min((ulong)extent.Key + extent.Blocks, cut);
                if (extent.Key < end)
                {
                    Mark(extent.Key, (uint)(end - extent.Key));
                }
            }

            /* bitmap stays, if new bitmap fits at bitmap base */
            var bitmapBase = layout.BitmapBase;
            if (bitmapBase >= reserved && IsFree(bitmapBase, (uint)newBitmapCount))
            {
                Mark(bitmapBase, (uint)newBitmapCount);
            }
            else
            {
                bitmapBase = FindRun((uint)newBitmapCount, "bitmap");
            }

            /* administration blocks */
            var adminUsed = new HashSet<uint>(layout.AdminBlocks);
            var freeAdminBlocks = new Queue<uint>(keptSpaces
                .SelectMany(x => Enumerable.Range(0, Constants.BlocksAdmin).Select(i => x.Space + (uint)i))
                .Where(x => !adminUsed.Contains(x)).OrderBy(x => x));
            var entriesPerContainer =
                (blockSize - Constants.AdminSpaceContainerHeaderSize) / Constants.AdminSpaceSize;
            var freeEntries = layout.AdminContainers.Count * entriesPerContainer - keptSpaces.Count;
            var newSpaces = new List<uint>();
            var newContainers = new List<uint>();

            uint AllocateAdmin()
            {
                if (freeAdminBlocks.Count == 0)
                {
                    var space = FindRun(Constants.BlocksAdmin, "admin space");
                    newSpaces.Add(space);
                    var first = 0U;
                    if (freeEntries == 0)
                    {
                        /* first block of admin space is a new admin space container */
                        newContainers.Add(space);
                        adminUsed.Add(space);
                        freeEntries += entriesPerContainer;
                        first = 1;
                    }

                    freeEntries--;
                    for (var i = first; i < Constants.BlocksAdmin; i++)
                    {
                        freeAdminBlocks.Enqueue(space + i);
                    }
                }

                var block = freeAdminBlocks.Dequeue();
                adminUsed.Add(block);
                return block;
            }

            var map = new Dictionary<uint, uint>();
            foreach (var block in layout.AdminBlocks.Where(x => removedSpaces.Any(s =>
                         x >= s.Space && x < s.Space + Constants.BlocksAdmin)).OrderBy(x => x))
            {
                if (block == layout.RootObjectContainer || block == layout.TransactionBlock)
                {
                    throw new PartitionOperationException(
                        "Root object container and transaction block can't be relocated");
                }

                adminUsed.Remove(block);
                map[block] = AllocateAdmin();
            }

            /* relocate data extents after new end */
            var moves = new List<(uint Source, uint Destination, uint Count)>();
            var newExtents = new SortedDictionary<uint, ExtentBNode>();
            var newFirstExtents = new Dictionary<FileObject, uint>();
            var reached = new HashSet<uint>();

            foreach (var file in layout.Files)
            {
                var chain = new List<ExtentBNode>();
                var key = file.FirstExtent;
                while (key != 0)
                {
                    if (!layout.Extents.TryGetValue(key, out var extent) || !reached.Add(key))
                    {
                        throw new PartitionOperationException(
                            $"File extent {key} is missing or used more than once, run doctor to repair");
                    }

                    chain.Add(extent);
                    key = extent.Next;
                }

                var segments = new List<(uint Key, uint Blocks)>();
                var changed = false;
                foreach (var extent in chain)
                {
                    var keep = extent.Key < cut ? Math.Min(extent.Blocks, cut - extent.Key) : 0;
                    if (keep > 0)
                    {
                        segments.Add((extent.Key, keep));
                    }

                    var source = extent.Key + keep;
                    var remaining = extent.Blocks - keep;
                    while (remaining > 0)
                    {
                        changed = true;
                        var run = AllocateRun(remaining);
                        moves.Add((source, run.Start, run.Count));
                        segments.Add((run.Start, run.Count));
                        source += run.Count;
                        remaining -= run.Count;
                    }
                }

                if (!changed)
                {
                    foreach (var extent in chain)
                    {
                        newExtents[extent.Key] = extent;
                    }

                    continue;
                }

                for (var i = 0; i < segments.Count; i++)
                {
                    newExtents[segments[i].Key] = new ExtentBNode
                    {
                        Key = segments[i].Key,
                        Blocks = (ushort)segments[i].Blocks,
                        Next = i < segments.Count - 1 ? segments[i + 1].Key : 0,
                        Prev = i == 0 ? chain[0].Prev : segments[i - 1].Key
                    };
                }

                if (segments[0].Key != file.FirstExtent)
                {
                    newFirstExtents[file] = segments[0].Key;
                }
            }

            foreach (var extent in layout.Extents.Values.Where(x => !reached.Contains(x.Key)))
            {
                if ((ulong)extent.Key + extent.Blocks > cut)
                {
                    throw new PartitionOperationException(
                        $"Extent {extent.Key} is not used by any file, run doctor to repair");
                }

                newExtents[extent.Key] = extent;
            }

            var extentsChanged = moves.Count > 0;

            /* rebuild extent B-tree, if extents are changed */
            var bNodeBlocks = new Dictionary<uint, byte[]>();
            if (extentsChanged)
            {
                var root = Mapped(map, layout.ExtentBNodeRoot);
                var pool = new Queue<uint>(layout.BNodeContainers.Where(x => x != layout.ExtentBNodeRoot)
                    .Select(x => Mapped(map, x)));

                uint NextBNodeBlock() => pool.Count > 0 ? pool.Dequeue() : AllocateAdmin();

                var leafCapacity = (blockSize - Constants.BNodeContainerHeaderSize) / Constants.ExtentBNodeSize;
                var internalCapacity = (blockSize - Constants.BNodeContainerHeaderSize) / Constants.BNodeSize;
                var nodes = newExtents.Values.Cast<BNode>().ToList();
                var isLeaf = true;
                while (nodes.Count > (isLeaf ? leafCapacity : internalCapacity))
                {
                    var level = new List<BNode>();
                    foreach (var group in SplitEvenly(nodes, isLeaf ? leafCapacity : internalCapacity))
                    {
                        var block = NextBNodeBlock();
                        bNodeBlocks[block] = BuildBNodeContainer(block, isLeaf, group, blockSize);
                        level.Add(new BNode { Key = group[0].Key, Data = block });
                    }

                    nodes = level;
                    isLeaf = false;
                }

                bNodeBlocks[root] = BuildBNodeContainer(root, isLeaf, nodes, blockSize);

                /* release b-tree blocks not used */
                while (pool.Count > 0)
                {
                    adminUsed.Remove(pool.Dequeue());
                }
            }

            /* admin spaces after resize */
            var finalSpaces = keptSpaces.Select(x => x.Space).Concat(newSpaces).ToList();

            /* free blocks */
            uint freeBlocks = 0;
            for (var block = 0; block < newTotal; block++)
            {
                if (!final[block])
                {
                    freeBlocks++;
                }
            }

            messages.Add(
                $"Resize Smart File System from {oldTotal} to {newTotal} blocks, relocate {map.Count} administration blocks and {moves.Sum(x => (long)x.Count)} data blocks, bitmap at block {bitmapBase}");

            if (dryRun)
            {
                return;
            }

            /* changed blocks by new block number */
            var changedBlocks = new Dictionary<uint, byte[]>();

            async Task<byte[]> GetForChange(uint block)
            {
                var newBlock = Mapped(map, block);
                if (!changedBlocks.TryGetValue(newBlock, out var data))
                {
                    data = block == 0 ? (byte[])layout.RootBlock.Clone() : await io.Read(block);
                    changedBlocks[newBlock] = data;
                }

                return data;
            }

            var bNodeContainers = new HashSet<uint>(layout.BNodeContainers);
            foreach (var reference in layout.References)
            {
                if (!map.TryGetValue(reference.Target, out var target) ||
                    extentsChanged && bNodeContainers.Contains(reference.Holder))
                {
                    continue;
                }

                var data = await GetForChange(reference.Holder);
                if (reference.Kind == BlockReferenceKind.NodeIndex)
                {
                    var mask = (1U << layout.ShiftsBlock32) - 1;
                    SetUInt32(data, reference.Offset, (target << layout.ShiftsBlock32) |
                                                      (GetUInt32(data, reference.Offset) & mask));
                }
                else
                {
                    SetUInt32(data, reference.Offset, target);
                }
            }

            foreach (var entry in newFirstExtents)
            {
                var data = await GetForChange(entry.Key.Container);
                SetUInt32(data, entry.Key.Offset, entry.Value);
            }

            /* admin space containers */
            var containers = layout.AdminContainers.Select(x => (Old: x, New: Mapped(map, x)))
                .Concat(newContainers.Select(x => (Old: 0U, New: x))).ToList();
            var containerData = new List<byte[]>();
            foreach (var container in containers)
            {
                byte[] data;
                if (container.Old != 0)
                {
                    data = await GetForChange(container.Old);
                }
                else
                {
                    data = new byte[blockSize];
                    SetUInt32(data, 0, Constants.AdminSpaceContainerId);
                    SetUInt32(data, 8, container.New);
                    data[0x14] = Constants.BlocksAdmin;
                    changedBlocks[container.New] = data;
                }

                for (var i = 0; i < entriesPerContainer; i++)
                {
                    var offset = Constants.AdminSpaceContainerHeaderSize + i * Constants.AdminSpaceSize;
                    SetUInt32(data, offset, 0);
                    SetUInt32(data, offset + 4, 0);
                }

                containerData.Add(data);
            }

            for (var i = 0; i < containers.Count; i++)
            {
                SetUInt32(containerData[i], 0xc, i < containers.Count - 1 ? containers[i + 1].New : 0);
                SetUInt32(containerData[i], 0x10, i > 0 ? containers[i - 1].New : 0);
            }

            /* kept admin spaces keep container and index, new admin spaces use free entries */
            var usedEntries = new HashSet<(int, int)>();
            foreach (var adminSpace in keptSpaces)
            {
                var containerIndex = layout.AdminContainers.IndexOf(adminSpace.Container);
                usedEntries.Add((containerIndex, adminSpace.Index));
                WriteAdminSpace(containerData[containerIndex], adminSpace.Index, adminSpace.Space, adminUsed);
            }

            var newSpaceIndex = 0;
            for (var c = 0; c < containers.Count && newSpaceIndex < newSpaces.Count; c++)
            {
                for (var i = 0; i < entriesPerContainer && newSpaceIndex < newSpaces.Count; i++)
                {
                    if (usedEntries.Contains((c, i)))
                    {
                        continue;
                    }

                    WriteAdminSpace(containerData[c], i, newSpaces[newSpaceIndex++], adminUsed);
                }
            }

            /* root info in root object container */
            var rootObjectContainer = await GetForChange(layout.RootObjectContainer);
            var rootInfoOffset = blockSize - Constants.RootInfoSize;
            SetUInt32(rootObjectContainer, rootInfoOffset + 8, freeBlocks);
            for (var offset = rootInfoOffset + 16; offset <= rootInfoOffset + 32; offset += 4)
            {
                if (GetUInt32(rootObjectContainer, offset) >= cut)
                {
                    SetUInt32(rootObjectContainer, offset, 0);
                }
            }

            /* extent b-tree containers */
            foreach (var entry in bNodeBlocks)
            {
                changedBlocks[entry.Key] = entry.Value;
            }

            /* root blocks */
            var rootBlock = await GetForChange(0);
            changedBlocks.Remove(0);
            var lastByte = (ulong)geometry.FirstByte + (ulong)newTotal * (ulong)blockSize;
            SmartFileSystemPartition.SetRootPosition(rootBlock, geometry.FirstByte, (long)lastByte);
            SetUInt32(rootBlock, 0x30, newTotal);
            SetUInt32(rootBlock, 0x60, bitmapBase);
            SetUInt32(rootBlock, 8, 0);
            ChecksumHelper.UpdateChecksum(rootBlock);
            var rootBlockCopy = (byte[])rootBlock.Clone();
            SetUInt32(rootBlockCopy, 8, newTotal - 1);
            ChecksumHelper.UpdateChecksum(rootBlockCopy);

            foreach (var data in changedBlocks.Values)
            {
                ChecksumHelper.UpdateChecksum(data);
            }

            /* bitmap blocks, bit set means free */
            var blocksInBitmap = BitmapBlock.BlocksInBitmap(blockSize);
            var bitmapData = new byte[newBitmapCount][];
            for (var i = 0; i < newBitmapCount; i++)
            {
                var bitmapBlock = new BitmapBlock(blockSize) { OwnBlock = bitmapBase + (uint)i };
                for (var bit = 0; bit < blocksInBitmap; bit++)
                {
                    var block = (long)i * blocksInBitmap + bit;
                    if (block < newTotal && !final[(int)block])
                    {
                        bitmapBlock.Map[bit >> 5] |= 0x80000000U >> (bit & 31);
                    }
                }

                bitmapData[i] = BitmapBlockBuilder.Build(bitmapBlock, blockSize);
            }

            /* write relocated data, destination blocks are free */
            foreach (var move in moves)
            {
                for (uint offset = 0; offset < move.Count; offset += CopyChunkBlocks)
                {
                    var count = (int)Math.Min(CopyChunkBlocks, move.Count - offset);
                    await io.Copy(move.Source + offset, move.Destination + offset, count);
                }
            }

            /* write administration blocks, bitmap and root blocks */
            foreach (var entry in changedBlocks.OrderBy(x => x.Key))
            {
                await io.Write(entry.Key, entry.Value);
            }

            for (var i = 0; i < newBitmapCount; i++)
            {
                await io.Write(bitmapBase + (uint)i, bitmapData[i]);
            }

            await io.Write(newTotal - 1, rootBlockCopy);
            await io.Write(0, rootBlock);
            await io.Flush();
        }

        private static void WriteAdminSpace(byte[] containerData, int index, uint space, HashSet<uint> adminUsed)
        {
            uint bits = 0;
            for (var i = 0; i < Constants.BlocksAdmin; i++)
            {
                if (adminUsed.Contains(space + (uint)i))
                {
                    bits |= 0x80000000U >> i;
                }
            }

            var offset = Constants.AdminSpaceContainerHeaderSize + index * Constants.AdminSpaceSize;
            SetUInt32(containerData, offset, space);
            SetUInt32(containerData, offset + 4, bits);
        }

        private static byte[] BuildBNodeContainer(uint block, bool isLeaf, IList<BNode> nodes, int blockSize)
        {
            var container = isLeaf ? BNodeContainer.CreateLeaf() : BNodeContainer.CreateInternal();
            container.OwnBlock = block;
            container.Nodes.AddRange(nodes);
            return BNodeContainerBuilder.Build(container, blockSize);
        }

        private static IEnumerable<IList<T>> SplitEvenly<T>(IList<T> items, int capacity)
        {
            var groups = (items.Count + capacity - 1) / capacity;
            var size = items.Count / groups;
            var remainder = items.Count % groups;
            var offset = 0;
            for (var i = 0; i < groups; i++)
            {
                var count = size + (i < remainder ? 1 : 0);
                yield return items.Skip(offset).Take(count).ToList();
                offset += count;
            }
        }

        private static uint Mapped(IDictionary<uint, uint> map, uint block) =>
            map.TryGetValue(block, out var mapped) ? mapped : block;

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static void SetUInt32(byte[] data, int offset, uint value) =>
            BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);
    }
}
