namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Blocks;

    public static class OffsetHelper
    {
        public static uint CalculateRootBlockOffset(uint lowCyl, uint highCyl, uint reserved, uint heads, uint sectors)
        {
            var cylinders = highCyl - lowCyl + 1;
            var highKey = cylinders * heads * sectors - reserved;
            var rootKey = (reserved + highKey) / 2;
            return rootKey;
        }

        public static void SetRootBlockOffsets(RootBlock rootBlock)
        {
            if (rootBlock.BitmapBlocksOffset == 0)
            {
                throw new ArgumentException("Bitmap block offset is not set", nameof(RootBlock.BitmapBlocksOffset));
            }

            var bitmapBlocks = rootBlock.BitmapBlocks.ToList();
            SetBitmapBlockOffsets(bitmapBlocks, rootBlock.BitmapBlocksOffset);

            rootBlock.BitmapBlockOffsets = bitmapBlocks.Select(x => x.Offset)
                .Concat(Enumerable.Range(1, Constants.MaxBitmapBlockPointersInRootBlock - bitmapBlocks.Count)
                    .Select(_ => 0U)).ToArray();

            rootBlock.BitmapExtensionBlocksOffset =
                rootBlock.BitmapExtensionBlocks.Any()
                    ? rootBlock.BitmapBlocksOffset + Constants.MaxBitmapBlockPointersInRootBlock
                    : 0;

            SetBitmapExtensionBlockOffsets(rootBlock.BitmapExtensionBlocks, rootBlock.BitmapExtensionBlocksOffset);
        }

        public static uint SetBitmapBlockOffsets(
            IEnumerable<BitmapBlock> bitmapBlocks, uint startOffset)
        {
            var offset = startOffset;
            foreach (var bitmapBlock in bitmapBlocks)
            {
                bitmapBlock.Offset = offset++;
            }

            return offset;
        }

        public static void SetBitmapExtensionBlockOffsets(IEnumerable<BitmapExtensionBlock> bitmapExtensionBlocks,
            uint startOffset)
        {
            var bitmapExtensionBlocksList = bitmapExtensionBlocks.ToList();

            var offset = startOffset;
            for (var i = 0; i < bitmapExtensionBlocksList.Count; i++)
            {
                var bitmapExtensionBlock = bitmapExtensionBlocksList[i];

                bitmapExtensionBlock.Offset = offset++;

                offset = SetBitmapBlockOffsets(bitmapExtensionBlock.BitmapBlocks, offset);

                bitmapExtensionBlock.BitmapBlockOffsets =
                    bitmapExtensionBlock.BitmapBlocks.Select(x => x.Offset).ToArray(); 
                bitmapExtensionBlock.NextBitmapExtensionBlockPointer =
                    i < bitmapExtensionBlocksList.Count - 1 ? offset : 0;
            }
        }
    }
}