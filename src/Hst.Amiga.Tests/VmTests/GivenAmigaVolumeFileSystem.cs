namespace Hst.Amiga.Tests.VmTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vm.FileSystem;
using Vm.Libraries.Dos;
using Xunit;

public class GivenAmigaVolumeFileSystem
{
    public static IEnumerable<object[]> FileSystems =>
        FileSystemVolumeTestHelper.FileSystems.Select(x => new object[] { x });

    private static async Task<(VolumeManager Volumes, AmigaVolumeFileSystem FileSystem)> Mount(string fileSystem,
        Stream stream, bool readOnly = false)
    {
        var volume = await FileSystemVolumeTestHelper.MountVolume(fileSystem, stream);
        var amigaFileSystem = new AmigaVolumeFileSystem(volume, readOnly);
        var volumes = new VolumeManager();
        volumes.AddVolume("Workbench", amigaFileSystem);
        return (volumes, amigaFileSystem);
    }

    private static async Task<MemoryStream> CreateDisk(string fileSystem)
    {
        var stream = new MemoryStream();
        await FileSystemVolumeTestHelper.CreateFormattedDisk(fileSystem, stream);
        return stream;
    }

    private static AmigaPath Resolve(VolumeManager volumes, string path) => volumes.Resolve(path, null);

    [Theory]
    [MemberData(nameof(FileSystems))]
    public async Task When_WritingAndReadingFiles_Then_FilesAreStoredInVolume(string fileSystem)
    {
        var stream = await CreateDisk(fileSystem);
        var (volumes, amigaFileSystem) = await Mount(fileSystem, stream);

        amigaFileSystem.CreateDirectory(Resolve(volumes, "Workbench:Dir"));
        volumes.WriteAllBytes(Resolve(volumes, "Workbench:Dir/File.txt"), Encoding.ASCII.GetBytes("hello"));

        // names are case insensitive and resolve to name in volume
        var path = Resolve(volumes, "workbench:dir/file.TXT");
        Assert.Equal("Workbench:Dir/File.txt", path.FullPath);
        Assert.True(volumes.IsDirectory(Resolve(volumes, "Workbench:Dir")));
        Assert.True(volumes.IsFile(path));
        Assert.Equal(5, volumes.GetInfo(path).Size);
        Assert.Equal("hello", Encoding.ASCII.GetString(volumes.ReadAllBytes(path)));
        Assert.Equal(new[] { "Dir" }, volumes.ListNames(Resolve(volumes, "Workbench:")));
        Assert.Equal(new[] { "File.txt" }, volumes.ListNames(Resolve(volumes, "Workbench:Dir")));
        amigaFileSystem.Dispose();
    }

    [Theory]
    [MemberData(nameof(FileSystems))]
    public async Task When_OpeningFileForReadWrite_Then_ChangesAreWrittenWhenClosed(string fileSystem)
    {
        var stream = await CreateDisk(fileSystem);
        var (volumes, amigaFileSystem) = await Mount(fileSystem, stream);
        var path = Resolve(volumes, "Workbench:file");
        volumes.WriteAllBytes(path, Encoding.ASCII.GetBytes("abcdef"));
        amigaFileSystem.SetComment(path, "comment");

        using (var file = amigaFileSystem.OpenFile(path, VolumeFileMode.OldFile))
        {
            Assert.True(file.CanWrite);
            file.Seek(2, SeekOrigin.Begin);
            file.Write(Encoding.ASCII.GetBytes("XY"), 0, 2);
            file.SetLength(5);
        }

        Assert.Equal("abXYe", Encoding.ASCII.GetString(volumes.ReadAllBytes(path)));
        Assert.Equal("comment", volumes.GetInfo(path).Comment);
        amigaFileSystem.Dispose();
    }

    [Theory]
    [MemberData(nameof(FileSystems))]
    public async Task When_ChangingEntries_Then_RenameDeleteProtectionAndCommentAreApplied(string fileSystem)
    {
        var stream = await CreateDisk(fileSystem);
        var (volumes, amigaFileSystem) = await Mount(fileSystem, stream);
        amigaFileSystem.CreateDirectory(Resolve(volumes, "Workbench:Dir"));
        volumes.WriteAllBytes(Resolve(volumes, "Workbench:file"), Encoding.ASCII.GetBytes("data"));

        amigaFileSystem.Rename(Resolve(volumes, "Workbench:file"), Resolve(volumes, "Workbench:Dir/moved"));
        amigaFileSystem.Rename(Resolve(volumes, "Workbench:Dir/moved"),
            new AmigaPath("Workbench", new[] { "Dir", "MOVED" }, null));
        var moved = Resolve(volumes, "Workbench:Dir/moved");
        Assert.Equal("Workbench:Dir/MOVED", moved.FullPath);
        Assert.False(volumes.Exists(Resolve(volumes, "Workbench:file")));

        amigaFileSystem.SetProtection(moved, 0x41); // script and not deletable
        amigaFileSystem.SetComment(moved, "a comment");
        var info = volumes.GetInfo(moved);
        Assert.Equal(0x41U, info.Protection);
        Assert.Equal("a comment", info.Comment);

        var exception = Assert.Throws<VolumeException>(() => amigaFileSystem.Delete(Resolve(volumes, "Workbench:Dir")));
        Assert.Equal(DosErrors.DirectoryNotEmpty, exception.DosError);

        amigaFileSystem.Delete(moved);
        amigaFileSystem.Delete(Resolve(volumes, "Workbench:Dir"));
        Assert.Empty(volumes.ListNames(Resolve(volumes, "Workbench:")));
        amigaFileSystem.Dispose();
    }

    [Theory]
    [MemberData(nameof(FileSystems))]
    public async Task When_VolumeIsReadOnly_Then_ChangesAreRejectedAsWriteProtected(string fileSystem)
    {
        var stream = await CreateDisk(fileSystem);
        var (writableVolumes, writable) = await Mount(fileSystem, stream);
        writableVolumes.WriteAllBytes(Resolve(writableVolumes, "Workbench:file"), Encoding.ASCII.GetBytes("data"));
        writable.Flush();
        writable.Dispose();

        var (volumes, amigaFileSystem) = await Mount(fileSystem, stream, true);
        var path = Resolve(volumes, "Workbench:file");

        using (var file = amigaFileSystem.OpenFile(path, VolumeFileMode.OldFile))
        {
            Assert.False(file.CanWrite);
        }

        Assert.Equal("data", Encoding.ASCII.GetString(volumes.ReadAllBytes(path)));
        Assert.Equal(DosErrors.DiskWriteProtected,
            Assert.Throws<VolumeException>(() => amigaFileSystem.OpenFile(path, VolumeFileMode.NewFile)).DosError);
        Assert.Equal(DosErrors.DiskWriteProtected,
            Assert.Throws<VolumeException>(() => amigaFileSystem.Delete(path)).DosError);
        Assert.Equal(DosErrors.DiskWriteProtected,
            Assert.Throws<VolumeException>(() =>
                amigaFileSystem.CreateDirectory(Resolve(volumes, "Workbench:Dir"))).DosError);
        amigaFileSystem.Dispose();
    }
}
