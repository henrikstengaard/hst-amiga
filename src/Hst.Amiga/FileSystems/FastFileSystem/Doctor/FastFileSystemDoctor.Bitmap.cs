namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Check of bitmap. Bitmap is compared with blocks used by directory tree and rebuilt in repair mode, when
    /// bitmap doesn't match or volume is not validated.
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        private int BitmapLongsPerBlock => volume.BlockSize / 4 - 1;
        private int BitmapBitsPerBlock => BitmapLongsPerBlock * 32;
        private int BitmapPointersPerExtensionBlock => volume.BlockSize / 4 - 1;

        /// <summary>
        /// Number of bitmap blocks. Calculated from number of blocks in volume the same way as when formatting.
        /// </summary>
        private int BitmapBlockCount => (int)((volume.Blocks + (long)BitmapBitsPerBlock - 1) / BitmapBitsPerBlock);

        private uint[] bitmapPointers;
        private bool[] bitmapPointerValid;
        private byte[][] bitmapData;
        private List<uint> bitmapExtensionBlocks;
        private bool bitmapNeedsRebuild;

        /// <summary>
        /// Check bitmap block and bitmap extension block pointers in root block. Bitmap blocks pointing outside
        /// volume, to blocks used by entries or to blocks already used by another bitmap block are relocated in
        /// repair mode.
        /// </summary>
        private async Task CheckBitmapPointers()
        {
            var count = BitmapBlockCount;
            bitmapPointers = new uint[count];
            bitmapPointerValid = new bool[count];
            bitmapData = new byte[count][];
            bitmapExtensionBlocks = new List<uint>();
            bitmapNeedsRebuild = false;

            var root = await ReadBlock(volume.RootBlock);

            if (GetUInt32(root, OffsetBitmapFlag) != Constants.BM_VALID)
            {
                AddIssue(IssueType.NotValidated, volume.RootBlock, null,
                    "Volume is not validated, bitmap is rebuilt");
                bitmapNeedsRebuild = true;
            }

            for (var i = 0; i < Math.Min(count, Constants.BM_SIZE); i++)
            {
                bitmapPointers[i] = GetUInt32(root, OffsetBitmapPages + i * 4);
            }

            /* read bitmap extension blocks */
            var index = Constants.BM_SIZE;
            var extension = GetUInt32(root, OffsetBitmapExtension);
            while (index < count)
            {
                if (!InRange(extension) || used[(int)extension])
                {
                    AddIssue(IssueType.BitmapPointerInvalid, volume.RootBlock, null,
                        extension == 0
                            ? $"Bitmap extension block is missing for bitmap block {index}, bitmap extension blocks are rebuilt"
                            : $"Bitmap extension block {extension} for bitmap block {index} is invalid, bitmap extension blocks are rebuilt");
                    bitmapNeedsRebuild = true;
                    break;
                }

                used[(int)extension] = true;
                bitmapExtensionBlocks.Add(extension);
                var data = await ReadBlock(extension);
                for (var i = 0; i < BitmapPointersPerExtensionBlock && index < count; i++)
                {
                    bitmapPointers[index++] = GetUInt32(data, i * 4);
                }

                extension = GetUInt32(data, volume.BlockSize - 4);
            }

            /* check bitmap block pointers */
            var invalidPointers = new List<int>();
            var checksumErrors = new List<uint>();
            for (var i = 0; i < count; i++)
            {
                var pointer = bitmapPointers[i];
                if (!InRange(pointer) || used[(int)pointer])
                {
                    invalidPointers.Add(i);
                    continue;
                }

                used[(int)pointer] = true;
                bitmapPointerValid[i] = true;
                var data = await ReadBlock(pointer);
                if (!ChecksumValid(data, 0))
                {
                    /* bitmap block is rewritten, but content isn't used for compare */
                    checksumErrors.Add(pointer);
                    continue;
                }

                bitmapData[i] = data;
            }

            if (invalidPointers.Count > 0)
            {
                AddIssue(IssueType.BitmapPointerInvalid, volume.RootBlock, null,
                    $"{invalidPointers.Count} of {count} bitmap block pointers are invalid (bitmap blocks {string.Join(", ", invalidPointers.Take(MaxListedBlocks))}{(invalidPointers.Count > MaxListedBlocks ? ", ..." : string.Empty)}), bitmap blocks are relocated");
                bitmapNeedsRebuild = true;
            }

            if (checksumErrors.Count > 0)
            {
                AddIssue(IssueType.BitmapChecksum, checksumErrors[0], null,
                    $"{checksumErrors.Count} bitmap blocks have checksum errors (blocks {string.Join(", ", FormatBlocks(checksumErrors))}{(checksumErrors.Count > MaxListedBlocks ? ", ..." : string.Empty)}), bitmap is rebuilt");
                bitmapNeedsRebuild = true;
            }
        }

        /// <summary>
        /// Compare bitmap with blocks used and rebuild bitmap in repair mode.
        /// </summary>
        private async Task CheckBitmapContent()
        {
            var usedMarkedFree = new List<uint>();
            var freeMarkedUsed = new List<uint>();
            var usedMarkedFreeCount = 0;
            var freeMarkedUsedCount = 0;

            for (var block = volume.Reserved; block < volume.Blocks; block++)
            {
                var index = block - volume.Reserved;
                var data = bitmapData[index / BitmapBitsPerBlock];
                if (data == null)
                {
                    continue;
                }

                var bitIndex = (int)(index % BitmapBitsPerBlock);
                var free = (GetUInt32(data, 4 + bitIndex / 32 * 4) & (1U << (bitIndex % 32))) != 0;
                var isUsed = used[(int)block];

                if (isUsed && free)
                {
                    if (usedMarkedFree.Count < MaxListedBlocks) usedMarkedFree.Add(block);
                    usedMarkedFreeCount++;
                }
                else if (!isUsed && !free)
                {
                    if (freeMarkedUsed.Count < MaxListedBlocks) freeMarkedUsed.Add(block);
                    freeMarkedUsedCount++;
                }
            }

            if (usedMarkedFreeCount > 0)
            {
                AddIssue(IssueType.BlocksUsedMarkedFree, usedMarkedFree[0], null,
                    $"{usedMarkedFreeCount} blocks are used, but marked free in bitmap (blocks {string.Join(", ", usedMarkedFree)}{(usedMarkedFreeCount > MaxListedBlocks ? ", ..." : string.Empty)})");
                bitmapNeedsRebuild = true;
            }

            if (freeMarkedUsedCount > 0)
            {
                AddIssue(IssueType.BlocksFreeMarkedUsed, freeMarkedUsed[0], null,
                    $"{freeMarkedUsedCount} blocks are not used, but marked used in bitmap (blocks {string.Join(", ", freeMarkedUsed)}{(freeMarkedUsedCount > MaxListedBlocks ? ", ..." : string.Empty)})",
                    true);
                bitmapNeedsRebuild = true;
            }

            if (Repair && bitmapNeedsRebuild)
            {
                await RebuildBitmap();
            }

            var usedBlocks = (uint)CountUsedBlocks();
            stats.UsedBlocks = usedBlocks;
            stats.FreeBlocks = volume.Blocks - volume.Reserved - usedBlocks;
        }

        /// <summary>
        /// Rebuild bitmap from blocks used. Invalid bitmap block pointers and bitmap extension blocks are
        /// allocated, then bitmap blocks, bitmap extension blocks and root block are written.
        /// </summary>
        private async Task RebuildBitmap()
        {
            var count = BitmapBlockCount;

            /* allocate bitmap blocks for invalid pointers */
            for (var i = 0; i < count; i++)
            {
                if (bitmapPointerValid[i])
                {
                    continue;
                }

                var allocated = AllocateBlock();
                if (allocated == 0)
                {
                    AddIssue(IssueType.DiskFull, volume.RootBlock, null, "No free blocks to relocate bitmap blocks",
                        fixable: false);
                    return;
                }

                bitmapPointers[i] = allocated;
                bitmapPointerValid[i] = true;
            }

            /* allocate bitmap extension blocks */
            var extensionCount = count > Constants.BM_SIZE
                ? (count - Constants.BM_SIZE + BitmapPointersPerExtensionBlock - 1) / BitmapPointersPerExtensionBlock
                : 0;
            while (bitmapExtensionBlocks.Count < extensionCount)
            {
                var allocated = AllocateBlock();
                if (allocated == 0)
                {
                    AddIssue(IssueType.DiskFull, volume.RootBlock, null,
                        "No free blocks to allocate bitmap extension blocks", fixable: false);
                    return;
                }

                bitmapExtensionBlocks.Add(allocated);
            }

            foreach (var block in bitmapExtensionBlocks.Skip(extensionCount))
            {
                used[(int)block] = false;
            }

            bitmapExtensionBlocks = bitmapExtensionBlocks.Take(extensionCount).ToList();

            /* write bitmap blocks, bit set means free */
            var bitsInBitmap = volume.Blocks - volume.Reserved;
            for (var i = 0; i < count; i++)
            {
                var data = new byte[volume.BlockSize];
                var start = (long)i * BitmapBitsPerBlock;
                for (var bit = 0; bit < BitmapBitsPerBlock && start + bit < bitsInBitmap; bit++)
                {
                    var block = volume.Reserved + start + bit;
                    if (used[(int)block])
                    {
                        continue;
                    }

                    var offset = 4 + bit / 32 * 4;
                    SetUInt32(data, offset, GetUInt32(data, offset) | (1U << (bit % 32)));
                }

                ChangeBlock(bitmapPointers[i], data, 0);
            }

            /* write bitmap extension blocks */
            for (var e = 0; e < bitmapExtensionBlocks.Count; e++)
            {
                var data = new byte[volume.BlockSize];
                for (var i = 0; i < BitmapPointersPerExtensionBlock; i++)
                {
                    var index = Constants.BM_SIZE + e * BitmapPointersPerExtensionBlock + i;
                    if (index >= count)
                    {
                        break;
                    }

                    SetUInt32(data, i * 4, bitmapPointers[index]);
                }

                SetUInt32(data, volume.BlockSize - 4,
                    e < bitmapExtensionBlocks.Count - 1 ? bitmapExtensionBlocks[e + 1] : 0);
                ChangeBlock(bitmapExtensionBlocks[e], data, -1);
            }

            /* update root block */
            var root = await ReadBlock(volume.RootBlock);
            for (var i = 0; i < Constants.BM_SIZE; i++)
            {
                SetUInt32(root, OffsetBitmapPages + i * 4, i < count ? bitmapPointers[i] : 0);
            }

            SetUInt32(root, OffsetBitmapExtension, bitmapExtensionBlocks.Count > 0 ? bitmapExtensionBlocks[0] : 0);
            SetUInt32(root, OffsetBitmapFlag, Constants.BM_VALID);
            ChangeBlock(volume.RootBlock, root);

            ShowMsg($"Bitmap rebuilt with {count} bitmap blocks and {bitmapExtensionBlocks.Count} bitmap extension blocks");
        }

        /// <summary>
        /// Allocate free block, searching from root block to end of volume and then from start of volume. Returns
        /// 0, if no blocks are free.
        /// </summary>
        private uint AllocateBlock()
        {
            for (var block = volume.RootBlock + 1; block < volume.Blocks; block++)
            {
                if (!used[(int)block])
                {
                    used[(int)block] = true;
                    return block;
                }
            }

            for (var block = volume.Reserved; block <= volume.RootBlock && block < volume.Blocks; block++)
            {
                if (!used[(int)block])
                {
                    used[(int)block] = true;
                    return block;
                }
            }

            return 0;
        }
    }
}
