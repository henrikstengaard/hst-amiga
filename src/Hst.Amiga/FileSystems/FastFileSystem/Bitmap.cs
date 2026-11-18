namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    internal static class Bitmap
    {
        public static async Task ReadBitmap(Volume vol, uint nBlock, RootBlock root)
        {
            int i;

            var mapSize = (uint)(nBlock / (vol.OffsetsPerBitmapBlock * 32));
            if (nBlock % (vol.OffsetsPerBitmapBlock * 32) != 0)
                mapSize++;
            vol.BitmapSize = mapSize;
            vol.BitmapTable = new BitmapBlock[mapSize];
            vol.BitmapBlocks = new uint[mapSize];
            vol.BitmapBlocksChg = new bool[mapSize];

            for (i = 0; i < mapSize; i++)
            {
                vol.BitmapBlocksChg[i] = false;
                vol.BitmapTable[i] = new BitmapBlock(vol.FileSystemBlockSize);
            }

            // prefetch bitmap blocks in root block, which are usually located after each other
            await Disk.PrefetchBlocks(vol, root.BitmapBlockOffsets.Take(Constants.BM_SIZE).Where(x => x != 0));

            var j = 0;
            i = 0;
            uint nSect;
            while (i < Constants.BM_SIZE && root.BitmapBlockOffsets[i] != 0)
            {
                vol.BitmapBlocks[j] = nSect = root.BitmapBlockOffsets[i];
                Disk.ThrowExceptionIfSectorNumberInvalid(vol, nSect);

                vol.BitmapTable[j] = await Disk.ReadBitmapBlock(vol, nSect);
                j++;
                i++;
            }

            nSect = root.BitmapExtensionBlocksOffset;
            while (nSect != 0)
            {
                var bmExt = await Disk.ReadBitmapExtensionBlock(vol, nSect);
                await Disk.PrefetchBlocks(vol, bmExt.BitmapBlockOffsets.Where(x => x != 0)
                    .Take((int)Math.Min(vol.OffsetsPerBitmapBlock, mapSize - j)));
                i = 0;
                while (i < vol.OffsetsPerBitmapBlock && j < mapSize)
                {
                    nSect = bmExt.BitmapBlockOffsets[i];
                    Disk.ThrowExceptionIfSectorNumberInvalid(vol, nSect);

                    vol.BitmapBlocks[j] = nSect;

                    vol.BitmapTable[j] = await Disk.ReadBitmapBlock(vol, nSect);
                    i++;
                    j++;
                }

                nSect = bmExt.NextBitmapExtensionBlockPointer;
            }

            vol.FreeBlocks = CountFreeBlocks(vol);
            vol.MaxFreeRunLength = -1;
        }

        /// <summary>
        /// Defer writing changed bitmap blocks until volume is flushed or disposed. Root block bitmap flags are
        /// set invalid on first deferred update, so bitmap is validated if volume isn't flushed (like AmigaDOS).
        /// </summary>
        public static async Task DeferBitmapUpdate(Volume vol)
        {
            if (vol.BitmapUpdatePending)
            {
                return;
            }

            await Disk.WriteRootBlockBitmapFlagsInvalid(vol);

            vol.BitmapUpdatePending = true;
        }

        /// <summary>
        /// Update bitmap by writing changed bitmap blocks and root block with bitmap flags valid to block cache,
        /// which are written to disk by flushing volume
        /// </summary>
        public static async Task UpdateBitmap(Volume vol)
        {
            var root = await Disk.ReadRootBlock(vol, vol.RootBlockOffset);

            for (var i = 0; i < vol.BitmapSize; i++)
                if (vol.BitmapBlocksChg[i])
                {
                    await Disk.WriteBitmapBlock(vol, vol.BitmapBlocks[i], vol.BitmapTable[i]);
                    vol.BitmapBlocksChg[i] = false;
                }

            root.BitmapFlags = Constants.BM_VALID;
            root.Date = DateTime.Now;

            await Disk.WriteRootBlock(vol, vol.RootBlockOffset, root);

            vol.BitmapUpdatePending = false;
        }

        /// <summary>
        /// Run length, which is large enough to allocate instead of smaller holes of free blocks
        /// </summary>
        public const int MinPreferredRunLength = 64;

        /// <summary>
        /// End of allocatable sectors (exclusive). Sectors from reserved to end can be allocated
        /// </summary>
        private static uint AllocatableEnd(Volume vol) => vol.LastBlock - vol.FirstBlock;

        public static uint AllocateBlock(Volume vol)
        {
            var (start, _) = AllocateRun(vol, 1);
            return start;
        }

        public static uint[] AllocateBlocks(Volume vol, uint nbSect)
        {
            var sectors = new uint[nbSect];
            for (var i = 0; i < nbSect; i++)
            {
                sectors[i] = AllocateBlock(vol);
            }

            return sectors;
        }

        /// <summary>
        /// Allocate a run of consecutive free blocks starting the search from next free block. Runs at least
        /// wanted blocks or min preferred run length long are preferred over smaller holes of free blocks to
        /// avoid fragmenting files. If no such run exists, the first free run found is allocated.
        /// </summary>
        /// <param name="vol">Volume to allocate blocks in</param>
        /// <param name="wanted">Number of blocks wanted</param>
        /// <returns>First sector and number of blocks allocated, between 1 and wanted</returns>
        /// <exception cref="DiskFullException">No free blocks</exception>
        public static (uint Start, int Count) AllocateRun(Volume vol, int wanted)
        {
            if (wanted < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(wanted), "Wanted blocks must be greater than zero");
            }

            var end = AllocatableEnd(vol);
            if (vol.Reserved >= end)
            {
                throw new DiskFullException("Disk full");
            }

            var cursor = vol.NextFreeBlock >= vol.Reserved && vol.NextFreeBlock < end
                ? vol.NextFreeBlock
                : vol.Reserved;
            var minRun = Math.Min(wanted, MinPreferredRunLength);

            // search for a run large enough, unless last search found no run large enough and no blocks were freed since
            uint start;
            int count;
            if (wanted == 1 || vol.MaxFreeRunLength < 0 || vol.MaxFreeRunLength >= minRun)
            {
                if (FindRun(vol, cursor, end, minRun, wanted, out start, out count, out var maxRunLength))
                {
                    MarkRunUsed(vol, start, count);
                    return (start, count);
                }

                vol.MaxFreeRunLength = maxRunLength;
            }

            // allocate first free run found
            if (!FindRun(vol, cursor, end, 1, wanted, out start, out count, out _))
            {
                throw new DiskFullException("Disk full");
            }

            MarkRunUsed(vol, start, count);
            return (start, count);
        }

        /// <summary>
        /// Free a run of consecutive blocks. If run ends at next free block, next free block is moved back to
        /// start of run, so freed blocks are allocated again next.
        /// </summary>
        public static void FreeRun(Volume vol, uint start, int count)
        {
            for (var i = 0; i < count; i++)
            {
                SetBlockFree(vol, start + (uint)i);
            }

            if (count > 0 && vol.NextFreeBlock == start + (uint)count)
            {
                vol.NextFreeBlock = start;
            }
        }

        private static void MarkRunUsed(Volume vol, uint start, int count)
        {
            for (var i = 0; i < count; i++)
            {
                SetBlockUsed(vol, start + (uint)i);
            }

            var next = start + (uint)count;
            vol.NextFreeBlock = next >= AllocatableEnd(vol) ? vol.Reserved : next;
        }

        /// <summary>
        /// Find run of free blocks at least min run blocks long searching from cursor to end and then from
        /// reserved to cursor.
        /// </summary>
        private static bool FindRun(Volume vol, uint cursor, uint end, int minRun, int wanted, out uint start,
            out int count, out int maxRunLength)
        {
            maxRunLength = 0;
            return FindRunInRange(vol, cursor, end, minRun, wanted, out start, out count, ref maxRunLength) ||
                   FindRunInRange(vol, vol.Reserved, cursor, minRun, wanted, out start, out count,
                       ref maxRunLength);
        }

        private static bool FindRunInRange(Volume vol, uint from, uint to, int minRun, int wanted, out uint start,
            out int count, ref int maxRunLength)
        {
            var sector = from;
            while (sector < to)
            {
                var free = FindFreeBlock(vol, sector, to);
                if (free == uint.MaxValue)
                {
                    break;
                }

                // count free blocks in run, stop when wanted is reached
                var runLength = CountFreeRun(vol, free, AllocatableEnd(vol), wanted);
                if (runLength >= minRun)
                {
                    start = free;
                    count = runLength;
                    return true;
                }

                maxRunLength = Math.Max(maxRunLength, runLength);
                sector = free + (uint)runLength;
            }

            start = 0;
            count = 0;
            return false;
        }

        private static readonly int[] DeBruijnTrailingZeros =
        {
            0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
            31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
        };

        /// <summary>
        /// Number of trailing zero bits in value, 32 if value is zero
        /// </summary>
        private static int TrailingZeroCount(uint value)
        {
#if NETCOREAPP3_0_OR_GREATER
            return System.Numerics.BitOperations.TrailingZeroCount(value);
#else
            return value == 0 ? 32 : DeBruijnTrailingZeros[((value & (~value + 1)) * 0x077CB531U) >> 27];
#endif
        }

        private static uint GetMapWord(Volume vol, uint wordIndex) =>
            vol.BitmapTable[wordIndex / (uint)vol.OffsetsPerBitmapBlock].Map[wordIndex % (uint)vol.OffsetsPerBitmapBlock];

        /// <summary>
        /// Find first free block from sector to end (exclusive) skipping 32 used blocks at a time
        /// </summary>
        /// <returns>Free sector or uint.MaxValue, if no free block found</returns>
        private static uint FindFreeBlock(Volume vol, uint sector, uint end)
        {
            if (sector >= end)
            {
                return uint.MaxValue;
            }

            var sectOfMap = sector - vol.Reserved;
            var wordIndex = sectOfMap / 32;
            var endWordIndex = (end - 1 - vol.Reserved) / 32;

            // ignore bits before sector in first word
            var word = GetMapWord(vol, wordIndex) & (uint.MaxValue << (int)(sectOfMap % 32));
            while (word == 0)
            {
                wordIndex++;
                if (wordIndex > endWordIndex)
                {
                    return uint.MaxValue;
                }

                word = GetMapWord(vol, wordIndex);
            }

            var free = vol.Reserved + wordIndex * 32 + (uint)TrailingZeroCount(word);
            return free < end ? free : uint.MaxValue;
        }

        /// <summary>
        /// Count consecutive free blocks from free sector, up to max blocks or end (exclusive)
        /// </summary>
        private static int CountFreeRun(Volume vol, uint sector, uint end, int max)
        {
            var limit = (int)Math.Min(max, end - sector);
            var count = 0;
            var sectOfMap = sector - vol.Reserved;

            while (count < limit)
            {
                var bit = (int)(sectOfMap % 32);
                var word = GetMapWord(vol, sectOfMap / 32) >> bit;

                // number of consecutive set (free) bits from bit
                var ones = TrailingZeroCount(~word);
                ones = Math.Min(ones, 32 - bit);

                count += ones;
                if (ones < 32 - bit)
                {
                    break;
                }

                sectOfMap += (uint)ones;
            }

            return Math.Min(count, limit);
        }

        private static readonly uint[] BitMask =
        {
            0x1, 0x2, 0x4, 0x8,
            0x10, 0x20, 0x40, 0x80,
            0x100, 0x200, 0x400, 0x800,
            0x1000, 0x2000, 0x4000, 0x8000,
            0x10000, 0x20000, 0x40000, 0x80000,
            0x100000, 0x200000, 0x400000, 0x800000,
            0x1000000, 0x2000000, 0x4000000, 0x8000000,
            0x10000000, 0x20000000, 0x40000000, 0x80000000
        };

        public static void SetBlockUsed(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            var oldValue = vol.BitmapTable[block].Map[indexInMap];
            if ((oldValue & BitMask[sectOfMap % 32]) != 0)
            {
                vol.FreeBlocks--;
            }

            vol.BitmapTable[block].Map[indexInMap] = oldValue & ~BitMask[sectOfMap % 32];
            vol.BitmapBlocksChg[block] = true;
        }

        /// <summary>
        /// Count free blocks in bitmap. Only bits of blocks in the volume are counted, as bits after the last block
        /// of the volume in the last bitmap block can be set (e.g. Workbench floppy disks).
        /// </summary>
        /// <param name="vol">Volume to count free blocks of.</param>
        /// <returns>Number of free blocks.</returns>
        public static long CountFreeBlocks(Volume vol)
        {
            long remainingBlocks = vol.Blocks > vol.Reserved ? vol.Blocks - vol.Reserved : 0;
            long freeBlocks = 0;

            foreach (var bitmapBlock in vol.BitmapTable)
            {
                for (var i = 0; i < vol.OffsetsPerBitmapBlock && i < bitmapBlock.Map.Length; i++)
                {
                    if (remainingBlocks <= 0)
                    {
                        return freeBlocks;
                    }

                    var mapEntry = bitmapBlock.Map[i];
                    if (remainingBlocks < 32)
                    {
                        mapEntry &= (1U << (int)remainingBlocks) - 1;
                    }

                    freeBlocks += CountBits(mapEntry);
                    remainingBlocks -= 32;
                }
            }

            return freeBlocks;
        }

        private static int CountBits(uint value)
        {
            value -= (value >> 1) & 0x55555555;
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }

        public static bool IsBlockFree(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            return (vol.BitmapTable[block].Map[indexInMap] & BitMask[sectOfMap % 32]) != 0;
        }

        public static void SetBlockFree(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            var oldValue = vol.BitmapTable[ block ].Map[ indexInMap ];
            if ((oldValue & BitMask[sectOfMap % 32]) == 0)
            {
                vol.FreeBlocks++;
            }

            vol.BitmapTable[ block ].Map[ indexInMap ] = oldValue | BitMask[ sectOfMap % 32 ];
            vol.BitmapBlocksChg[ block ] = true;

            // freed block can make a larger run of free blocks
            vol.MaxFreeRunLength = -1;
        }
    }
}