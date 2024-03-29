﻿namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.Linq;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;
    using BlockHelper = FileSystems.FastFileSystem.BlockHelper;

    public class GivenOffsetHelper
    {
        [Fact]
        public void WhenCalculateRootBlockOffsetForDoubleDensityFloppyDiskThenOffsetMatch()
        {
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(
                FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl,
                FloppyDiskConstants.DoubleDensity.ReservedBlocks,
                FloppyDiskConstants.DoubleDensity.Heads,
                FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.FileSystemBlockSize);

            Assert.Equal(880U, rootBlockOffset);
        }

        [Fact]
        public void WhenCalculateRootBlockOffsetForPartitionOffsetMatch()
        {
            // arrange - partition geometry
            const int lowCyl = 2;
            const int highCyl = 17;
            const int reserved = 2;
            const int surfaces = 16;
            const int blocksPerTrack = 63;
            const uint fileSystemBlockSize = 512;
            
            // arrange - calculate expected root block offset
            var cylinders = highCyl - lowCyl + 1;
            var highKey = cylinders * surfaces * blocksPerTrack - reserved;
            var expectedRootBlockOffset = (uint)((reserved + highKey) / 2);
            
            // act - calculate root block offset for partition
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(
                lowCyl,
                highCyl,
                reserved,
                surfaces,
                blocksPerTrack,
                fileSystemBlockSize);

            // assert - root block offset matches expected root block offset
            Assert.Equal(expectedRootBlockOffset, rootBlockOffset);
        }
        
        [Fact]
        public void WhenSetOffsetsForOneBitmapExtensionBlockThenOffsetsAreSetSequential()
        {
            const int fileSystemBlockSize = 512;
            const int offsetSize = 4;
            const uint bitmapExtensionBlockOffset = 100U;
            var offsetsPerBitmapExtensionBlock = (fileSystemBlockSize - offsetSize) / offsetSize;
            var bitmapBlocksCount = offsetsPerBitmapExtensionBlock - 10;
            var bitmapBlocks = Enumerable.Range(1, bitmapBlocksCount)
                .Select(x => new BitmapBlock(fileSystemBlockSize)).ToList();
            
            var bitmapExtensionBlocks = BlockHelper
                .CreateBitmapExtensionBlocks(bitmapBlocks, fileSystemBlockSize)
                .ToList();

            OffsetHelper.SetBitmapExtensionBlockOffsets(bitmapExtensionBlocks, bitmapExtensionBlockOffset);
            
            Assert.Single(bitmapExtensionBlocks);

            var bitmapExtensionBlock1 = bitmapExtensionBlocks[0];
            Assert.Equal(100U, bitmapExtensionBlock1.Offset);
            Assert.Equal(bitmapBlocksCount, bitmapExtensionBlock1.BitmapBlocks.Count());
            Assert.Equal(0U, bitmapExtensionBlock1.NextBitmapExtensionBlockPointer);

            var bitmapBlocks1 = bitmapExtensionBlock1.BitmapBlocks.ToList();
            for (var i = 0; i < bitmapBlocks1.Count; i++)
            {
                Assert.Equal(100U + i + 1, bitmapBlocks1[i].Offset);
            }
        }
        
        [Fact]
        public void WhenSetOffsetsForMultipleBitmapExtensionBlocksThenOffsetsAreSetSequential()
        {
            const int fileSystemBlockSize = 512;
            const int nextPointerSize = 4;
            const int pointerSize = 4;
            const uint bitmapExtensionBlockOffset = 100U;
            
            var offsetsPerBitmapExtensionBlock = (fileSystemBlockSize - nextPointerSize) / pointerSize;
            var bitmapBlocksCount = offsetsPerBitmapExtensionBlock + 10;
            var bitmapBlocks = Enumerable.Range(1, bitmapBlocksCount)
                .Select(x => new BitmapBlock(fileSystemBlockSize)).ToList();
            
            var bitmapExtensionBlocks = BlockHelper
                .CreateBitmapExtensionBlocks(bitmapBlocks, fileSystemBlockSize, bitmapExtensionBlockOffset)
                .ToList();

            OffsetHelper.SetBitmapExtensionBlockOffsets(bitmapExtensionBlocks, bitmapExtensionBlockOffset);
            
            Assert.NotEmpty(bitmapExtensionBlocks);
            Assert.Equal(2, bitmapExtensionBlocks.Count);

            var bitmapExtensionBlock1 = bitmapExtensionBlocks[0];
            Assert.Equal(100U, bitmapExtensionBlock1.Offset);
            Assert.Equal(offsetsPerBitmapExtensionBlock, bitmapExtensionBlock1.BitmapBlocks.Count());
            Assert.Equal(100U + offsetsPerBitmapExtensionBlock + 1,
                bitmapExtensionBlock1.NextBitmapExtensionBlockPointer);

            var bitmapBlocks1 = bitmapExtensionBlock1.BitmapBlocks.ToList();
            for (var i = 0; i < bitmapBlocks1.Count; i++)
            {
                Assert.Equal(100U + i + 1, bitmapBlocks1[i].Offset);
            }

            var bitmapExtensionBlock2 = bitmapExtensionBlocks[1];
            Assert.Equal(100U + offsetsPerBitmapExtensionBlock + 1, bitmapExtensionBlock2.Offset);
            Assert.Equal(10, bitmapExtensionBlock2.BitmapBlocks.Count());
            Assert.Equal(0U, bitmapExtensionBlock2.NextBitmapExtensionBlockPointer);

            var bitmapBlocks2 = bitmapExtensionBlock2.BitmapBlocks.ToList();
            for (var i = 0; i < bitmapBlocks2.Count; i++)
            {
                Assert.Equal(100U + offsetsPerBitmapExtensionBlock + 2 + i, bitmapBlocks2[i].Offset);
            }
        }
    }
}