namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using FileSystems;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenEntryBlockReaderAndWriter
    {
        [Fact]
        public async Task WhenReadAndBuildEntryBlockThenEntryBlockIsUnchanged()
        {
            var adfPath = Path.Combine("TestData", "FastFileSystems", "dos3.adf");

            // arrange - open adf path
            await using var adfStream = File.OpenRead(adfPath);

            // act - seek root block 882 offset for floppy disk
            adfStream.Seek(882 * 512, SeekOrigin.Begin);

            // act - read entry block bytes
            var blockBytes = new byte[512];
            var bytesRead = await adfStream.ReadAsync(blockBytes, 0, blockBytes.Length);
            Assert.Equal(512, bytesRead);

            // hack - write hashtable size 72 at offset 0xc and calculate new checksum
            // why does amiga created adf have 0 as hashtable size, but it's hashtable contains entries?
            BigEndianConverter.ConvertUInt32ToBytes(72, blockBytes, 0xc);
            ChecksumHelper.UpdateChecksum(blockBytes, 20);
            
            // act - read and build root block
            var expectedEntryBlock = EntryBlockParser.Parse(blockBytes);
            var entryBlockBytes = EntryBlockBuilder.Build(expectedEntryBlock, 512);

            // assert - root block and new root block bytes are equal
            Assert.Equal(blockBytes, entryBlockBytes);
        }
    }
}