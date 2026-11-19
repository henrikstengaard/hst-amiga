namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Threading.Tasks;
using Xunit;

public class GivenFastFileSystemMaxFileNameLength : FastFileSystemTestBase
{
    [Fact]
    public async Task WhenMountingDos3VolumeThenMaxFileNameLengthIs30()
    {
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos3DosType);
        await using var volume = await MountVolume(stream);

        Assert.Equal(30, volume.MaxFileNameLength);
    }

    [Fact]
    public async Task WhenMountingDos7VolumeThenMaxFileNameLengthIs107()
    {
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        await using var volume = await MountVolume(stream);

        Assert.Equal(107, volume.MaxFileNameLength);
    }
}
