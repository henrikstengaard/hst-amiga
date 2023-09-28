namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using RigidDiskBlocks;
    using Xunit;

    public class GivenRigidDiskBlock : RigidDiskBlockTestBase
    {
        [Fact]
        public void WhenCreatingFromChsThenDiskSizeMatches()
        {
            // arrange - cylinders, heads and sectors
            var cylinders = 800;
            var heads = 16;
            var sectors = 255;
            
            // act - create rigid disk block from cylinders, heads and sectors
            var rigidDiskBlock = RigidDiskBlock
                .Create(cylinders, heads, sectors);

            // assert - disk size is equal
            Assert.Equal(rigidDiskBlock.DiskSize, cylinders * heads * sectors * 512);

            // assert - cylinders, heads and sectors are equal
            Assert.Equal(rigidDiskBlock.Cylinders, (uint)cylinders);
            Assert.Equal(rigidDiskBlock.Heads, (uint)heads);
            Assert.Equal(rigidDiskBlock.Sectors, (uint)sectors);
        }

        [Fact]
        public void WhenAddPartitionLargerThanRigidDiskBlockThenPartitionSizeIsAdjustedToFit()
        {
            // arrange - create rigid disk block of 10mb
            var rigidDiskBlock = RigidDiskBlock.Create(10.MB());

            // act - add partition of 100mb
            var partitionBlock =
                PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("PFS3"), "DH0", 100.MB());

            // assert - partition low and high cylinder matches rigid disk block partitionable disk area
            Assert.Equal(rigidDiskBlock.LoCylinder, partitionBlock.LowCyl);
            Assert.Equal(rigidDiskBlock.HiCylinder, partitionBlock.HighCyl);
            
            // assert - calculated partition size matches
            var cylinders = rigidDiskBlock.HiCylinder - rigidDiskBlock.LoCylinder + 1;
            var partitionSize = (long)cylinders * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
            Assert.Equal(partitionSize, partitionBlock.PartitionSize);
        }

        [Fact]
        public void WhenAddLargePartitionThenPartitionSizeIsCalculatedPartitionSizeMatches()
        {
            // arrange - create rigid disk block of 10mb
            var rigidDiskBlock = RigidDiskBlock.Create(32.GB());

            // act - add partition of 100mb
            var partitionBlock =
                PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("PFS3"), "DH0", 32.GB());

            // assert - partition low and high cylinder matches rigid disk block partitionable disk area
            Assert.Equal(rigidDiskBlock.LoCylinder, partitionBlock.LowCyl);
            Assert.Equal(rigidDiskBlock.HiCylinder, partitionBlock.HighCyl);
            
            // assert - calculated partition size matches
            var cylinders = rigidDiskBlock.HiCylinder - rigidDiskBlock.LoCylinder + 1;
            var partitionSize = (long)cylinders * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
            Assert.Equal(partitionSize, partitionBlock.PartitionSize);
        }
        
        [Fact]
        public async Task WhenAddFileSystemThenFileSystemIsAdded()
        {
            var pfs3FileSystemBytes =
                await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio"));

            var rigidDiskBlock = RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(Pds3DosType, pfs3FileSystemBytes);

            Assert.Single(rigidDiskBlock.FileSystemHeaderBlocks);
        }
        
        [Fact]
        public async Task WhenAddFileSystemTwiceWithOverwriteThenOnlyOneFileSystemExists()
        {
            var pfs3FileSystemBytes =
                await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio"));

            var rigidDiskBlock = RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(Pds3DosType, pfs3FileSystemBytes)
                .AddFileSystem(Pds3DosType, pfs3FileSystemBytes, true);

            Assert.Single(rigidDiskBlock.FileSystemHeaderBlocks);
        }

        [Fact]
        public async Task WhenAddFileSystemTwiceThenExceptionIsThrown()
        {
            var pfs3FileSystemBytes =
                await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio"));

            Assert.Throws<ArgumentException>(() =>
            {
                RigidDiskBlock
                    .Create(10.MB().ToUniversalSize())
                    .AddFileSystem(Pds3DosType, pfs3FileSystemBytes)
                    .AddFileSystem(Pds3DosType, pfs3FileSystemBytes);
            });
        }
        
        [Fact]
        public void WhenCreatingLargeRigidDiskBlockThenDiskSizeIsCalculatedCorrectly()
        {
            var diskSize = 32.GB().ToSectorSize();
            
            // arrange - create rigid disk block of 32gb
            var rigidDiskBlock = RigidDiskBlock.Create(diskSize);

            // assert - heads and sectors are equal to default
            Assert.Equal(16U, rigidDiskBlock.Heads);
            Assert.Equal(63U, rigidDiskBlock.Sectors);

            var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
            var cylinders = diskSize / (cylinderSize * 512);
            
            // assert - cylinders, blocks per cylinder and disk size are equal
            Assert.Equal(cylinderSize, rigidDiskBlock.CylBlocks);
            Assert.Equal(cylinders, rigidDiskBlock.Cylinders);
            Assert.Equal(cylinderSize * cylinders * 512, rigidDiskBlock.DiskSize);
        }

        [Fact]
        public async Task WhenUpdateBlockPointersThenRigidDiskBlockIsEqual()
        {
            var pfs3AioBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio"));
            
            // arrange: create rigid disk block with 2 file systems and 2 partitions
            var rigidDiskBlock = RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(Dos3DosType, FastFileSystemBytes) 
                .AddFileSystem(Pds3DosType, pfs3AioBytes)
                .AddPartition("DH0", 3.MB(), bootable: true)
                .AddPartition("DH1");

            // act: update block pointers
            BlockHelper.UpdateBlockPointers(rigidDiskBlock);

            var currentBlock = rigidDiskBlock.RdbBlockLo;

            // assert: partition list is set to current block + 1
            Assert.Equal(currentBlock + 1, rigidDiskBlock.PartitionList);

            // assert: partition blocks
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            for (var i = 0; i < partitionBlocks.Count; i++)
            {
                var partitionBlock = partitionBlocks[i];
                currentBlock++;
                
                // assert: next partition block is set to current block + 1, if more than one partition block is present.
                // otherwise next partition block is equal to end of blocks 
                Assert.Equal(i < partitionBlocks.Count - 1 ? currentBlock + 1 : BlockIdentifiers.EndOfBlock, partitionBlock.NextPartitionBlock);
            }

            // assert: file system header list is set to current block +1
            Assert.Equal(currentBlock + 1, rigidDiskBlock.FileSysHdrList);
            
            // assert: file system header blocks
            var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();
            for (var i = 0; i < fileSystemHeaderBlocks.Count; i++)
            {
                var fileSystemHeaderBlock = fileSystemHeaderBlocks[i];
                var loadSegBlocks = fileSystemHeaderBlock.LoadSegBlocks.ToList();

                currentBlock++;
                
                // assert: next file system header block is set to current block + 1, if more than one file system header block is present.
                // otherwise next file system header block is equal to end of blocks 
                Assert.Equal(i < fileSystemHeaderBlocks.Count - 1 ? currentBlock + 1 + loadSegBlocks.Count : BlockIdentifiers.EndOfBlock, fileSystemHeaderBlock.NextFileSysHeaderBlock);
                
                // assert: load seg block
                Assert.Equal(currentBlock + 1, (uint)fileSystemHeaderBlock.SegListBlocks);
                
                // assert: load seg blocks
                for (var j = 0; j < loadSegBlocks.Count; j++)
                {
                    var loadSegBlock = loadSegBlocks[j];
                    currentBlock++;
                
                    // assert: next load seg block is set to current block + 1, if more than one load seg block is present.
                    // otherwise next load seg block is equal to end of blocks 
                    Assert.Equal(j < loadSegBlocks.Count - 1 ? (int)currentBlock + 1 : -1, loadSegBlock.NextLoadSegBlock);
                }
            }
        }
    }
}