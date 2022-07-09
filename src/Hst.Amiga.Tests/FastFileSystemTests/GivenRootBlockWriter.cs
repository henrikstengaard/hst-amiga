namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.Threading.Tasks;
    using FileSystems.FastFileSystem;
    using Xunit;

    public class GivenRootBlockWriter : FastFileSystemTestBase
    {
        [Fact]
        public async Task WhenBuildingRootBlockForDoubleDensityFloppyDiskThenBytesMatch()
        {
            // arrange - create root block for double density floppy disk
            var blockSize = 512;
            var diskName = "HstWB";
            var rootBlock = new RootBlock
            {
                Name = diskName,
                BitmapBlocksOffset = 881,
                BitmapBlockOffsets = new[] { 881 },
                Date = Date,
                DiskAlterationDate = DateTime.MinValue,
                FileSystemCreationDate = Date,
                BitmapBlocks = new[] { new BitmapBlock() } // dummy used for writing bitmap block
            };

            // act - build root block bytes
            var rootBlockBytes = RootBlockWriter.BuildBlock(rootBlock, blockSize);

            // assert - root block bytes are equal to expected for double density floppy disk
            var expectedRootBlockBytes = await CreateExpectedRootBlockBytes();

            Assert.Equal(expectedRootBlockBytes, rootBlockBytes);
        }
    }
}