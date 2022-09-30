namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using RigidDiskBlocks;
    using Xunit;

    public class GivenRigidDiskBlockReader : RigidDiskBlockTestBase
    {
        [Fact]
        public async Task WhenParseExistingBlockBytesThenRigidDiskBlockIsNotNull()
        {
            // arrange - read rigid disk block bytes
            var blockBytes =
                await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "rdsk_block.bin"));

            // act - parse rigid disk block bytes
            var rigidDiskBlock = await RigidDiskBlockReader.Parse(blockBytes);

            // assert - rigid disk block is not null
            Assert.NotNull(rigidDiskBlock);
        }

        [Fact]
        public async Task WhenReadRigidDiskBlockFromSector0ThenReadRigidDiskBlockIsValid()
        {
            // arrange - create rigid disk block
            var rigidDiskBlock = CreateRigidDiskBlock(1024);
            var blockBytes = await RigidDiskBlockWriter.BuildBlock(rigidDiskBlock);

            // arrange - write rigid disk block at sector 0 (0x0)
            var memoryStream = new MemoryStream();
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.WriteBytes(blockBytes);

            // act - read rigid disk block
            var actualRigidDiskBlock = await RigidDiskBlockReader.Read(memoryStream);

            // assert - rigid disk block is not null
            Assert.NotNull(actualRigidDiskBlock);
        }

        [Fact]
        public async Task WhenReadRigidDiskBlockFromSector1ThenReadRigidDiskBlockIsValid()
        {
            // arrange - create rigid disk block
            var rigidDiskBlock = CreateRigidDiskBlock(1024);
            var blockBytes = await RigidDiskBlockWriter.BuildBlock(rigidDiskBlock);

            // arrange - write rigid disk block at sector 1 (0x512)
            var memoryStream = new MemoryStream();
            memoryStream.Seek(512, SeekOrigin.Begin);
            await memoryStream.WriteBytes(blockBytes);

            // act - read rigid disk block
            var actualRigidDiskBlock = await RigidDiskBlockReader.Read(memoryStream);

            // assert - rigid disk block is not null
            Assert.NotNull(actualRigidDiskBlock);
        }

        [Fact]
        public async Task WhenAmigaHardFileThenRigidDiskBlockIsValid()
        {
            // arrange hard file
            var hardFile =
                new MemoryStream(await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks",
                    "rigid-disk-block.img")));

            // act read rigid disk block from hard file
            var rigidDiskBlock = await RigidDiskBlockReader.Read(hardFile);

            // assert rigid disk block
            Assert.NotNull(rigidDiskBlock);
            Assert.Equal("UAE", rigidDiskBlock.DiskVendor);
            Assert.Equal("HstWB 4GB", rigidDiskBlock.DiskProduct);
            Assert.Equal("0.4", rigidDiskBlock.DiskRevision);

            // assert number of partitions
            Assert.NotEmpty(rigidDiskBlock.PartitionBlocks);
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            Assert.Equal(2, partitionBlocks.Count);

            // assert partition 1
            var partition1 = partitionBlocks[0];
            Assert.Equal("DH0", partition1.DriveName);

            // assert partition 2
            var partition2 = partitionBlocks[1];
            Assert.Equal("DH1", partition2.DriveName);
        }
    }
}