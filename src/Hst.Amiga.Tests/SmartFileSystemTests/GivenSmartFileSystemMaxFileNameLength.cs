namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System.Threading.Tasks;
using Xunit;

public class GivenSmartFileSystemMaxFileNameLength : SmartFileSystemTestBase
{
    [Fact]
    public async Task WhenMountingVolumeThenMaxFileNameLengthIs100()
    {
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using var volume = await MountVolume(stream);

        Assert.Equal(100, volume.MaxFileNameLength);
    }
}
