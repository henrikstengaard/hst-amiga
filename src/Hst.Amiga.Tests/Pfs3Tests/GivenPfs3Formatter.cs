namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using RigidDiskBlocks;
using Xunit;

public class GivenPfs3Formatter
{
    private readonly byte[] pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    
    [Theory]
    //[InlineData("10mb.hdf", 1024 * 1024 * 10)]
    [InlineData("pfs3_100mb.hdf", 1024 * 1024 * 100)]
    public async Task WhenFormattingHardDiskFileThenPfs3BlocksAreCreated(string hdfPath, long diskSize)
    {
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 
        
        // arrange - create hdf file with 1 partition using DOS3 dos type 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(diskSize.ToUniversalSize())
            .AddFileSystem(pfs3DosType, await File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToFile(hdfPath);

        var partition = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await using var stream = File.Open(hdfPath, FileMode.Open, FileAccess.ReadWrite);
        await Pfs3Formatter.FormatPartition(stream, partition, "Workbench");
        
        // TODO - assert pfs3 blocks are created as expected
        
        // clean up
        stream.Close();
        File.Delete(hdfPath);
    }
}