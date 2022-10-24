namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Amiga.Extensions;
    using Blocks;

    public static class BlockHelper
    {
        public static int CalculateOffsetsPerBitmapBlockCount(uint blockSize)
        {
            // calculate bitmaps per bitmap blocks count
            return Convert.ToInt32((blockSize - SizeOf.Long) / SizeOf.Long);
        }

        public static int CalculateBitmapsPerBitmapBlockCount(uint blockSize)
        {
            // calculate bitmaps per bitmap blocks count
            return Convert.ToInt32(CalculateOffsetsPerBitmapBlockCount(blockSize) *
                                   Constants.BitmapsPerULong);
        }

        public static int CalculateBitmapBlockOffsetsPerBitmapExtensionBlock(uint blockSize)
        {
            return Convert.ToInt32((blockSize - SizeOf.Long) / SizeOf.Long);            
        }

        public static IEnumerable<BitmapBlock> CreateBitmapBlocks(uint lowCyl, uint highCyl, uint heads,
            uint blocksPerTrack, uint blockSize)
        {
            // calculate blocks count
            var cylinders = highCyl - lowCyl + 1;
            var blocksCount = cylinders * heads * blocksPerTrack;

            var bitmapsPerBitmapBlockCount = CalculateBitmapsPerBitmapBlockCount(blockSize);

            for (var b = 0; b < blocksCount; b += bitmapsPerBitmapBlockCount)
            {
                var map = new List<uint>();

                var bitmapsInBitmapBlock = Math.Min(blocksCount - b, bitmapsPerBitmapBlockCount);
                for (var m = 0; m < bitmapsInBitmapBlock; m += Constants.BitmapsPerULong)
                {
                    if (m + Constants.BitmapsPerULong > bitmapsInBitmapBlock)
                    {
                        var bitmaps = 0U;
                        var lastBlocks = bitmapsInBitmapBlock - m;
                        for (var i = 0; i < lastBlocks; i++)
                        {
                            bitmaps |= 1U << i;
                        }
                        map.Add(bitmaps);
                        continue;
                    }
                    
                    map.Add(uint.MaxValue);
                }
                
                yield return new BitmapBlock
                {
                    Map = map.ToArray()
                };
            }
        }

        public static IEnumerable<BitmapExtensionBlock> CreateBitmapExtensionBlocks(
            IEnumerable<BitmapBlock> bitmapBlocks, uint blockSize)
        {
            // calculate pointers per bitmap extension block based on block size - next pointer
            var pointersPerBitmapExtensionBlock =
                Convert.ToInt32((blockSize - SizeOf.Long) / SizeOf.Long);

            // chunk bitmap blocks
            var bitmapBlockChunks = new List<BitmapExtensionBlock>();
            bitmapBlocks.ChunkBy(pointersPerBitmapExtensionBlock, blocks => bitmapBlockChunks.Add(
                new BitmapExtensionBlock
                {
                    BitmapBlocks = blocks.ToList(),
                }));

            return bitmapBlockChunks;
        }

        public static IEnumerable<BitmapExtensionBlock> CreateBitmapExtensionBlocks(
            IEnumerable<BitmapBlock> bitmapBlocks, uint blockSize, uint bitmapExtensionBlockOffset)
        {
            // calculate number of offsets stored in bitmap extension block
            var offsetsPerBitmapExtensionBlock = Convert.ToInt32((blockSize - 4) / 4);
            var currentBitmapExtensionBlockOffset = bitmapExtensionBlockOffset;

            var bitmapBlockChunks = new List<IEnumerable<BitmapBlock>>();
            bitmapBlocks.ChunkBy(offsetsPerBitmapExtensionBlock, blocks => bitmapBlockChunks.Add(blocks.ToList()));
            for (var i = 0; i < bitmapBlockChunks.Count; i++)
            {
                var bitmapBlockChunk = bitmapBlockChunks[i].ToList();

                var nextBitmapExtensionBlockOffset =
                    OffsetHelper.SetBitmapBlockOffsets(bitmapBlockChunk, currentBitmapExtensionBlockOffset + 1) + 1;

                yield return new BitmapExtensionBlock
                {
                    Offset = currentBitmapExtensionBlockOffset,
                    BitmapBlocks = bitmapBlockChunk,
                    NextBitmapExtensionBlockPointer =
                        i < bitmapBlockChunks.Count - 1 ? nextBitmapExtensionBlockOffset : 0
                };

                currentBitmapExtensionBlockOffset = nextBitmapExtensionBlockOffset;
            }
        }

        public static void UpdateBitmaps(IEnumerable<BitmapBlock> bitmapBlocks,
            IDictionary<uint, bool> blocksFreeMap, uint reserved, uint blockSize)
        {
            var bitmapBlocksList = bitmapBlocks.ToList();
            var bitmapsPerBitmapBlockCount = CalculateBitmapsPerBitmapBlockCount(blockSize);

            foreach (var entry in blocksFreeMap)
            {
                var bitmapBlockIndex = Convert.ToInt32((entry.Key - reserved) / bitmapsPerBitmapBlockCount);
                var blockIndex = (int)((entry.Key - reserved) % bitmapsPerBitmapBlockCount);

                MapBlockHelper.SetBlock(bitmapBlocksList[bitmapBlockIndex], blockIndex,
                    entry.Value ? BitmapBlock.BlockState.Free : BitmapBlock.BlockState.Used);
            }
        }

        public static async Task<IEnumerable<BitmapBlock>> ReadBitmapBlocks(Volume volume,
            IEnumerable<uint> bitmapBlockOffsets)
        {
            var bitmapBlocks = new List<BitmapBlock>();
            foreach (var bitmapBlockOffset in bitmapBlockOffsets)
            {
                var bitmapBlock = await Disk.ReadBitmapBlock(volume, bitmapBlockOffset);
                bitmapBlock.Offset = bitmapBlockOffset;

                bitmapBlocks.Add(bitmapBlock);
            }

            return bitmapBlocks;
        }

        public static async Task<IEnumerable<BitmapExtensionBlock>> ReadBitmapExtensionBlocks(Volume volume,
            uint bitmapExtensionBlocksOffset)
        {
            var bitmapExtensionBlocks = new List<BitmapExtensionBlock>();

            while (bitmapExtensionBlocksOffset != 0)
            {
                var bitmapExtensionBlock = await Disk.ReadBitmapExtensionBlock(volume, bitmapExtensionBlocksOffset);
                bitmapExtensionBlock.BitmapBlocks =
                    await ReadBitmapBlocks(volume, bitmapExtensionBlock.BitmapBlockOffsets);

                bitmapExtensionBlocks.Add(bitmapExtensionBlock);

                bitmapExtensionBlocksOffset = bitmapExtensionBlock.NextBitmapExtensionBlockPointer;
            }

            return bitmapExtensionBlocks;
        }
    }
}