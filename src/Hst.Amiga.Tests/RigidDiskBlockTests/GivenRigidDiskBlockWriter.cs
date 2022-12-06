namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using RigidDiskBlocks;
    using Xunit;

    public class GivenRigidDiskBlockWriter : RigidDiskBlockTestBase
    {
        [Fact]
        public async Task WhenCreateAndWriteRigidDiskBlockThenRigidDiskBlockIsEqual()
        {
            var path = "amiga.hdf";

            var rigidDiskBlock = await RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(Pds3DosType,
                    await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio")))
                .AddPartition("DH0", 3.MB(), bootable: true)
                .AddPartition("DH1")
                .WriteToFile("amiga.hdf");

            await using var stream = File.OpenRead(path);
            var actualRigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            var rigidDiskBlockJson = System.Text.Json.JsonSerializer.Serialize(rigidDiskBlock);
            var actualRigidDiskBlockJson = System.Text.Json.JsonSerializer.Serialize(actualRigidDiskBlock);
            Assert.Equal(rigidDiskBlockJson, actualRigidDiskBlockJson);
        }
        
        [Fact]
        public async Task WhenWriteRigidDiskBlockThenSectorsContainExpectedBlocks()
        {
            var pfs3AioBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio"));
            
            var rigidDiskBlock = RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(Dos3DosType, FastFileSystemBytes) 
                .AddFileSystem(Pds3DosType, pfs3AioBytes)
                .AddPartition("DH0", 3.MB(), bootable: true)
                .AddPartition("DH1", bootable: true);

            // create memory stream and write rigid disk block
            using var stream = new MemoryStream();
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
                
            // seek rigid disk block sector
            stream.Position = rigidDiskBlock.RdbBlockLo * 512;

            // assert: rigid disk block is equal
            var blockBytes = await stream.ReadBytes(512);
            Assert.Equal(await RigidDiskBlockWriter.BuildBlock(rigidDiskBlock), blockBytes);

            // iterate partition blocks
            var nextPartitionBlock = rigidDiskBlock.PartitionList;
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            var partitionBlockIndex = 0;
            while(nextPartitionBlock != BlockIdentifiers.EndOfBlock)
            {
                var partitionBlock = partitionBlocks[partitionBlockIndex];
                
                // seek next partition block sector
                stream.Position = nextPartitionBlock * 512;
                
                // assert: partition block is equal
                blockBytes = await stream.ReadBytes(512);
                Assert.Equal(await PartitionBlockWriter.BuildBlock(partitionBlock), blockBytes);

                // set next partition block
                nextPartitionBlock = partitionBlock.NextPartitionBlock;
                partitionBlockIndex++;
            }

            // iterate file system header blocks
            var nextFileSystemHeaderBlock = rigidDiskBlock.FileSysHdrList;
            var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();
            var fileSystemHeaderBlockIndex = 0;
            while(nextFileSystemHeaderBlock != BlockIdentifiers.EndOfBlock)
            {
                var fileSystemHeaderBlock = fileSystemHeaderBlocks[fileSystemHeaderBlockIndex];
                
                // seek next file system header block sector
                stream.Position = nextFileSystemHeaderBlock * 512;
                
                // assert: file system header block is equal
                blockBytes = await stream.ReadBytes(512);
                Assert.Equal(await FileSystemHeaderBlockWriter.BuildBlock(fileSystemHeaderBlock), blockBytes);

                // set next file system header block
                nextFileSystemHeaderBlock = fileSystemHeaderBlock.NextFileSysHeaderBlock;
                fileSystemHeaderBlockIndex++;
                
                // iterate load seg blocks
                var nextLoadSegBlock = fileSystemHeaderBlock.SegListBlocks;
                var loadSegBlocks = fileSystemHeaderBlock.LoadSegBlocks.ToList();
                var loadSegBlockIndex = 0;
                while (nextLoadSegBlock != -1)
                {
                    var loadSegBlock = loadSegBlocks[loadSegBlockIndex];
                    
                    // seek next load seg block sector
                    stream.Position = nextLoadSegBlock * 512;
                
                    // assert: load seg block is equal
                    blockBytes = await stream.ReadBytes(512);
                    Assert.Equal(await LoadSegBlockWriter.BuildBlock(loadSegBlock), blockBytes);

                    // set next load seg block
                    nextLoadSegBlock = loadSegBlock.NextLoadSegBlock;
                    loadSegBlockIndex++;
                }
            }
        }
    }
}