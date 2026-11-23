namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vm.FileSystem;
using Vm.Libraries.Dos;
using Xunit;
using static GivenAmigaVmWithImages;

public class GivenIsoVolumeFileSystem
{
    [Fact]
    public async Task When_DetectingIso_Then_OnlyIsoImagesAreIso()
    {
        var iso = CreateIso("TESTCD");
        iso.Position = 10;

        Assert.True(ImageMounter.IsIso(iso));
        Assert.Equal(10, iso.Position);
        Assert.False(ImageMounter.IsIso(await CreateAdf("Workbench")));
        Assert.False(ImageMounter.IsIso(await CreateDisk()));
        Assert.False(ImageMounter.IsIso(new MemoryStream(new byte[100])));
    }

    [Fact]
    public async Task When_MountingIso_Then_CdVolumeWithVolumeLabelIsMounted()
    {
        var volumes = await ImageMounter.Mount(CreateIso("TESTCD"), true, cdDeviceName: "CD2");

        var volume = Assert.Single(volumes);
        using (volume)
        {
            Assert.Equal("CD2", volume.DeviceName);
            Assert.Equal("TESTCD", volume.VolumeName);
            Assert.Equal(ImageVolumeType.Cd, volume.Type);
            Assert.True(volume.FileSystem.IsReadOnly);
        }
    }

    [Fact]
    public void When_ListingAndReadingFiles_Then_NamesInfoAndContentAreReturned()
    {
        using var fileSystem = new IsoVolumeFileSystem(CreateIso("TESTCD"));
        var root = fileSystem.GetRoot("CD0");

        Assert.Equal("TESTCD", fileSystem.Name);
        Assert.Equal(new[] { "C", "S" }, fileSystem.ListNames(root).OrderBy(x => x).ToArray());

        // trailing dot of iso name without extension, e.g. "Hello.", is hidden
        var c = fileSystem.GetChild(root, "C");
        Assert.Equal(new[] { "Hello" }, fileSystem.ListNames(c));
        Assert.Equal(new[] { "C", "Hello" }, fileSystem.GetChild(c, "hello").Components);
        Assert.False(fileSystem.GetInfo(fileSystem.GetChild(c, "hello")).IsDirectory);

        // names are matched ignoring case
        var directory = fileSystem.GetChild(root, "s");
        var file = fileSystem.GetChild(directory, "FILE.TXT");
        Assert.Equal(new[] { "S", "file.txt" }, file.Components);
        Assert.True(fileSystem.GetInfo(directory).IsDirectory);

        var info = fileSystem.GetInfo(file);
        Assert.False(info.IsDirectory);
        Assert.Equal(4, info.Size);
        Assert.Null(fileSystem.GetInfo(fileSystem.GetChild(root, "missing")));

        using var stream = fileSystem.OpenFile(file, VolumeFileMode.OldFile);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        Assert.Equal("text", reader.ReadToEnd());
    }

    [Fact]
    public void When_Writing_Then_DiskWriteProtectedErrorIsThrown()
    {
        using var fileSystem = new IsoVolumeFileSystem(CreateIso("TESTCD"));
        var root = fileSystem.GetRoot("CD0");
        var file = fileSystem.GetChild(fileSystem.GetChild(root, "S"), "file.txt");
        var newFile = fileSystem.GetChild(root, "new.txt");

        Action[] writes =
        {
            () => fileSystem.OpenFile(newFile, VolumeFileMode.NewFile),
            () => fileSystem.OpenFile(file, VolumeFileMode.NewFile),
            () => fileSystem.CreateDirectory(newFile),
            () => fileSystem.Delete(file),
            () => fileSystem.Rename(file, newFile),
            () => fileSystem.SetProtection(file, 0),
            () => fileSystem.SetComment(file, "comment"),
            () => fileSystem.SetDate(file, DateTime.Now)
        };

        foreach (var write in writes)
        {
            Assert.Equal(DosErrors.DiskWriteProtected, Assert.Throws<VolumeException>(write).DosError);
        }
    }
}
