namespace Hst.Amiga.Tests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems;
using FileSystems.Exceptions;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;
using static FileSystemVolumeTestHelper;

public class GivenFileSystemVolumeMountedTwice
{
    public static TheoryData<string> FileSystemNames => new() { Ffs, Pfs3, Sfs };

    private static async Task<BlockMemoryStream> CreateFormattedDiskStream(string fileSystem)
    {
        var stream = new BlockMemoryStream();
        await CreateFormattedDisk(fileSystem, stream);
        return stream;
    }

    private static async Task<IFileSystemVolume> MountWithFactory(Stream stream, bool force = false)
    {
        var partitionBlock = (await RigidDiskBlockReader.Read(stream)).PartitionBlocks.First();
        return await FileSystemVolumeFactory.MountPartition(stream, partitionBlock, force);
    }

    [Theory]
    [MemberData(nameof(FileSystemNames))]
    public async Task WhenMountPartitionAlreadyMountedThenExceptionIsThrown(string fileSystem)
    {
        // arrange - create formatted disk and mount partition
        var stream = await CreateFormattedDiskStream(fileSystem);
        await using var volume = await MountVolume(fileSystem, stream);

        // act & assert - mount partition again throws exception
        await Assert.ThrowsAsync<PartitionAlreadyMountedException>(() => MountVolume(fileSystem, stream));
        await Assert.ThrowsAsync<PartitionAlreadyMountedException>(() => MountWithFactory(stream));

        // assert - only first volume is registered and first volume can still be used
        Assert.Equal(1, MountRegistry.Count(stream));
        await volume.CreateDirectory("Dir");
    }

    [Theory]
    [MemberData(nameof(FileSystemNames))]
    public async Task WhenMountPartitionAfterVolumeIsDisposedThenPartitionIsMounted(string fileSystem)
    {
        // arrange - create formatted disk, mount partition and dispose volume
        var stream = await CreateFormattedDiskStream(fileSystem);
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            await volume.CreateDirectory("Dir");
        }

        // act - mount partition again
        await using (var volume = await MountWithFactory(stream))
        {
            // assert - directory created by first volume exists
            Assert.Contains(await volume.ListEntries(), x => x.Name == "Dir");
        }

        // act - synchronous dispose also releases mounted partition
        var syncVolume = await MountVolume(fileSystem, stream);
        syncVolume.Dispose();

        // assert - no volumes are registered
        Assert.Equal(0, MountRegistry.Count(stream));
    }

    [Theory]
    [MemberData(nameof(FileSystemNames))]
    public async Task WhenMountPartitionAlreadyMountedWithForceThenPartitionIsMounted(string fileSystem)
    {
        // arrange - create formatted disk and mount partition
        var stream = await CreateFormattedDiskStream(fileSystem);
        await using var volume = await MountVolume(fileSystem, stream);

        // act - mount partition again with force
        await using (var forcedVolume = await MountWithFactory(stream, true))
        {
            // assert - both volumes are registered
            Assert.Equal(2, MountRegistry.Count(stream));

            // assert - mount partition again without force throws exception
            await Assert.ThrowsAsync<PartitionAlreadyMountedException>(() => MountWithFactory(stream));
        }

        // assert - forced volume is released on dispose
        Assert.Equal(1, MountRegistry.Count(stream));
    }

    [Theory]
    [MemberData(nameof(FileSystemNames))]
    public async Task WhenMountUnformattedPartitionFailsThenPartitionIsNotRegistered(string fileSystem)
    {
        // arrange - create disk with unformatted partition
        var stream = new BlockMemoryStream();
        await CreateDisk(fileSystem, stream);

        // act - mount unformatted partition twice fails
        for (var i = 0; i < 2; i++)
        {
            var exception = await Record.ExceptionAsync(() => MountWithFactory(stream));
            Assert.NotNull(exception);
            Assert.IsNotType<PartitionAlreadyMountedException>(exception);
        }

        // assert - no volumes are registered
        Assert.Equal(0, MountRegistry.Count(stream));
    }

    [Fact]
    public async Task WhenMountDifferentPartitionsInSameStreamThenPartitionsAreMounted()
    {
        // arrange - create disk with two partitions formatted with fast file system
        var stream = new BlockMemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create(10.MB().ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        rigidDiskBlock.AddFileSystem(new byte[] { 0x44, 0x4f, 0x53, 0x3 },
                Encoding.ASCII.GetBytes("$VER: FileSystem 0.1 (01/01/22) "))
            .AddPartition("DH0", 4.MB(), bootable: true)
            .AddPartition("DH1");
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks)
        {
            await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, partitionBlock.DriveName);
        }

        // act - mount both partitions
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        await using var volume1 = await FastFileSystemVolume.MountPartition(stream, partitionBlocks[0]);
        await using var volume2 = await FastFileSystemVolume.MountPartition(stream, partitionBlocks[1]);

        // assert - both volumes are registered and mounting first partition again throws exception
        Assert.Equal(2, MountRegistry.Count(stream));
        await Assert.ThrowsAsync<PartitionAlreadyMountedException>(() =>
            FastFileSystemVolume.MountPartition(stream, partitionBlocks[0]));
    }

    [Fact]
    public async Task WhenMountPartitionTwiceFromReadOnlyStreamThenPartitionIsMounted()
    {
        // arrange - create formatted disk and read only stream
        var stream = await CreateFormattedDiskStream(Ffs);
        var bytes = new MemoryStream();
        await stream.WriteTo(bytes);
        var readOnlyStream = new MemoryStream(bytes.ToArray(), false);

        // act - mount partition twice from read only stream
        await using var volume1 = await MountWithFactory(readOnlyStream);
        await using var volume2 = await MountWithFactory(readOnlyStream);

        // assert - volumes from read only streams are not registered
        Assert.Equal(0, MountRegistry.Count(readOnlyStream));
        Assert.Empty(await volume1.ListEntries());
        Assert.Empty(await volume2.ListEntries());
    }
}
