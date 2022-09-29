namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System;
    using System.IO;
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
            var partitionSize = cylinders * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
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
    }
}