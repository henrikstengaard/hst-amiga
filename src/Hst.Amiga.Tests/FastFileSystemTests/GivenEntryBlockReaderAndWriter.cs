namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.IO;
    using System.Threading.Tasks;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenEntryBlockReaderAndWriter
    {
        [Fact]
        public async Task WhenReadAndBuildEntryBlockThenEntryBlockIsUnchanged()
        {
            var adfPath = Path.Combine("TestData", "FastFileSystems", "dos3.adf");

            // arrange - open adf path
            await using var adfStream = System.IO.File.OpenRead(adfPath);

            // act - seek root block 882 offset for floppy disk
            adfStream.Seek(882 * 512, SeekOrigin.Begin);

            // act - read entry block bytes
            var blockBytes = new byte[512];
            var bytesRead = await adfStream.ReadAsync(blockBytes, 0, blockBytes.Length);
            Assert.Equal(512, bytesRead);

            
            // act - read and build root block
            var expectedEntryBlock = EntryBlockReader.Parse(blockBytes);
            var entryBlockBytes = EntryBlockWriter.BuildBlock(expectedEntryBlock, 512);

            for (var i = 0; i < 512; i++)
            {
                if (blockBytes[i] != entryBlockBytes[i])
                {
                    
                }
            }
            
            // assert - root block and new root block bytes are equal
            Assert.Equal(blockBytes, entryBlockBytes);
        }
    }
}