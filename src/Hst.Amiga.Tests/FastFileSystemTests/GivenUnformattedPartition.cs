namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using RigidDiskBlocks;
using Xunit;

public class GivenUnformattedPartition : FastFileSystemTestBase
{
    [Fact]
    public async Task WhenMountFastFileSystemThenExceptionIsThrown()
    {
        // arrange - create unformatted disk
        var stream = new BlockMemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create(DiskSize100Mb.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);

        rigidDiskBlock.AddFileSystem(Dos3DosType, DummyFastFileSystemBytes)
            .AddPartition("DH0", bootable: true, fileSystemBlockSize: 512);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        // assert - mount fast file system volume throws exception invalid dos type
        await Assert.ThrowsAsync<IOException>(async () => await MountVolume(stream));
    }
}