namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenBitmapBlockWriter : FastFileSystemTestBase
    {
        [Fact]
        public async Task WhenBuildingBitmapBlockForDoubleDensityFloppyDiskThenBytesMatch()
        {
            // 2 blocks reserved for boot block
            var bootBlocks = 2;

            // arrange - create bitmap block for blank formatted adf
            var blocks = FloppyDiskConstants.DoubleDensity.Size / FloppyDiskConstants.BlockSize;
            var blockFree = new bool[blocks];
            for (var i = 0; i < blocks; i++)
            {
                blockFree[i] = true;
            }

            // arrange - calculate root block offset for double density floppy disk
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(
                FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl,
                FloppyDiskConstants.DoubleDensity.ReservedBlocks,
                FloppyDiskConstants.DoubleDensity.Heads,
                FloppyDiskConstants.DoubleDensity.Sectors);

            // arrange - create bitmap block for blank formatted adf
            var bitmapBlockOffset = rootBlockOffset + 1;
            blockFree[rootBlockOffset - bootBlocks] = false;
            blockFree[bitmapBlockOffset - bootBlocks] = false;

            var bitmapBlock = new BitmapBlock(FloppyDiskConstants.BlockSize)
            {
                Map = blockFree.ChunkBy(Constants.BitmapsPerULong)
                    .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray()))
                    .ToArray()
            };

            // act - build bitmap block bytes
            var bitmapBlockBytes = BitmapBlockBuilder.Build(bitmapBlock, FloppyDiskConstants.BlockSize);

            // assert - bitmap block bytes are equal to expected
            var expectedBitmapBlockBytes = await CreateExpectedBitmapBlockBytes();
            Assert.Equal(expectedBitmapBlockBytes, bitmapBlockBytes);
        }
    }
}