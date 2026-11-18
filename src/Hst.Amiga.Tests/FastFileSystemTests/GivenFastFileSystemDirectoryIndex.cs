namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems.Exceptions;
using Xunit;

public class GivenFastFileSystemDirectoryIndex : FastFileSystemTestBase
{
    public static TheoryData<byte[]> DosTypes => new()
    {
        { new byte[] { 0x44, 0x4f, 0x53, 0x1 } },
        { Dos3DosType },
        { new byte[] { 0x44, 0x4f, 0x53, 0x5 } },
        { Dos7DosType }
    };

    private static async Task<FileSystems.FindEntryResult> FindEntry(FileSystems.FastFileSystem.FastFileSystemVolume volume,
        string directory, string name)
    {
        await volume.ChangeDirectory(directory);
        var result = await volume.FindEntry(name);
        await volume.ChangeDirectory("/");
        return result;
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenCreateMoveRenameAndDeleteEntriesThenEntriesAreFoundInDirectories(byte[] dosType)
    {
        // arrange - create formatted disk with directories and files
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType);
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateDirectory("Dir1");
            await volume.CreateDirectory("Dir2");
            for (var i = 0; i < 150; i++)
            {
                await volume.CreateFile($"File{i}");
            }

            // arrange - list and find entries, so directories are indexed
            Assert.Equal(152, (await volume.ListEntries()).Count());
            Assert.NotNull((await volume.FindEntry("File10")).Entry);

            // act - move files to directories, rename and delete files
            for (var i = 0; i < 150; i += 3)
            {
                await volume.Rename($"File{i}", $"Dir1/Moved{i}");
            }

            await volume.Rename("File1", "Renamed1");
            await volume.Rename("File2", "Dir2/File2");
            await volume.Delete("File4");

            // assert - entries are found in new locations and not found in old locations
            Assert.NotNull((await FindEntry(volume, "Dir1", "Moved3")).Entry);
            Assert.NotEmpty((await volume.FindEntry("File3")).PartsNotFound);
            Assert.Equal("Renamed1", (await volume.FindEntry("RENAMED1")).Entry.Name);
            Assert.NotNull((await FindEntry(volume, "Dir2", "File2")).Entry);
            Assert.NotEmpty((await volume.FindEntry("File4")).PartsNotFound);
            await Assert.ThrowsAsync<PathAlreadyExistsException>(() => volume.CreateFile("file5"));

            // act - create file with name of moved file and delete directory
            await volume.CreateFile("File3");
            await volume.ChangeDirectory("Dir2");
            await volume.Delete("File2");
            await volume.ChangeDirectory("/");
            await volume.Delete("Dir2");
            await volume.CreateDirectory("Dir2");

            // assert - created file and directory are found and new directory is empty
            Assert.NotNull((await volume.FindEntry("File3")).Entry);
            Assert.NotEmpty((await FindEntry(volume, "Dir2", "File2")).PartsNotFound);
            await volume.ChangeDirectory("Dir2");
            Assert.Empty(await volume.ListEntries());
        }

        // assert - entries listed after remount are equal
        await using (var volume = await MountVolume(stream))
        {
            var entries = (await volume.ListEntries()).Select(x => x.Name).ToList();
            Assert.Contains("Renamed1", entries);
            Assert.Contains("File3", entries);
            Assert.DoesNotContain("File4", entries);
            Assert.DoesNotContain("File6", entries);
            Assert.Equal(150 - 50 - 2 + 1 + 2, entries.Count);

            await volume.ChangeDirectory("Dir1");
            Assert.Equal(50, (await volume.ListEntries()).Count());
        }
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenCreateFileInDirectoryWithManyEntriesThenDirectoryIsIndexedWithBatchedReads(byte[] dosType)
    {
        // arrange - create disk with many files
        var stream = new CountingStream(await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType));
        const int files = 500;
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < files; i++)
            {
                await volume.CreateFile($"File{i}");
            }
        }

        await using (var volume = await MountVolume(stream))
        {
            // act - create file, which indexes directory
            stream.Reset();
            await volume.CreateFile("New");

            // assert - directory is indexed with less reads than entries
            Assert.True(stream.Reads < files / 4, $"{stream.Reads} reads");
            Assert.Equal(files + 1, (await volume.ListEntries()).Count());
        }
    }
}
