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
    protected static readonly Stream Stream = new BlockMemoryStream();
    
    public async Task CreatePfs3FormattedDisk()
    {
        RigidDiskBlock.AddFileSystem(Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(Stream, partitionBlock, "Workbench");
    }
}