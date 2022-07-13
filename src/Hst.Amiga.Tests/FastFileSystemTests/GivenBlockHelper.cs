namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;
    using FileSystems.FastFileSystem;
    using Xunit;

    public class GivenBlockHelper
    {
        [Fact]
        public void WhenCreateBitmapBlocksForDoubleDensityFloppyDiskThenMapEntriesHaveBlocksSetFree()
        {
            // arrange - calculate blocks for double density disk
            var cylinders = FloppyDiskConstants.DoubleDensity.HighCyl - FloppyDiskConstants.DoubleDensity.LowCyl + 1;
            var blocks = cylinders * FloppyDiskConstants.DoubleDensity.Heads *
                         FloppyDiskConstants.DoubleDensity.Sectors;

            // arrange - create expected free map entries with bit set to 1 (true/free) for double density floppy disk blocks
            var expectedFreeMapEntries = Enumerable.Range(0, blocks).Select(_ => true)
                .ChunkBy(Constants.BitmapsPerULong)
                .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray())).ToList();

            // act - create bitmap blocks
            var bitmapBlocks = BlockHelper.CreateBitmapBlocks(
                FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl,
                FloppyDiskConstants.DoubleDensity.Heads,
                FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.BlockSize).ToList();

            // assert - 1 bitmap block is created for a double density floppy disk
            Assert.Single(bitmapBlocks);

            // assert - bitmap block has map entries for double density floppy disk set to 1 (true/free)
            Assert.Equal(expectedFreeMapEntries, bitmapBlocks[0].Map);
        }

        [Fact]
        public void WhenCreateBitmapExtensionBlocksSmallerThanBlockSizeThenOnlyOneIsCreated()
        {
            const int blockSize = 512;
            const int nextPointerSize = 4;
            const int pointerSize = 4;
            var offsetsPerBitmapExtensionBlock = (blockSize - nextPointerSize) / pointerSize;
            var bitmapBlocksCount = offsetsPerBitmapExtensionBlock - 10;
            var bitmapBlocks = Enumerable.Range(1, bitmapBlocksCount)
                .Select(_ => new BitmapBlock()).ToList();

            var bitmapExtensionBlocks = BlockHelper
                .CreateBitmapExtensionBlocks(bitmapBlocks, blockSize)
                .ToList();

            Assert.Single(bitmapExtensionBlocks);

            var bitmapExtensionBlock1 = bitmapExtensionBlocks[0];
            Assert.Equal(bitmapBlocksCount, bitmapExtensionBlock1.BitmapBlocks.Count());
        }

        [Fact]
        public void WhenCreateBitmapExtensionBlocksLargerThanBlockSizeThenMultipleAreCreated()
        {
            const int blockSize = 512;
            const int nextPointerSize = 4;
            const int pointerSize = 4;
            var offsetsPerBitmapExtensionBlock = (blockSize - nextPointerSize) / pointerSize;
            var bitmapBlocksCount = offsetsPerBitmapExtensionBlock + 10;
            var bitmapBlocks = Enumerable.Range(1, bitmapBlocksCount)
                .Select(_ => new BitmapBlock()).ToList();

            var bitmapExtensionBlocks = BlockHelper
                .CreateBitmapExtensionBlocks(bitmapBlocks, blockSize)
                .ToList();

            Assert.NotEmpty(bitmapExtensionBlocks);
            Assert.Equal(2, bitmapExtensionBlocks.Count);

            var bitmapExtensionBlock1 = bitmapExtensionBlocks[0];
            Assert.Equal(offsetsPerBitmapExtensionBlock, bitmapExtensionBlock1.BitmapBlocks.Count());

            var bitmapExtensionBlock2 = bitmapExtensionBlocks[1];
            Assert.Equal(10, bitmapExtensionBlock2.BitmapBlocks.Count());
        }

        [Fact]
        public void WhenUpdateBitmapsForBitmapBlocksThenBitmapsAreChanged()
        {
            const uint rootBlockOffset = 880U;
            const uint bitmapBlockOffset = 881U;

            var bitmaps = new Dictionary<uint, bool>
            {
                { rootBlockOffset, false },
                { bitmapBlockOffset, false }
            };

            var bitmapBlocks = BlockHelper.CreateBitmapBlocks(
                FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl,
                FloppyDiskConstants.DoubleDensity.Heads,
                FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.BlockSize).ToList();


            // act - update bitmaps
            BlockHelper.UpdateBitmaps(bitmapBlocks, bitmaps, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
                FloppyDiskConstants.BlockSize);

            // assert - 1 bitmap block is created for a double density floppy disk
            Assert.Single(bitmapBlocks);

            // assert - root block offset 880 is set 0 (used)
            var mapEntry = Convert.ToInt32(Math.Floor((double)rootBlockOffset / Constants.BitmapsPerULong));
            var blockOffset = (int)(rootBlockOffset % Constants.BitmapsPerULong);
            Assert.Equal(0U, bitmapBlocks[0].Map[mapEntry] & (1 << blockOffset));

            // assert - bitmap block offset 881 is set 0 (used)
            mapEntry = Convert.ToInt32(Math.Floor((double)bitmapBlockOffset / Constants.BitmapsPerULong));
            blockOffset = (int)(bitmapBlockOffset % Constants.BitmapsPerULong);
            Assert.Equal(0U, bitmapBlocks[0].Map[mapEntry] & (1 << blockOffset));
        }
    }
}