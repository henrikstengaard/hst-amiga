namespace Hst.Amiga.FileSystems.Pfs3.Resizer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;
    using Constants = Pfs3.Constants;

    /// <summary>
    /// Resize partition with PFS3 within the cylinders available before and after the partition. The reserved
    /// area at the start of the partition is not changed. When expanding, bitmap blocks and bitmap index blocks
    /// are added in the reserved area. When shrinking, file data in the area cut off is relocated to free blocks,
    /// deleted files in the area cut off are removed from the deldir and surplus bitmap blocks are freed.
    /// </summary>
    public static class Pfs3Resizer
    {
        private const int SectorSize = 512;
        private const int CopyChunkBlocks = 2048;
        private const int OffsetRootBitmapIndex = 0x60;
        private const int OffsetRootSmallIndexBlocks = OffsetRootBitmapIndex + (Constants.MAXSMALLBITMAPINDEX + 1) * 4;
        private const int OffsetExtensionSuperIndex = 0x40;
        private const int OffsetExtensionDelDir = 0x90;
        private const int AnodeSize = 12;
        private const int DelDirEntrySize = 32;
        private const int OffsetDelDirEntries = 0x20;

        /// <summary>
        /// Resize partition to new cylinders. File system is checked before and after the resize.
        /// </summary>
        /// <param name="stream">Stream with disk containing rigid disk block.</param>
        /// <param name="partitionNumber">Partition number starting from 1.</param>
        /// <param name="options">Resize options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task<PartitionOperationResult> Resize(Stream stream, int partitionNumber,
            PartitionResizeOptions options, CancellationToken cancellationToken = default) =>
            PartitionOperation.Resize(stream, Pfs3Partition.Instance, partitionNumber, options, cancellationToken);

        internal static async Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders,
            bool dryRun, IList<string> messages, CancellationToken cancellationToken)
        {
            if (partitionBlock.BlockSize != SectorSize)
            {
                throw new PartitionOperationException(
                    $"Resizing PFS3 partition with block size {partitionBlock.BlockSize} is not supported");
            }

            var io = new PartitionBlockIo(stream, PartitionGeometry.GetStartOffset(partitionBlock), SectorSize,
                cancellationToken);
            var oldTotalLong = PartitionGeometry.GetSize(partitionBlock) / SectorSize;
            var newTotalLong = newCylinders * PartitionGeometry.GetCylinderSize(partitionBlock) / SectorSize;
            if (newTotalLong > uint.MaxValue)
            {
                throw new PartitionOperationException("New size is too large for PFS3");
            }

            var oldTotal = (uint)oldTotalLong;
            var newTotal = (uint)newTotalLong;

            /* root block */
            var root = await io.Read(Constants.ROOTBLOCK);
            var diskType = GetInt32(root, 0);
            if (diskType != Constants.ID_PFS_DISK && diskType != Constants.ID_PFS2_DISK)
            {
                throw new PartitionOperationException("PFS3 root block is invalid, run doctor to repair");
            }

            var options = GetUInt32(root, 4);
            var datestamp = GetUInt32(root, 8);
            var lastReserved = GetUInt32(root, 0x34);
            var firstReserved = GetUInt32(root, 0x38);
            var reservedFree = GetUInt32(root, 0x3c);
            var resBlkSize = (int)GetUInt16(root, 0x40);
            var rblkCluster = GetUInt16(root, 0x42);
            var alwaysFree = GetUInt32(root, 0x48);
            var diskSize = GetUInt32(root, 0x54);
            var extension = GetUInt32(root, 0x58);

            if ((options & Constants.MODE_HARDDISK) == 0 || (options & Constants.MODE_SPLITTED_ANODES) == 0)
            {
                throw new PartitionOperationException(
                    "Resizing PFS3 volume without hard disk mode and splitted anodes is not supported");
            }

            if ((options & Constants.MODE_SIZEFIELD) != 0 && diskSize != oldTotal)
            {
                throw new PartitionOperationException(
                    $"PFS3 root block has disk size {diskSize}, partition has {oldTotal} blocks, run doctor to repair");
            }

            if (resBlkSize < 1024 || resBlkSize % SectorSize != 0)
            {
                throw new PartitionOperationException($"PFS3 reserved block size {resBlkSize} is invalid");
            }

            var superIndex = (options & Constants.MODE_SUPERINDEX) != 0;
            var resCluster = (uint)(resBlkSize / SectorSize);
            var longsPerBmb = resBlkSize / 4 - 3;
            var indexPerBlock = longsPerBmb;
            var anodesPerBlock = (resBlkSize - 16) / AnodeSize;
            var bitmapStart = lastReserved + 1;
            var maxBitmapIndex = superIndex ? Constants.MAXBITMAPINDEX + 1 : Constants.MAXSMALLBITMAPINDEX + 1;
            var numReserved = (lastReserved - firstReserved + 1) / resCluster;

            /* limits */
            long maxDiskSize;
            if (!superIndex)
            {
                maxDiskSize = Constants.MAXSMALLDISK;
            }
            else
            {
                maxDiskSize = resBlkSize == 1024 ? Constants.MAXDISKSIZE1K :
                    resBlkSize == 2048 ? Constants.MAXDISKSIZE2K : Constants.MAXDISKSIZE4K;
            }

            if (newTotal > maxDiskSize)
            {
                throw new PartitionOperationException(
                    $"New size of {newTotal} blocks is larger than max size of {maxDiskSize} blocks for PFS3 volume formatted with {resBlkSize} bytes reserved blocks{(superIndex ? string.Empty : " without super index")}");
            }

            var reservedSize = lastReserved / resCluster - firstReserved + 1;
            if (newTotal <= bitmapStart || reservedSize > newTotal / 8)
            {
                throw new PartitionOperationException(
                    $"New size of {newTotal} blocks is too small for PFS3 reserved area of {lastReserved + 1} blocks");
            }

            /* reserved bitmap in root block cluster */
            var reservedBitmapLongs = (int)(numReserved / 32 + 1);
            var reservedBitmapSectors = Math.Max(1, rblkCluster - 1);
            var reservedBitmap = await io.Read(Constants.ROOTBLOCK + 1, reservedBitmapSectors);

            bool IsReservedFree(uint block)
            {
                var bit = (block - firstReserved) / resCluster;
                return (GetUInt32(reservedBitmap, 12 + (int)(bit / 32) * 4) & (1U << (int)(31 - bit % 32))) != 0;
            }

            void SetReserved(uint block, bool free)
            {
                var bit = (block - firstReserved) / resCluster;
                var offset = 12 + (int)(bit / 32) * 4;
                var mask = 1U << (int)(31 - bit % 32);
                var value = GetUInt32(reservedBitmap, offset);
                SetUInt32(reservedBitmap, offset, free ? value | mask : value & ~mask);
            }

            uint AllocateReserved()
            {
                for (uint i = 0; i < numReserved && i / 32 < reservedBitmapLongs; i++)
                {
                    var block = firstReserved + i * resCluster;
                    if (block > lastReserved || !IsReservedFree(block))
                    {
                        continue;
                    }

                    SetReserved(block, false);
                    reservedFree--;
                    return block;
                }

                throw new PartitionOperationException(
                    "PFS3 reserved area has no free blocks for bitmap blocks, size can't be increased");
            }

            void FreeReserved(uint block)
            {
                SetReserved(block, true);
                reservedFree++;
            }

            /* reserved blocks changed */
            var changedReserved = new Dictionary<uint, byte[]>();

            async Task<byte[]> ReadReserved(uint block, ushort id)
            {
                if (block < firstReserved || block > lastReserved)
                {
                    throw new PartitionOperationException(
                        $"Reserved block {block} is outside reserved area, run doctor to repair");
                }

                if (changedReserved.TryGetValue(block, out var changed))
                {
                    return changed;
                }

                var data = await io.Read(block, (int)resCluster);
                if (GetUInt16(data, 0) != id)
                {
                    throw new PartitionOperationException(
                        $"Reserved block {block} has id 0x{GetUInt16(data, 0):x4}, expected 0x{id:x4}, run doctor to repair");
                }

                return data;
            }

            /* bitmap index and bitmap blocks */
            var oldBitmapBlocks = CalculateBitmapBlocks(oldTotal, bitmapStart, longsPerBmb);
            var newBitmapBlocks = CalculateBitmapBlocks(newTotal, bitmapStart, longsPerBmb);
            var oldIndexBlocks = (oldBitmapBlocks + indexPerBlock - 1) / indexPerBlock;
            var newIndexBlocks = (newBitmapBlocks + indexPerBlock - 1) / indexPerBlock;

            if (newIndexBlocks > maxBitmapIndex)
            {
                throw new PartitionOperationException(
                    $"New size needs {newIndexBlocks} bitmap index blocks, PFS3 volume has max {maxBitmapIndex}");
            }

            var bitmapIndexBlocks = new List<uint>();
            for (var i = 0; i < oldIndexBlocks; i++)
            {
                var block = GetUInt32(root, OffsetRootBitmapIndex + i * 4);
                await ReadReserved(block, Constants.BMIBLKID);
                bitmapIndexBlocks.Add(block);
            }

            var bitmapIndexData = new List<byte[]>();
            foreach (var block in bitmapIndexBlocks)
            {
                bitmapIndexData.Add(await ReadReserved(block, Constants.BMIBLKID));
            }

            var bitmapBlocks = new List<uint>();
            var bitmapData = new List<byte[]>();
            for (var seq = 0; seq < oldBitmapBlocks; seq++)
            {
                var block = GetUInt32(bitmapIndexData[seq / indexPerBlock], 12 + seq % indexPerBlock * 4);
                bitmapBlocks.Add(block);
                bitmapData.Add(await ReadReserved(block, Constants.BMBLKID));
            }

            /* data blocks used, from bitmap */
            var used = new BitArray((int)Math.Max(oldTotal, newTotal));

            bool IsBitmapFree(uint block)
            {
                var bit = block - bitmapStart;
                var data = bitmapData[(int)(bit / 32 / longsPerBmb)];
                var offset = 12 + (int)(bit / 32 % longsPerBmb) * 4;
                return (GetUInt32(data, offset) & (1U << (int)(31 - bit % 32))) != 0;
            }

            for (var block = bitmapStart; block < oldTotal; block++)
            {
                used[(int)block] = !IsBitmapFree(block);
            }

            /* anodes */
            var anodeBlocks = await ReadAnodeBlocks(root, extension, superIndex, indexPerBlock, ReadReserved);

            var anodeData = new Dictionary<uint, byte[]>();
            foreach (var entry in anodeBlocks)
            {
                anodeData[entry.Value] = await ReadReserved(entry.Value, Constants.ABLKID);
            }

            (byte[] Data, int Offset) GetAnode(uint anodenr)
            {
                var seq = anodenr >> 16;
                var offset = (int)(anodenr & 0xffff);
                if (!anodeBlocks.TryGetValue(seq, out var block) || offset >= anodesPerBlock)
                {
                    throw new PartitionOperationException($"Anode {anodenr:x} is invalid, run doctor to repair");
                }

                return (anodeData[block], 16 + offset * AnodeSize);
            }

            var cut = newTotal;
            var moves = new List<(uint Source, uint Destination, uint Count)>();
            var changedAnodeBlocks = new HashSet<uint>();
            var deletedChainsCleared = 0;
            uint searchStart = bitmapStart;

            if (newTotal < oldTotal)
            {
                /* deleted files with blocks in area cut off are removed from deldir */
                var deletedAnodes = new HashSet<uint>();
                foreach (var delDirBlock in await GetDelDirBlocks(root, extension, options, io))
                {
                    var data = await ReadReserved(delDirBlock, Constants.DELDIRID);
                    var changed = false;
                    var entries = (resBlkSize - OffsetDelDirEntries) / DelDirEntrySize;
                    for (var i = 0; i < entries; i++)
                    {
                        var entryOffset = OffsetDelDirEntries + i * DelDirEntrySize;
                        var anodenr = GetUInt32(data, entryOffset);
                        if (anodenr == 0)
                        {
                            continue;
                        }

                        var chain = new List<uint>();
                        var outside = false;
                        for (var nr = anodenr; nr != 0 && chain.Count <= used.Length;)
                        {
                            var anode = GetAnode(nr);
                            chain.Add(nr);
                            var clusterSize = GetUInt32(anode.Data, anode.Offset);
                            var blockNr = GetUInt32(anode.Data, anode.Offset + 4);
                            if ((ulong)blockNr + clusterSize > cut)
                            {
                                outside = true;
                            }

                            nr = GetUInt32(anode.Data, anode.Offset + 8);
                        }

                        if (!outside)
                        {
                            continue;
                        }

                        SetUInt32(data, entryOffset, 0);
                        changed = true;
                        deletedChainsCleared++;
                        foreach (var nr in chain)
                        {
                            deletedAnodes.Add(nr);
                        }
                    }

                    if (changed)
                    {
                        changedReserved[delDirBlock] = data;
                    }
                }

                /* free anodes of deleted files removed from deldir */
                foreach (var nr in deletedAnodes)
                {
                    var anode = GetAnode(nr);
                    Array.Clear(anode.Data, anode.Offset, AnodeSize);
                    changedAnodeBlocks.Add(anodeBlocks[nr >> 16]);
                }

                /* free anode slots */
                var freeAnodes = new Queue<uint>();
                foreach (var entry in anodeBlocks.OrderBy(x => x.Key))
                {
                    var data = anodeData[entry.Value];
                    for (var i = 0; i < anodesPerBlock; i++)
                    {
                        var anodenr = (entry.Key << 16) | (uint)i;
                        if (anodenr < Constants.ANODE_USERFIRST || deletedAnodes.Contains(anodenr))
                        {
                            continue;
                        }

                        var offset = 16 + i * AnodeSize;
                        if (GetUInt32(data, offset) == 0 && GetUInt32(data, offset + 4) == 0 &&
                            GetUInt32(data, offset + 8) == 0)
                        {
                            freeAnodes.Enqueue(anodenr);
                        }
                    }
                }

                (uint Start, uint Count) AllocateRun(uint count)
                {
                    while (searchStart < cut && used[(int)searchStart])
                    {
                        searchStart++;
                    }

                    if (searchStart >= cut)
                    {
                        throw new PartitionOperationException(
                            "PFS3 doesn't fit in new size, not enough free blocks to relocate data");
                    }

                    var start = searchStart;
                    uint allocated = 0;
                    while (allocated < count && searchStart < cut && !used[(int)searchStart])
                    {
                        used[(int)searchStart] = true;
                        searchStart++;
                        allocated++;
                    }

                    return (start, allocated);
                }

                /* used blocks to relocate */
                long usedAfterCut = 0;
                long freeBeforeCut = 0;
                for (var block = bitmapStart; block < oldTotal; block++)
                {
                    if (block >= cut && used[(int)block])
                    {
                        usedAfterCut++;
                    }
                    else if (block < cut && !used[(int)block])
                    {
                        freeBeforeCut++;
                    }
                }

                if (usedAfterCut > freeBeforeCut)
                {
                    throw new PartitionOperationException(
                        $"PFS3 doesn't fit in new size, {usedAfterCut} blocks used in area cut off and {freeBeforeCut} blocks free");
                }

                /* relocate data of anodes in area cut off */
                foreach (var entry in anodeBlocks.OrderBy(x => x.Key).ToList())
                {
                    var data = anodeData[entry.Value];
                    for (var i = 0; i < anodesPerBlock; i++)
                    {
                        var offset = 16 + i * AnodeSize;
                        var clusterSize = GetUInt32(data, offset);
                        var blockNr = GetUInt32(data, offset + 4);
                        if (clusterSize == 0 || blockNr == 0xffffffff || blockNr <= lastReserved ||
                            (ulong)blockNr + clusterSize <= cut)
                        {
                            continue;
                        }

                        var keep = blockNr < cut ? cut - blockNr : 0;
                        var source = blockNr + keep;
                        var remaining = clusterSize - keep;
                        var next = GetUInt32(data, offset + 8);

                        /* pieces of data to relocate, first piece is stored in anode, if no blocks are kept */
                        var pieces = new List<(uint Start, uint Count)>();
                        while (remaining > 0)
                        {
                            var run = AllocateRun(remaining);
                            moves.Add((source, run.Start, run.Count));
                            pieces.Add(run);
                            source += run.Count;
                            remaining -= run.Count;
                        }

                        var currentData = data;
                        var currentOffset = offset;
                        var pieceIndex = 0;
                        if (keep > 0)
                        {
                            SetUInt32(currentData, currentOffset, keep);
                        }
                        else
                        {
                            SetUInt32(currentData, currentOffset, pieces[0].Count);
                            SetUInt32(currentData, currentOffset + 4, pieces[0].Start);
                            pieceIndex = 1;
                        }

                        changedAnodeBlocks.Add(entry.Value);

                        for (; pieceIndex < pieces.Count; pieceIndex++)
                        {
                            if (freeAnodes.Count == 0)
                            {
                                throw new PartitionOperationException(
                                    "PFS3 has no free anodes to relocate fragmented data");
                            }

                            var anodenr = freeAnodes.Dequeue();
                            SetUInt32(currentData, currentOffset + 8, anodenr);
                            var anode = GetAnode(anodenr);
                            SetUInt32(anode.Data, anode.Offset, pieces[pieceIndex].Count);
                            SetUInt32(anode.Data, anode.Offset + 4, pieces[pieceIndex].Start);
                            changedAnodeBlocks.Add(anodeBlocks[anodenr >> 16]);
                            currentData = anode.Data;
                            currentOffset = anode.Offset;
                        }

                        SetUInt32(currentData, currentOffset + 8, next);
                    }
                }

                /* all used blocks in area cut off must be relocated */
                var relocated = new BitArray(used.Length);
                foreach (var move in moves)
                {
                    for (uint b = 0; b < move.Count; b++)
                    {
                        relocated[(int)(move.Source + b)] = true;
                    }
                }

                for (var block = cut; block < oldTotal; block++)
                {
                    if (used[(int)block] && !relocated[(int)block])
                    {
                        throw new PartitionOperationException(
                            $"Block {block} is used, but not by any anode, run doctor to repair");
                    }
                }
            }

            /* bitmap blocks for new size */
            var newBitmapIndexBlocks = new List<uint>();
            var newBitmapBlockNumbers = new List<uint>();
            var freedReserved = new List<uint>();
            if (newBitmapBlocks > oldBitmapBlocks)
            {
                for (var i = oldIndexBlocks; i < newIndexBlocks; i++)
                {
                    newBitmapIndexBlocks.Add(AllocateReserved());
                }

                for (var i = oldBitmapBlocks; i < newBitmapBlocks; i++)
                {
                    newBitmapBlockNumbers.Add(AllocateReserved());
                }
            }
            else
            {
                for (var i = newBitmapBlocks; i < oldBitmapBlocks; i++)
                {
                    freedReserved.Add(bitmapBlocks[i]);
                }

                for (var i = newIndexBlocks; i < oldIndexBlocks; i++)
                {
                    freedReserved.Add(bitmapIndexBlocks[i]);
                }

                foreach (var block in freedReserved)
                {
                    FreeReserved(block);
                }
            }

            messages.Add(
                $"Resize PFS3 from {oldTotal} to {newTotal} blocks, relocate {moves.Sum(x => (long)x.Count)} data blocks, remove {deletedChainsCleared} deleted files, {newBitmapBlocks} bitmap blocks");

            if (dryRun)
            {
                return;
            }

            /* build bitmap for new size */
            var allBitmapIndexBlocks = bitmapIndexBlocks.Take(Math.Min(oldIndexBlocks, newIndexBlocks))
                .Concat(newBitmapIndexBlocks).ToList();
            var allBitmapBlocks = bitmapBlocks.Take(Math.Min(oldBitmapBlocks, newBitmapBlocks))
                .Concat(newBitmapBlockNumbers).ToList();

            var newBitmapData = new List<byte[]>();
            uint blocksFree = 0;
            for (var seq = 0; seq < newBitmapBlocks; seq++)
            {
                var data = new byte[resBlkSize];
                SetUInt16(data, 0, Constants.BMBLKID);
                SetUInt32(data, 4, datestamp);
                SetUInt32(data, 8, (uint)seq);
                for (var l = 0; l < longsPerBmb; l++)
                {
                    uint value = 0;
                    for (var b = 0; b < 32; b++)
                    {
                        var block = bitmapStart + ((long)seq * longsPerBmb + l) * 32 + b;
                        var free = block >= newTotal || !used[(int)block];
                        if (free)
                        {
                            value |= 1U << (31 - b);
                            if (block < newTotal)
                            {
                                blocksFree++;
                            }
                        }
                    }

                    SetUInt32(data, 12 + l * 4, value);
                }

                newBitmapData.Add(data);
            }

            var newIndexData = new List<byte[]>();
            for (var i = 0; i < newIndexBlocks; i++)
            {
                var data = i < bitmapIndexData.Count ? bitmapIndexData[i] : NewIndexBlock(resBlkSize, datestamp, i);
                for (var e = 0; e < indexPerBlock; e++)
                {
                    var seq = i * indexPerBlock + e;
                    SetUInt32(data, 12 + e * 4, seq < newBitmapBlocks ? allBitmapBlocks[seq] : 0);
                }

                newIndexData.Add(data);
            }

            /* root block */
            for (var i = 0; i < maxBitmapIndex; i++)
            {
                SetUInt32(root, OffsetRootBitmapIndex + i * 4, i < newIndexBlocks ? allBitmapIndexBlocks[i] : 0);
            }

            var newAlwaysFree = diskSize > 0 ? (uint)((ulong)alwaysFree * newTotal / diskSize) : newTotal / 20;
            if (newAlwaysFree < newTotal / 40 || newAlwaysFree > newTotal / 15)
            {
                newAlwaysFree = newTotal / 20;
            }

            SetUInt32(root, 0x3c, reservedFree);
            SetUInt32(root, 0x44, blocksFree);
            SetUInt32(root, 0x48, newAlwaysFree);
            SetUInt32(root, 0x4c, 0);
            SetUInt32(root, 0x54, newTotal);

            /* write relocated data, destination blocks are free */
            foreach (var move in moves)
            {
                for (uint offset = 0; offset < move.Count; offset += CopyChunkBlocks)
                {
                    var count = (int)Math.Min(CopyChunkBlocks, move.Count - offset);
                    await io.Copy(move.Source + offset, move.Destination + offset, count);
                }
            }

            /* write anode blocks, deldir blocks, bitmap blocks, bitmap index blocks */
            foreach (var block in changedAnodeBlocks)
            {
                changedReserved[block] = anodeData[block];
            }

            for (var i = 0; i < newBitmapBlocks; i++)
            {
                changedReserved[allBitmapBlocks[i]] = newBitmapData[i];
            }

            for (var i = 0; i < newIndexBlocks; i++)
            {
                changedReserved[allBitmapIndexBlocks[i]] = newIndexData[i];
            }

            foreach (var entry in changedReserved.OrderBy(x => x.Key))
            {
                await io.Write(entry.Key, entry.Value);
            }

            /* reserved bitmap and root block are written last */
            await io.Write(Constants.ROOTBLOCK + 1, reservedBitmap);
            await io.Write(Constants.ROOTBLOCK, root);
            await io.Flush();
        }

        private static int CalculateBitmapBlocks(uint totalBlocks, uint bitmapStart, int longsPerBmb)
        {
            var longs = ((long)totalBlocks - bitmapStart + 31) / 32;
            return (int)((longs + longsPerBmb - 1) / longsPerBmb);
        }

        private static byte[] NewIndexBlock(int resBlkSize, uint datestamp, int seq)
        {
            var data = new byte[resBlkSize];
            SetUInt16(data, 0, Constants.BMIBLKID);
            SetUInt32(data, 4, datestamp);
            SetUInt32(data, 8, (uint)seq);
            return data;
        }

        /// <summary>
        /// Read anode blocks by sequence number through index blocks or super blocks and index blocks.
        /// </summary>
        private static async Task<Dictionary<uint, uint>> ReadAnodeBlocks(byte[] root, uint extension,
            bool superIndex, int indexPerBlock, Func<uint, ushort, Task<byte[]>> readReserved)
        {
            var indexBlocks = new List<(uint Seq, uint Block)>();
            if (superIndex)
            {
                var rootExtension = await readReserved(extension, Constants.EXTENSIONID);
                for (uint s = 0; s <= Constants.MAXSUPER; s++)
                {
                    var superBlock = GetUInt32(rootExtension, OffsetExtensionSuperIndex + (int)s * 4);
                    if (superBlock == 0)
                    {
                        continue;
                    }

                    var data = await readReserved(superBlock, Constants.SBLKID);
                    for (uint i = 0; i < indexPerBlock; i++)
                    {
                        var indexBlock = GetUInt32(data, 12 + (int)i * 4);
                        if (indexBlock != 0)
                        {
                            indexBlocks.Add((s * (uint)indexPerBlock + i, indexBlock));
                        }
                    }
                }
            }
            else
            {
                for (uint i = 0; i <= Constants.MAXSMALLINDEXNR; i++)
                {
                    var indexBlock = GetUInt32(root, OffsetRootSmallIndexBlocks + (int)i * 4);
                    if (indexBlock != 0)
                    {
                        indexBlocks.Add((i, indexBlock));
                    }
                }
            }

            var anodeBlocks = new Dictionary<uint, uint>();
            foreach (var (seq, block) in indexBlocks)
            {
                var data = await readReserved(block, Constants.IBLKID);
                for (uint i = 0; i < indexPerBlock; i++)
                {
                    var anodeBlock = GetUInt32(data, 12 + (int)i * 4);
                    if (anodeBlock != 0)
                    {
                        anodeBlocks[seq * (uint)indexPerBlock + i] = anodeBlock;
                    }
                }
            }

            return anodeBlocks;
        }

        private static async Task<IList<uint>> GetDelDirBlocks(byte[] root, uint extension, uint options,
            PartitionBlockIo io)
        {
            var blocks = new List<uint>();
            if ((options & Constants.MODE_DELDIR) == 0)
            {
                return blocks;
            }

            if ((options & Constants.MODE_SUPERDELDIR) != 0 && extension != 0)
            {
                var rootExtension = await io.Read(extension);
                if (GetUInt16(rootExtension, 0) == Constants.EXTENSIONID)
                {
                    for (var i = 0; i < 32; i++)
                    {
                        var block = GetUInt32(rootExtension, OffsetExtensionDelDir + i * 4);
                        if (block != 0)
                        {
                            blocks.Add(block);
                        }
                    }
                }
            }
            else if (GetUInt32(root, 0x50) != 0)
            {
                blocks.Add(GetUInt32(root, 0x50));
            }

            return blocks;
        }

        private static ushort GetUInt16(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt16(data, offset);

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static int GetInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToInt32(data, offset);

        private static void SetUInt16(byte[] data, int offset, ushort value) =>
            BigEndianConverter.ConvertUInt16ToBytes(value, data, offset);

        private static void SetUInt32(byte[] data, int offset, uint value) =>
            BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);
    }
}
