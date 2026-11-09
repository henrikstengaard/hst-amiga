namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems;
using FileSystems.Exceptions;
using Xunit;
using FileMode = FileSystems.FileMode;

public class GivenFormattedSmartFileSystemPartition : SmartFileSystemTestBase
{
    [Fact]
    public async Task WhenCreatingDirectoriesAndFilesThenEntriesAreListed()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            // act
            await volume.CreateDirectory("Dir1");
            await volume.CreateFile("File1.txt");
            await volume.ChangeDirectory("Dir1");
            await volume.CreateDirectory("Dir2");
            await volume.CreateFile("File2.txt");
        }

        // assert - entries are listed after remount
        await using (var volume = await MountVolume(stream))
        {
            var entries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal("Dir1", entries[0].Name);
            Assert.Equal(EntryType.Dir, entries[0].Type);
            Assert.Equal("File1.txt", entries[1].Name);
            Assert.Equal(EntryType.File, entries[1].Type);
            Assert.Equal(0, entries[1].Size);
            Assert.Equal(ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete,
                entries[1].ProtectionBits);

            await volume.ChangeDirectory("Dir1");
            Assert.Equal("/Dir1", await volume.GetCurrentPath());
            entries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();
            Assert.Equal(new[] { "Dir2", "File2.txt" }, entries.Select(x => x.Name));

            await volume.ChangeDirectory("/");
            Assert.Equal("/", await volume.GetCurrentPath());
            await volume.ChangeDirectory("Dir1/Dir2");
            Assert.Equal("/Dir1/Dir2", await volume.GetCurrentPath());
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenCreatingExistingEntryThenExceptionIsThrown()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateDirectory("Dir");
        await volume.CreateFile("File");

        // act & assert - names are case insensitive
        await Assert.ThrowsAsync<PathAlreadyExistsException>(() => volume.CreateDirectory("DIR"));
        await Assert.ThrowsAsync<PathAlreadyExistsException>(() => volume.CreateFile("file"));
        await Assert.ThrowsAsync<PathNotFoundException>(() => volume.ChangeDirectory("NotExisting"));
        await Assert.ThrowsAsync<PathNotFoundException>(() => volume.ChangeDirectory("File"));
    }

    [Fact]
    public async Task WhenFindingEntryThenEntryIsFound()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateFile("ReadMe.txt");

        // act
        var found = await volume.FindEntry("README.TXT");
        var notFound = await volume.FindEntry("Missing");

        // assert
        Assert.NotNull(found.Entry);
        Assert.Equal("ReadMe.txt", found.Entry.Name);
        Assert.Empty(found.PartsNotFound);
        Assert.Null(notFound.Entry);
        Assert.Equal(new[] { "Missing" }, notFound.PartsNotFound);
    }

    [Fact]
    public async Task WhenCaseSensitiveVolumeThenNamesWithDifferentCaseAreDifferentEntries()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk(caseSensitive: true);
        await using (var volume = await MountVolume(stream))
        {
            // act
            await volume.CreateFile("file");
            await volume.CreateFile("FILE");

            // assert
            Assert.Equal(2, (await volume.ListEntries()).Count());
            Assert.Null((await volume.FindEntry("File")).Entry);
        }

        await AssertVolumeIsValid(stream);
    }

    [Theory]
    [InlineData(512, 0)]
    [InlineData(512, 1)]
    [InlineData(512, 511)]
    [InlineData(512, 512)]
    [InlineData(512, 513)]
    [InlineData(512, 100000)]
    [InlineData(1024, 1)]
    [InlineData(1024, 100000)]
    [InlineData(4096, 1000000)]
    public async Task WhenWritingAndReadingFileThenDataIsEqual(int blockSize, int length)
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk(fileSystemBlockSize: blockSize);
        var data = CreateTestData(length);
        long freeBefore;

        // act - write file
        await using (var volume = await MountVolume(stream))
        {
            freeBefore = volume.Free;
            await WriteFile(volume, "file.bin", data);
        }

        // assert - read file after remount
        await using (var volume = await MountVolume(stream))
        {
            var entry = (await volume.FindEntry("file.bin")).Entry;
            Assert.Equal(length, entry.Size);
            Assert.Equal(data, await ReadFile(volume, "file.bin"));

            var blocks = (length + blockSize - 1) / blockSize;
            Assert.Equal(freeBefore - (long)blocks * blockSize, volume.Free);
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenWritingFileInSmallChunksThenDataIsEqual()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var data = CreateTestData(20000);

        // act - write data in chunks not aligned to block size
        await using (var volume = await MountVolume(stream))
        {
            await using var entryStream = await volume.OpenFile("file.bin", FileMode.Write);
            for (var offset = 0; offset < data.Length; offset += 77)
            {
                await entryStream.WriteAsync(data, offset, Math.Min(77, data.Length - offset));
            }
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(data, await ReadFile(volume, "file.bin"));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenOverwritingAndAppendingFileThenDataIsUpdated()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var data1 = CreateTestData(10000, 1);
        var data2 = CreateTestData(3000, 2);
        var data3 = CreateTestData(1500, 3);

        await using (var volume = await MountVolume(stream))
        {
            var freeBefore = volume.Free;
            await WriteFile(volume, "file.bin", data1);

            // act - overwrite with smaller data
            await WriteFile(volume, "file.bin", data2);
            Assert.Equal(data2, await ReadFile(volume, "file.bin"));
            Assert.Equal(freeBefore - 6 * 512, volume.Free);

            // act - append data
            await using (var entryStream = await volume.OpenFile("file.bin", FileMode.Append))
            {
                await entryStream.WriteAsync(data3, 0, data3.Length);
            }

            // act - write without overwrite fails
            await Assert.ThrowsAsync<PathAlreadyExistsException>(() =>
                volume.OpenFile("file.bin", FileMode.Write, false));
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(data2.Concat(data3).ToArray(), await ReadFile(volume, "file.bin"));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenSeekingAndWritingInFileThenDataIsUpdated()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var data = CreateTestData(5000);
        var patch = Enumerable.Repeat((byte)0xaa, 1000).ToArray();

        await using (var volume = await MountVolume(stream))
        {
            await WriteFile(volume, "file.bin", data);

            // act - overwrite part of file and write beyond end of file
            await using (var entryStream = await volume.OpenFile("file.bin", FileMode.Append))
            {
                entryStream.Seek(1000, SeekOrigin.Begin);
                await entryStream.WriteAsync(patch, 0, patch.Length);
                entryStream.Seek(6000, SeekOrigin.Begin);
                await entryStream.WriteAsync(patch, 0, 10);
            }
        }

        // assert
        var expected = new byte[6010];
        Array.Copy(data, expected, data.Length);
        Array.Copy(patch, 0, expected, 1000, patch.Length);
        Array.Copy(patch, 0, expected, 6000, 10);

        await using (var volume = await MountVolume(stream))
        {
            await using var entryStream = await volume.OpenFile("file.bin", FileMode.Read);
            Assert.Equal(6010, entryStream.Length);
            var buffer = new byte[100];
            entryStream.Seek(950, SeekOrigin.Begin);
            Assert.Equal(100, await entryStream.ReadAsync(buffer, 0, 100));
            Assert.Equal(expected.Skip(950).Take(100), buffer);
        }

        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(expected, await ReadFile(volume, "file.bin"));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenTruncatingFileThenBlocksAreFreed()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var data = CreateTestData(10000);

        await using (var volume = await MountVolume(stream))
        {
            var freeBefore = volume.Free;
            await WriteFile(volume, "file.bin", data);

            // act
            await using (var entryStream = await volume.OpenFile("file.bin", FileMode.Append))
            {
                entryStream.SetLength(1000);
            }

            // assert
            Assert.Equal(freeBefore - 2 * 512, volume.Free);
            Assert.Equal(data.Take(1000), await ReadFile(volume, "file.bin"));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenDeletingFilesAndDirectoriesThenBlocksAreFreed()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            var freeBefore = volume.Free;
            await volume.CreateDirectory("Dir");
            await volume.ChangeDirectory("Dir");
            await WriteFile(volume, "file1.bin", CreateTestData(50000));
            await WriteFile(volume, "file2.bin", CreateTestData(10));
            await volume.ChangeDirectory("/");

            // act & assert - delete non empty directory fails
            await Assert.ThrowsAsync<DirectoryNotEmptyException>(() => volume.Delete("Dir"));

            // act - delete files and directory
            await volume.Delete("Dir/file1.bin");
            await volume.Delete("Dir/file2.bin");
            await volume.Delete("Dir");

            // assert
            Assert.Empty(await volume.ListEntries());
            Assert.Equal(freeBefore, volume.Free);
            await Assert.ThrowsAsync<PathNotFoundException>(() => volume.Delete("Dir"));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenDeletingProtectedFileThenExceptionIsThrownUnlessIgnoringProtectionBits()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateFile("file");
        await volume.SetProtectionBits("file", ProtectionBits.Read);

        // act & assert
        await Assert.ThrowsAsync<FileSystemException>(() => volume.Delete("file"));
        await Assert.ThrowsAsync<FileSystemException>(() => volume.Delete(".recycled", true));
        await volume.Delete("file", true);
        Assert.Empty(await volume.ListEntries());
    }

    [Fact]
    public async Task WhenRenamingAndMovingEntriesThenEntriesAreMoved()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var data = CreateTestData(3000);
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateDirectory("Dir1");
            await volume.CreateDirectory("Dir2");
            await WriteFile(volume, "file.bin", data);
            await volume.ChangeDirectory("Dir1");
            await volume.CreateFile("sub.txt");
            await volume.ChangeDirectory("/");

            // act - rename file, move file and move directory
            await volume.Rename("file.bin", "renamed.bin");
            await volume.Rename("renamed.bin", "Dir2/moved.bin");
            await volume.Rename("Dir1", "Dir2/Dir1");
            await volume.Rename("Dir2/Dir1/sub.txt", "Dir2/Dir1/SUB.TXT");

            // act & assert - invalid renames
            await Assert.ThrowsAsync<PathNotFoundException>(() => volume.Rename("missing", "x"));
            await Assert.ThrowsAsync<PathAlreadyExistsException>(() => volume.Rename("Dir2", "Dir2"));
            await Assert.ThrowsAsync<PathNotFoundException>(() => volume.Rename("Dir2", "missing/x"));
            await Assert.ThrowsAsync<FileSystemException>(() => volume.Rename("Dir2", "Dir2/Dir1/Dir2"));
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(new[] { "Dir2" }, (await volume.ListEntries()).Select(x => x.Name));
            await volume.ChangeDirectory("Dir2");
            Assert.Equal(new[] { "Dir1", "moved.bin" }, (await volume.ListEntries()).Select(x => x.Name).OrderBy(x => x));
            Assert.Equal(data, await ReadFile(volume, "moved.bin"));
            await volume.ChangeDirectory("Dir1");
            Assert.Equal("/Dir2/Dir1", await volume.GetCurrentPath());
            Assert.Equal(new[] { "SUB.TXT" }, (await volume.ListEntries()).Select(x => x.Name));
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenSettingCommentProtectionBitsAndDateThenEntryIsUpdated()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        var date = new DateTime(2023, 4, 5, 6, 7, 8);
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateFile("file");

            // act
            await volume.SetComment("file", "This is a comment");
            await volume.SetProtectionBits("file", ProtectionBits.Archive | ProtectionBits.Read);
            await volume.SetDate("file", date);
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            var entry = (await volume.FindEntry("file")).Entry;
            Assert.Equal("This is a comment", entry.Comment);
            Assert.Equal(ProtectionBits.Archive | ProtectionBits.Read, entry.ProtectionBits);
            Assert.Equal(date, entry.Date);

            // act - file without read bit can't be read unless ignoring protection bits
            await volume.SetProtectionBits("file", ProtectionBits.Write);
            await Assert.ThrowsAsync<FileSystemException>(() => volume.OpenFile("file", FileMode.Read));
            await using (await volume.OpenFile("file", FileMode.Read, ignoreProtectionBits: true))
            {
            }
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenSettingLongCommentsInFullObjectContainerThenObjectIsMoved()
    {
        // arrange - create files filling object container
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < 12; i++)
            {
                await volume.CreateFile($"file{i}");
            }

            // act - set max length comments
            for (var i = 0; i < 12; i++)
            {
                await volume.SetComment($"file{i}", new string((char)('a' + i), 79));
            }
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            var entries = (await volume.ListEntries()).ToList();
            Assert.Equal(12, entries.Count);
            for (var i = 0; i < 12; i++)
            {
                var entry = entries.Single(x => x.Name == $"file{i}");
                Assert.Equal(new string((char)('a' + i), 79), entry.Comment);
            }
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenCreatingSoftLinkThenLinkPathIsListed()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateDirectory("Dir");
            await volume.ChangeDirectory("Dir");
            await volume.CreateFile("file");
            await volume.ChangeDirectory("/");

            // act
            await volume.CreateLink("link", "Dir/file");
            await Assert.ThrowsAsync<PathNotFoundException>(() => volume.CreateLink("link2", "missing"));
            await Assert.ThrowsAsync<PathAlreadyExistsException>(() => volume.CreateLink("link", "Dir"));
        }

        // assert
        await using (var volume = await MountVolume(stream))
        {
            var link = (await volume.FindEntry("link")).Entry;
            Assert.Equal(EntryType.SoftLink, link.Type);
            Assert.Equal("Dir/file", link.LinkPath);

            // act - delete link
            await volume.Delete("link");
            Assert.Null((await volume.FindEntry("link")).Entry);
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenMountingReadOnlyStreamThenWritingFails()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            await WriteFile(volume, "file", CreateTestData(100));
        }

        var bytes = new MemoryStream();
        await stream.WriteTo(bytes);
        var readOnlyStream = new MemoryStream(bytes.ToArray(), false);

        // act
        await using var readOnlyVolume = await MountVolume(readOnlyStream);

        // assert
        Assert.Equal(CreateTestData(100), await ReadFile(readOnlyVolume, "file"));
        await Assert.ThrowsAsync<FileSystemException>(() => readOnlyVolume.CreateDirectory("Dir"));
        await Assert.ThrowsAsync<FileSystemException>(() => readOnlyVolume.OpenFile("file2", FileMode.Write));
    }
}
