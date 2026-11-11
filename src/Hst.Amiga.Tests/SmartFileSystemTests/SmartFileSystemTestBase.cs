namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.SmartFileSystem;
using RigidDiskBlocks;
using Xunit;

public abstract class SmartFileSystemTestBase
{
    private const long Size1Mb = 1024 * 1024;

    protected const long DiskSize100Mb = Size1Mb * 100;

    protected static readonly byte[] SfsDosType = { 0x53, 0x46, 0x53, 0x0 };

    protected static readonly byte[] DummySmartFileSystemBytes = Encoding.ASCII.GetBytes(
        "$VER: SmartFilesystem 1.84 (01/01/22)   ");

    protected static async Task<BlockMemoryStream> CreateSmartFileSystemFormattedDisk(long diskSize = DiskSize100Mb,
        int fileSystemBlockSize = 512, bool caseSensitive = false, bool recycled = true)
    {
        var stream = new BlockMemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);

        rigidDiskBlock.AddFileSystem(SfsDosType, DummySmartFileSystemBytes)
            .AddPartition("DH0", bootable: true, fileSystemBlockSize: fileSystemBlockSize);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench", caseSensitive, recycled);

        return stream;
    }

    protected static async Task<PartitionBlock> ReadPartitionBlock(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        return rigidDiskBlock.PartitionBlocks.First();
    }

    protected static async Task<SmartFileSystemVolume> MountVolume(Stream stream)
    {
        return await SmartFileSystemVolume.Mount(stream, await ReadPartitionBlock(stream));
    }

    protected static async Task AssertVolumeIsValid(Stream stream)
    {
        var partitionBlock = await ReadPartitionBlock(stream);
        var volume = await SmartFileSystemHelper.Mount(stream, partitionBlock);
        var errors = await SmartFileSystemVerifier.Verify(volume, partitionBlock.PreAlloc);
        Assert.Empty(errors);
    }

    protected static byte[] CreateTestData(int length, int seed = 0)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)((i + seed) % 251);
        }

        return data;
    }

    protected static async Task WriteFile(SmartFileSystemVolume volume, string name, byte[] data)
    {
        await using var stream = await volume.OpenFile(name, FileSystems.FileMode.Write, true);
        await stream.WriteAsync(data, 0, data.Length);
    }

    protected static async Task<byte[]> ReadFile(SmartFileSystemVolume volume, string name)
    {
        await using var stream = await volume.OpenFile(name, FileSystems.FileMode.Read);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
