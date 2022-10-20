namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using RigidDiskBlocks;

public abstract class Pfs3TestBase
{
    protected static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    protected static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
    protected readonly RigidDiskBlock RigidDiskBlock = RigidDiskBlock
        .Create(100.MB().ToUniversalSize());
    protected static readonly BlockMemoryStream Stream = new BlockMemoryStream();

    protected async Task CreatePfs3FormattedDisk()
    {
        Stream.SetLength(RigidDiskBlock.DiskSize);
        
        RigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(RigidDiskBlock, Stream);
        
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(Stream, partitionBlock, "Workbench");
    }

    protected async Task WriteStreamToFile(string path)
    {
        await using var fileStream = System.IO.File.OpenWrite(path);
        await Stream.WriteTo(fileStream);
    }
}