namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.IO;
    using System.Threading.Tasks;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenRootBlockReaderAndWriter
    {
        [Fact]
        public async Task WhenReadAndWriteRootBlockThenRootBlockIsUnchanged()
        {
            var adfPath = Path.Combine("TestData", "FastFileSystems", "dos3.adf");

            // arrange - open adf path
            await using var adfStream = System.IO.File.OpenRead(adfPath);

            // act - seek root block 880 offset for floppy disk
            adfStream.Seek(880 * 512, SeekOrigin.Begin);

            // act - read root block bytes
            var rootBlockBytes = new byte[512];
            var bytesRead = await adfStream.ReadAsync(rootBlockBytes, 0, rootBlockBytes.Length);
            Assert.Equal(512, bytesRead);

            // act - parse and build root block
            var expectedRootBlock = RootBlockReader.Parse(rootBlockBytes);
            var newRootBlockBytes = RootBlockWriter.BuildBlock(expectedRootBlock, 512);

            // assert - root block and new root block bytes are equal
            Assert.Equal(rootBlockBytes, newRootBlockBytes);
        }
    }
}