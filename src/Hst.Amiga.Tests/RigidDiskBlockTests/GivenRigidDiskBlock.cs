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