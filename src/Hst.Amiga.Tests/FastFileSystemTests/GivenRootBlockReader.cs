namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.IO;
    using System.Threading.Tasks;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenRootBlockReader
    {
        [Fact]
        public async Task WhenReadRootBlockFromAdfThenRootBlockIsValid()
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", "dos3.adf");

            // arrange - calculate root block offset
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
                FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.FileSystemBlockSize);
            await using var adfStream = System.IO.File.OpenRead(adfPath);

            // arrange - seek root block offset
            adfStream.Seek(rootBlockOffset * FloppyDiskConstants.BlockSize, SeekOrigin.Begin);

            // act - read root block bytes
            var rootBlockBytes = new byte[FloppyDiskConstants.BlockSize];
            var bytesRead = await adfStream.ReadAsync(rootBlockBytes, 0, FloppyDiskConstants.BlockSize);
            var rootBlock = RootBlockParser.Parse(rootBlockBytes);

            // assert - bytes read and root block matches type and disk name
            var expectedHashtableSize =
                FastFileSystemHelper.CalculateHashtableSize(FloppyDiskConstants.FileSystemBlockSize);
            Assert.Equal(FloppyDiskConstants.BlockSize, bytesRead);
            Assert.Equal(2, rootBlock.Type);
            Assert.Equal(0U, rootBlock.HeaderKey);
            Assert.Equal(0U, rootBlock.HighSeq);
            Assert.Equal(expectedHashtableSize, rootBlock.HashTableSize);
            Assert.Equal("DOS3", rootBlock.DiskName);
        }
    }
}