namespace Hst.Amiga.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems;
using FileSystems.Exceptions;
using Xunit;
using static FileSystemVolumeTestHelper;

public class GivenFileSystemVolumeChangingDirectory
{
    public static IEnumerable<object[]> AllFileSystems => FileSystems.Select(x => new object[] { x });

    /// <summary>
    /// Mount formatted disk with directories "/Dir1/Dir2" and file "/Dir1/File1".
    /// </summary>
    private static async Task<IFileSystemVolume> MountVolumeWithDirectories(string fileSystem)
    {
        var stream = new MemoryStream();
        await CreateFormattedDisk(fileSystem, stream);
        var volume = await MountVolume(fileSystem, stream);
        await volume.CreateDirectory("Dir1");
        await volume.ChangeDirectory("Dir1");
        await volume.CreateDirectory("Dir2");
        await volume.CreateFile("File1", false, false);
        await volume.ChangeDirectory("/");
        return volume;
    }

    private static async Task<string[]> ListNames(IFileSystemVolume volume) =>
        (await volume.ListEntries()).Select(x => x.Name).OrderBy(x => x).ToArray();

    [Theory]
    [MemberData(nameof(AllFileSystems))]
    public async Task When_ChangingToAbsolutePathWithMultipleDirectories_Then_CurrentDirectoryIsLastDirectory(
        string fileSystem)
    {
        await using var volume = await MountVolumeWithDirectories(fileSystem);
        await volume.ChangeDirectory("Dir1");
        await volume.ChangeDirectory("Dir2");

        await volume.ChangeDirectory("/Dir1/Dir2");
        await volume.CreateFile("File2", false, false);

        Assert.Equal(new[] { "File2" }, await ListNames(volume));
        Assert.Equal("/Dir1/Dir2", await volume.GetCurrentPath());
    }

    [Theory]
    [MemberData(nameof(AllFileSystems))]
    public async Task When_ChangingToAbsolutePathFromSubDirectory_Then_PathIsResolvedFromRoot(string fileSystem)
    {
        await using var volume = await MountVolumeWithDirectories(fileSystem);
        await volume.ChangeDirectory("Dir1/Dir2");

        await volume.ChangeDirectory("/Dir1");

        Assert.Equal(new[] { "Dir2", "File1" }, await ListNames(volume));
    }

    [Theory]
    [MemberData(nameof(AllFileSystems))]
    public async Task When_ChangingToRelativePathWithMultipleDirectories_Then_CurrentDirectoryIsLastDirectory(
        string fileSystem)
    {
        await using var volume = await MountVolumeWithDirectories(fileSystem);

        await volume.ChangeDirectory("Dir1/Dir2");

        Assert.Empty(await ListNames(volume));
        Assert.Equal("/Dir1/Dir2", await volume.GetCurrentPath());
    }

    [Theory]
    [MemberData(nameof(AllFileSystems))]
    public async Task When_ChangingToRoot_Then_CurrentDirectoryIsRoot(string fileSystem)
    {
        await using var volume = await MountVolumeWithDirectories(fileSystem);
        await volume.ChangeDirectory("Dir1/Dir2");

        await volume.ChangeDirectory("/");

        Assert.Equal(new[] { "Dir1" }, await ListNames(volume));
    }

    [Theory]
    [MemberData(nameof(AllFileSystems))]
    public async Task When_ChangingToAbsolutePathNotFound_Then_ExceptionIsThrownAndCurrentDirectoryIsUnchanged(
        string fileSystem)
    {
        await using var volume = await MountVolumeWithDirectories(fileSystem);
        await volume.ChangeDirectory("Dir1");

        await Assert.ThrowsAsync<PathNotFoundException>(() => volume.ChangeDirectory("/Dir1/Missing"));
        await Assert.ThrowsAsync<PathNotFoundException>(() => volume.ChangeDirectory("/Dir1/File1"));

        Assert.Equal(new[] { "Dir2", "File1" }, await ListNames(volume));
    }
}
