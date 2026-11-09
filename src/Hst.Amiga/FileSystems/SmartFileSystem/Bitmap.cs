namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Bitmap keeping track of free and used blocks, see fs/bitmap.c in smart file system source.
    /// Bitmap blocks are kept in memory and written through on changes.
    /// </summary>
    public static class Bitmap
    {
        public static int CalculateBitmapBlocks(uint totalBlocks, int blockSize)
        {
            var blocksInBitmap = (uint)BitmapBlock.BlocksInBitmap(blockSize);
            return (int)((totalBlocks + blocksInBitmap - 1) / blocksInBitmap);
        }

        public static async Task Load(Volume volume)
        {
            var bitmapBlocks = CalculateBitmapBlocks(volume.TotalBlocks, volume.BlockSize);
            volume.BlocksInBitmap = BitmapBlock.BlocksInBitmap(volume.BlockSize);
            volume.BitmapBlocks = new BitmapBlock[bitmapBlocks];
            for (var i = 0; i < bitmapBlocks; i++)
            {
                volume.BitmapBlocks[i] = await Disk.ReadBitmapBlock(volume, volume.RootBlock.BitmapBase + (uint)i);
            }

            volume.FreeBlocks = CountFreeBlocks(volume);
        }

        public static uint CountFreeBlocks(Volume volume)
        {
            uint free = 0;
            for (uint block = 0; block < volume.TotalBlocks; block++)
            {
                if (IsFree(volume, block))
                {
                    free++;
                }
            }

            return free;
        }

        public static bool IsFree(Volume volume, uint block)
        {
            if (block >= volume.TotalBlocks)
            {
                return false;
            }

            var bitmapBlock = volume.BitmapBlocks[block / volume.BlocksInBitmap];
            var bit = (int)(block % volume.BlocksInBitmap);
            return (bitmapBlock.Map[bit >> 5] & (0x80000000U >> (bit & 31))) != 0;
        }

        /// <summary>
        /// Mark blocks as used, markspace() in smart file system source.
        /// </summary>
        public static Task MarkUsed(Volume volume, uint start, uint count) => SetBits(volume, start, count, false);

        /// <summary>
        /// Mark blocks as free, freespace() in smart file system source.
        /// </summary>
        public static Task MarkFree(Volume volume, uint start, uint count) => SetBits(volume, start, count, true);

        private static async Task SetBits(Volume volume, uint start, uint count, bool free)
        {
            if (count == 0)
            {
                return;
            }

            if ((ulong)start + count > volume.TotalBlocks)
            {
                throw new IOException($"Blocks {start} to {start + count - 1} are outside partition");
            }

            var changedBitmapBlocks = new SortedSet<int>();
            for (var block = start; block < start + count; block++)
            {
                if (IsFree(volume, block) == free)
                {
                    throw new IOException($"Block {block} is already {(free ? "free" : "used")}");
                }

                var bitmapIndex = (int)(block / volume.BlocksInBitmap);
                var bitmapBlock = volume.BitmapBlocks[bitmapIndex];
                var bit = (int)(block % volume.BlocksInBitmap);
                var mask = 0x80000000U >> (bit & 31);
                if (free)
                {
                    bitmapBlock.Map[bit >> 5] |= mask;
                }
                else
                {
                    bitmapBlock.Map[bit >> 5] &= ~mask;
                }

                changedBitmapBlocks.Add(bitmapIndex);
            }

            foreach (var bitmapIndex in changedBitmapBlocks)
            {
                await Disk.WriteBitmapBlock(volume, volume.BitmapBlocks[bitmapIndex]);
            }

            volume.FreeBlocks = free ? volume.FreeBlocks + count : volume.FreeBlocks - count;
            var freeBlocks = volume.FreeBlocks;
            await Disk.UpdateRootInfo(volume, rootInfo => rootInfo.FreeBlocks = freeBlocks);
        }

        /// <summary>
        /// Find space of continuous free blocks between start and end block (exclusive).
        /// </summary>
        /// <returns>First block of free space or null, if not found.</returns>
        public static uint? FindSpace(Volume volume, uint blocksNeeded, uint startBlock, uint endBlock)
        {
            uint runStart = 0;
            uint runLength = 0;
            for (var block = startBlock; block < endBlock && block < volume.TotalBlocks; block++)
            {
                if (!IsFree(volume, block))
                {
                    runLength = 0;
                    continue;
                }

                if (runLength == 0)
                {
                    runStart = block;
                }

                runLength++;
                if (runLength >= blocksNeeded)
                {
                    return runStart;
                }
            }

            return null;
        }

        /// <summary>
        /// Find first run of free blocks starting search at start block and wrapping around to block 0.
        /// </summary>
        /// <returns>First block and number of free blocks up to max blocks in run, or null if no free blocks.</returns>
        public static (uint Start, uint Length)? FindRun(Volume volume, uint startBlock, uint maxBlocks)
        {
            if (startBlock >= volume.TotalBlocks)
            {
                startBlock = 0;
            }

            for (ulong i = 0; i < volume.TotalBlocks; i++)
            {
                var block = (uint)((startBlock + i) % volume.TotalBlocks);
                if (!IsFree(volume, block))
                {
                    continue;
                }

                uint length = 0;
                while (length < maxBlocks && block + length < volume.TotalBlocks && IsFree(volume, block + length))
                {
                    length++;
                }

                return (block, length);
            }

            return null;
        }
    }
}
