namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.Threading.Tasks;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenRootBlockWriter : FastFileSystemTestBase
    {
        [Fact]
        public async Task WhenBuildingRootBlockForDoubleDensityFloppyDiskThenBytesMatch()
        {
            // arrange - create root block for double density floppy disk
            const int fileSystemBlockSize = 512;
            const string diskName = "HstWB";
            var rootBlock = new RootBlock(fileSystemBlockSize)
            {
                Name = diskName,
                BitmapBlocksOffset = 881U,
                BitmapBlockOffsets = new[] { 881U },
                Date = Date,
                DiskAlterationDate = DateTime.MinValue,
                FileSystemCreationDate = Date,
                BitmapBlocks = new[] { new BitmapBlock(fileSystemBlockSize) } // dummy used for writing bitmap block
            };

            // act - build root block bytes
            var rootBlockBytes = RootBlockBuilder.Build(rootBlock, fileSystemBlockSize);

            // assert - root block bytes are equal to expected for double density floppy disk
            var expectedRootBlockBytes = await CreateExpectedRootBlockBytes();

            Assert.Equal(expectedRootBlockBytes, rootBlockBytes);
        }
    }
}