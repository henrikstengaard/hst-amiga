namespace Hst.Amiga.Tests.Pfs3Tests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems.Pfs3.Doctor;
using Xunit;

public class GivenPfs3Doctor : Pfs3TestBase
{
    [Fact(Skip = "WIP")]
    public async Task Fix()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        var volume = Pfs3Doctor.OpenVolume(Stream, partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.LowCyl,
            partitionBlock.HighCyl, partitionBlock.SizeBlock);

        await Pfs3Doctor.Scan(volume, true);
    }
}