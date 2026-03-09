using System;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.Exceptions;
using Hst.Amiga.FileSystems.FastFileSystem;
using Xunit;
using FileMode = Hst.Amiga.FileSystems.FileMode;

namespace Hst.Amiga.Tests.FastFileSystemTests;

public class GivenFastFileSystemFormattedPartitionWithLinks : FastFileSystemTestBase
{
    [Fact]
    public async Task When_CreatingLinkToFile_Then_FileAndLinkAreCrossLinked()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // act - create link to file
        await ffsVolume.CreateLink("link", "file");

        // assert - 2 entries exists
        var rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, rawEntries.Count);

        // assert - file and link exists
        var fileEntry = rawEntries.FirstOrDefault(x => x.Name == "file");
        var linkEntry = rawEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);
        Assert.NotNull(linkEntry);
        Assert.NotNull(linkEntry.EntryBlock);

        // assert - file has type file, size and next link set to link entry header key
        Assert.Equal(Constants.ST_FILE, fileEntry.EntryBlock.SecType);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(linkEntry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkPath);

        // assert - link has type link file, size and real entry set to file entry header key
        Assert.Equal(Constants.ST_LFILE, linkEntry.EntryBlock.SecType);
        Assert.Equal(0U, linkEntry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, linkEntry.EntryBlock.RealEntry);
        Assert.Equal("file", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_Creating2LinksToFile_Then_FileAndLinksAreCrossLinked()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // act - create link 1 to file
        await ffsVolume.CreateLink("link1", "file");

        // act - create link 2 to file
        await ffsVolume.CreateLink("link2", "file");

        // assert - 3 entries exists
        var rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(3, rawEntries.Count);

        // assert - file, link 1 and link 2 exists
        var fileEntry = rawEntries.FirstOrDefault(x => x.Name == "file");
        var link1Entry = rawEntries.FirstOrDefault(x => x.Name == "link1");
        var link2Entry = rawEntries.FirstOrDefault(x => x.Name == "link2");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);
        Assert.NotNull(link1Entry);
        Assert.NotNull(link1Entry.EntryBlock);
        Assert.NotNull(link2Entry);
        Assert.NotNull(link2Entry.EntryBlock);

        // assert - file has type file, size and next link set to link 2 entry header key (newest link is first in chain)
        Assert.Equal(Constants.ST_FILE, fileEntry.EntryBlock.SecType);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(link2Entry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkPath);

        // assert - link 1 has type link file, size, real entry set to file entry header key and
        // next link set to 0 (end of link chain)
        Assert.Equal(Constants.ST_LFILE, link1Entry.EntryBlock.SecType);
        Assert.Equal(0U, link1Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link1Entry.EntryBlock.RealEntry);
        Assert.Equal(0U, link1Entry.EntryBlock.NextLink);
        Assert.Equal("file", link1Entry.LinkPath);

        // assert - link 2 has type link file, size, real entry set to file entry header key and
        // next link set to link 1 entry header key (link 1 is next in chain)
        Assert.Equal(Constants.ST_LFILE, link2Entry.EntryBlock.SecType);
        Assert.Equal(0U, link2Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link2Entry.EntryBlock.RealEntry);
        Assert.Equal(link1Entry.EntryBlock.HeaderKey, link2Entry.EntryBlock.NextLink);
        Assert.Equal("file", link2Entry.LinkPath);
    }

    [Fact]
    public async Task When_CreatingLinkToDir_Then_FileAndLinkAreCrossLinked()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create dir
        await ffsVolume.CreateDirectory("dir");

        // act - create link to dir
        await ffsVolume.CreateLink("link", "dir");

        // assert - 2 entries exists
        var rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, rawEntries.Count);

        // assert - dir and link exists
        var dirEntry = rawEntries.FirstOrDefault(x => x.Name == "dir");
        var linkEntry = rawEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(dirEntry);
        Assert.NotNull(dirEntry.EntryBlock);
        Assert.NotNull(linkEntry);
        Assert.NotNull(linkEntry.EntryBlock);

        // assert - dir has type dir, size and next link set to link entry header key
        Assert.Equal(Constants.ST_DIR, dirEntry.EntryBlock.SecType);
        Assert.Equal(0U, dirEntry.Size);
        Assert.Equal(linkEntry.EntryBlock.HeaderKey, dirEntry.EntryBlock.NextLink);
        Assert.Null(dirEntry.LinkPath);

        // assert - link has type link dir, size and real entry set to dir entry header key
        Assert.Equal(Constants.ST_LDIR, linkEntry.EntryBlock.SecType);
        Assert.Equal(0U, linkEntry.Size);
        Assert.Equal(dirEntry.EntryBlock.HeaderKey, linkEntry.EntryBlock.RealEntry);
        Assert.Equal("dir", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_CreatingLinkToFileInSubDir_Then_FileAndLinkAreCrossLinked()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create dir
        await ffsVolume.CreateDirectory("dir");

        // arrange - create file in dir
        await ffsVolume.ChangeDirectory("dir");
        await ffsVolume.CreateFile("file", true, true);

        // act - create link to file in root directory
        await ffsVolume.ChangeDirectory("/");
        await ffsVolume.CreateLink("link", "dir/file");

        // assert - 2 entries exists in root directory
        var rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, rawEntries.Count);

        // assert - dir and link exists
        var dirEntry = rawEntries.FirstOrDefault(x => x.Name == "dir");
        var linkEntry = rawEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(dirEntry);
        Assert.NotNull(dirEntry.EntryBlock);
        Assert.NotNull(linkEntry);
        Assert.NotNull(linkEntry.EntryBlock);

        // assert - 1 entry exists in dir directory
        await ffsVolume.ChangeDirectory("dir");
        rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(rawEntries);

        // assert - file entry exists in dir directory
        var fileEntry = rawEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);

        // assert - dir has type dir, size and next link set to link entry header key
        Assert.Equal(Constants.ST_DIR, dirEntry.EntryBlock.SecType);
        Assert.Equal(0U, dirEntry.Size);
        Assert.Equal(0U, dirEntry.EntryBlock.NextLink);
        Assert.Null(dirEntry.LinkPath);

        // assert - link has type link file, size and real entry set to file entry header key
        Assert.Equal(Constants.ST_LFILE, linkEntry.EntryBlock.SecType);
        Assert.Equal(0U, linkEntry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, linkEntry.EntryBlock.RealEntry);
        Assert.Equal("dir/file", linkEntry.LinkPath);

        // assert - file has type file, size and next link set to link entry header key
        Assert.Equal(Constants.ST_FILE, fileEntry.EntryBlock.SecType);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(linkEntry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkPath);
    }

    [Fact]
    public async Task When_CreatingLinkInSubDirToParentSubDirFile_FileAndLinkAreCrossLinked()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create dir 1 and 2 in root directory
        await ffsVolume.CreateDirectory("dir1");
        await ffsVolume.CreateDirectory("dir2");

        // arrange - create file in dir1
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateFile("file", true, true);

        // arrange - data to write
        var data = new byte[10];
        Array.Fill<byte>(data, 1);

        // arrange - append data to file
        using (var fileStream = await ffsVolume.OpenFile("file", FileMode.Append, true))
        {
            await fileStream.WriteAsync(data);
        }

        // act - create link in dir2 to file in dir1
        await ffsVolume.ChangeDirectory("/");
        await ffsVolume.ChangeDirectory("dir2");
        await ffsVolume.CreateLink("link", "/dir1/file");

        // assert - 2 entries exists in root directory
        await ffsVolume.ChangeDirectory("/");
        var rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, rawEntries.Count);

        // assert - dir1 and dir2 entries exists
        var dir1Entry = rawEntries.FirstOrDefault(x => x.Name == "dir1");
        var dir2Entry = rawEntries.FirstOrDefault(x => x.Name == "dir2");
        Assert.NotNull(dir1Entry);
        Assert.NotNull(dir1Entry.EntryBlock);
        Assert.NotNull(dir2Entry);
        Assert.NotNull(dir2Entry.EntryBlock);

        // assert - dir1 has type dir, size and next link set to link entry header key
        Assert.Equal(Constants.ST_DIR, dir1Entry.EntryBlock.SecType);
        Assert.Equal(0U, dir1Entry.Size);
        Assert.Equal(0U, dir1Entry.EntryBlock.NextLink);
        Assert.Null(dir1Entry.LinkPath);

        // assert - dir2 has type dir, size and next link set to link entry header key
        Assert.Equal(Constants.ST_DIR, dir2Entry.EntryBlock.SecType);
        Assert.Equal(0U, dir2Entry.Size);
        Assert.Equal(0U, dir2Entry.EntryBlock.NextLink);
        Assert.Null(dir2Entry.LinkPath);

        // assert - 1 entry exists in dir1 directory
        await ffsVolume.ChangeDirectory("dir1");
        rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(rawEntries);

        // assert - file entry exists in dir1 directory
        var fileEntry = rawEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);

        // assert - 1 entry exists in dir2 directory
        await ffsVolume.ChangeDirectory("/");
        await ffsVolume.ChangeDirectory("dir2");
        rawEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(rawEntries);

        // assert - link entry exists in dir2 directory
        var linkEntry = rawEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.NotNull(linkEntry.EntryBlock);

        // assert - file has type file, size and next link set to link entry header key
        Assert.Equal(Constants.ST_FILE, fileEntry.EntryBlock.SecType);
        Assert.Equal(10U, fileEntry.Size);
        Assert.Equal(linkEntry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkPath);

        // assert - link has type link file, size and real entry set to file entry header key
        Assert.Equal(Constants.ST_LFILE, linkEntry.EntryBlock.SecType);
        Assert.Equal(0U, linkEntry.Size);
        Assert.Equal(10U, linkEntry.LinkEntryBlock.ByteSize);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, linkEntry.EntryBlock.RealEntry);
        Assert.Equal("/dir1/file", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_CreatingLinkToNonExistingEntry_Then_ErrorIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act and assert - create link to file that does not exists throws exception
        await Assert.ThrowsAsync<PathNotFoundException>(async () => await ffsVolume.CreateLink("link", "file"));
    }

    [Fact]
    public async Task When_WritingDataToLink_Then_FileAndLinkHasSameData()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - append data to file
        using (var fileStream = await ffsVolume.OpenFile("file", FileMode.Append, true))
        {
            var data = new byte[10];
            Array.Fill<byte>(data, 1);
            await fileStream.WriteAsync(data);
        }

        // arrange - create link to file
        await ffsVolume.CreateLink("link", "file");

        // act - write data to entry with name "link"
        using (var fileStream = await ffsVolume.OpenFile("link", FileMode.Write, true))
        {
            var data = new byte[1];
            Array.Fill<byte>(data, 2);
            fileStream.Write(data);
        }

        // assert - 2 entries exists
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Equal(2, entries.Count);

        // assert - file exists, has type file and is 1 byte in size
        var fileEntry = entries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.Equal(EntryType.File, fileEntry.Type);
        Assert.Equal(1, fileEntry.Size);

        // assert - file has data
        var expectedData = new byte[1];
        Array.Fill<byte>(expectedData, 2);
        var actualData = new byte[1];
        int bytesRead;
        using (var fileStream = await ffsVolume.OpenFile("file", FileMode.Read))
        {
            bytesRead = await fileStream.ReadAsync(actualData, 0, actualData.Length);
        }

        Assert.Equal(1, bytesRead);
        Assert.Equal(expectedData, actualData);

        // assert - link exists, has type file link and is 1 byte in size
        var linkEntry = entries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(EntryType.FileLink, linkEntry.Type);
        Assert.Equal(1, linkEntry.Size);

        // assert - link file has data
        using (var fileStream = await ffsVolume.OpenFile("link", FileMode.Read))
        {
            bytesRead = await fileStream.ReadAsync(actualData, 0, actualData.Length);
        }

        Assert.Equal(1, bytesRead);
        Assert.Equal(expectedData, actualData);
    }

    [Fact]
    public async Task When_CreatingFileWithExistingLinkEntryAndOverwrite_Then_FileAndLinkIsEmpty()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - append data to file
        using (var fileStream = await ffsVolume.OpenFile("file", FileMode.Append, true))
        {
            var data = new byte[10];
            Array.Fill<byte>(data, 1);
            await fileStream.WriteAsync(data);
        }

        // arrange - create link to file
        await ffsVolume.CreateLink("link", "file");

        // act - create file with filename "link" to overwrite entry.
        // this should resolve the link to the file and overwrite the file.
        // the link is not changed and should still link to the file, but the file should be overwritten and have 0 byte in size.
        await ffsVolume.CreateFile("link", true, true);

        // assert - 2 entries exists
        var dirEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, dirEntries.Count);

        // assert - file exists, has type file and is 0 byte in size
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.Equal(Constants.ST_FILE, fileEntry.Type);
        Assert.Equal(0U, fileEntry.Size);

        // assert - link exists, has type file and is 0 byte in size
        var linkEntry = dirEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(Constants.ST_LFILE, linkEntry.Type);
        Assert.Equal(0U, linkEntry.Size);
    }

    [Fact]
    public async Task When_DeletingLinkToFile_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - create link to file
        await ffsVolume.CreateLink("link", "file");

        // act - delete link entry
        await ffsVolume.Delete("link");

        // assert - 1 entry exists
        var dirEntries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(dirEntries);

        // assert - file exists, has type file, size 0 and no link entry or path
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.Equal(Constants.ST_FILE, fileEntry.Type);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(0U, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkEntryBlock);
        Assert.Null(fileEntry.LinkPath);
    }

    [Fact]
    public async Task When_DeletingFirstLinkToFileWith2Links_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - create link 1 to file
        await ffsVolume.CreateLink("link1", "file");

        // arrange - create link 2 to file
        await ffsVolume.CreateLink("link2", "file");

        // act - delete link 1 entry
        await ffsVolume.Delete("link1");

        // assert - 2 entry exists
        var entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, entries.Count);

        // assert - file and link 2 entries exists
        var fileEntry = entries.FirstOrDefault(x => x.Name == "file");
        var link2Entry = entries.FirstOrDefault(x => x.Name == "link2");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);
        Assert.NotNull(link2Entry);
        Assert.NotNull(link2Entry.EntryBlock);

        // assert - file entry has type file, size 0 and next link to link 2 entry
        Assert.Equal(Constants.ST_FILE, fileEntry.Type);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(link2Entry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkEntryBlock);
        Assert.Null(fileEntry.LinkPath);

        // assert - link 2 entry has type link file, size 0, next link to 0 and real entry to link 2 entry
        Assert.Equal(Constants.ST_LFILE, link2Entry.Type);
        Assert.Equal(0U, link2Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link2Entry.EntryBlock.RealEntry);
        Assert.Equal(0U, link2Entry.EntryBlock.NextLink);
        Assert.NotNull(link2Entry.LinkEntryBlock);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link2Entry.LinkEntryBlock.HeaderKey);
        Assert.Equal("file", link2Entry.LinkPath);
    }

    [Fact]
    public async Task When_DeletingSecondLinkToFileWith2Links_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - create link 1 to file
        await ffsVolume.CreateLink("link1", "file");

        // arrange - create link 2 to file
        await ffsVolume.CreateLink("link2", "file");

        // act - delete link 2 entry
        await ffsVolume.Delete("link2");

        // assert - 2 entry exists
        var entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(2, entries.Count);

        // assert - file and link 1 entries exists
        var fileEntry = entries.FirstOrDefault(x => x.Name == "file");
        var link1Entry = entries.FirstOrDefault(x => x.Name == "link1");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);
        Assert.NotNull(link1Entry);
        Assert.NotNull(link1Entry.EntryBlock);

        // assert - file entry has type file, size 0 and next link to link 1 entry
        Assert.Equal(Constants.ST_FILE, fileEntry.Type);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(link1Entry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkEntryBlock);
        Assert.Null(fileEntry.LinkPath);

        // assert - link 1 entry has type link file, size 0, next link to 0 and real entry to link 1 entry
        Assert.Equal(Constants.ST_LFILE, link1Entry.Type);
        Assert.Equal(0U, link1Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link1Entry.EntryBlock.RealEntry);
        Assert.Equal(0U, link1Entry.EntryBlock.NextLink);
        Assert.NotNull(link1Entry.LinkEntryBlock);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link1Entry.LinkEntryBlock.HeaderKey);
        Assert.Equal("file", link1Entry.LinkPath);
    }

    [Fact]
    public async Task When_DeletingSecondLinkToFileWith3Links_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("file", true, true);

        // arrange - create link 1 to file
        await ffsVolume.CreateLink("link1", "file");

        // arrange - create link 2 to file
        await ffsVolume.CreateLink("link2", "file");

        // arrange - create link 3 to file
        await ffsVolume.CreateLink("link3", "file");

        // act - delete link 2 entry
        await ffsVolume.Delete("link2");

        // assert - 3 entries exists
        var entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Equal(3, entries.Count);

        // assert - file, link 1 and link 3 entries exists
        var fileEntry = entries.FirstOrDefault(x => x.Name == "file");
        var link1Entry = entries.FirstOrDefault(x => x.Name == "link1");
        var link3Entry = entries.FirstOrDefault(x => x.Name == "link3");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.EntryBlock);
        Assert.NotNull(link1Entry);
        Assert.NotNull(link1Entry.EntryBlock);
        Assert.NotNull(link3Entry);
        Assert.NotNull(link3Entry.EntryBlock);

        // assert - file entry has type file, size 0 and next link to link 3 entry
        Assert.Equal(Constants.ST_FILE, fileEntry.Type);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(link3Entry.EntryBlock.HeaderKey, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkEntryBlock);
        Assert.Null(fileEntry.LinkPath);

        // assert - link 3 entry has type link file, size 0, next link to link 1 entry and real entry to file entry
        Assert.Equal(Constants.ST_LFILE, link3Entry.Type);
        Assert.Equal(0U, link3Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link3Entry.EntryBlock.RealEntry);
        Assert.Equal(link1Entry.EntryBlock.HeaderKey, link3Entry.EntryBlock.NextLink);
        Assert.NotNull(link3Entry.LinkEntryBlock);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link3Entry.LinkEntryBlock.HeaderKey);
        Assert.Equal("file", link3Entry.LinkPath);

        // assert - link 1 entry has type link file, size 0, next link to 0 and real entry to file entry
        Assert.Equal(Constants.ST_LFILE, link1Entry.Type);
        Assert.Equal(0U, link1Entry.Size);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link1Entry.EntryBlock.RealEntry);
        Assert.Equal(0U, link1Entry.EntryBlock.NextLink);
        Assert.NotNull(link1Entry.LinkEntryBlock);
        Assert.Equal(fileEntry.EntryBlock.HeaderKey, link1Entry.LinkEntryBlock.HeaderKey);
        Assert.Equal("file", link1Entry.LinkPath);
    }

    [Fact]
    public async Task When_DeletingLinkToDir_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create dir
        await ffsVolume.CreateDirectory("dir");

        // arrange - create link to dir
        await ffsVolume.CreateLink("link", "dir");
        
        // act - delete link entry
        await ffsVolume.Delete("link");

        // assert - 1 entry exists
        var entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(entries);

        // assert - dir entry exists
        var dirEntry = entries.FirstOrDefault(x => x.Name == "dir");
        Assert.NotNull(dirEntry);
        Assert.NotNull(dirEntry.EntryBlock);
        
        // assert - dir has type dir, size and has no next link, link entry block and link path
        Assert.Equal(Constants.ST_DIR, dirEntry.EntryBlock.SecType);
        Assert.Equal(0U, dirEntry.Size);
        Assert.Equal(0U, dirEntry.EntryBlock.NextLink);
        Assert.Null(dirEntry.LinkEntryBlock);
        Assert.Null(dirEntry.LinkPath);
    }

    [Fact]
    public async Task When_DeletingLinkToFileInSubDirectory_Then_LinkIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create directory with file
        await ffsVolume.CreateDirectory("dir");
        await ffsVolume.ChangeDirectory("dir");
        await ffsVolume.CreateFile("file", true, true);

        // arrange - create link in root directory to file in directory
        await ffsVolume.ChangeDirectory("/");
        await ffsVolume.CreateLink("link", "dir/file");
        
        // act - delete link entry from root directory
        await ffsVolume.Delete("link");
        
        // assert - 1 entry exists
        var entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(entries);
        
        // assert - dir entry exists
        var dirEntry = entries.FirstOrDefault(x => x.Name == "dir");
        Assert.NotNull(dirEntry);

        // assert - dir has type dir, size and has no next link, link entry block and link path
        Assert.Equal(Constants.ST_DIR, dirEntry.EntryBlock.SecType);
        Assert.Equal(0U, dirEntry.Size);
        Assert.Equal(0U, dirEntry.EntryBlock.NextLink);
        Assert.Null(dirEntry.LinkEntryBlock);
        Assert.Null(dirEntry.LinkPath);

        // assert - 1 entry exist in dir        
        await ffsVolume.ChangeDirectory("dir");
        entries = (await ffsVolume.ListRawEntries()).ToList();
        Assert.Single(entries);

        // assert - file entry exists
        var fileEntry = entries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        
        // assert - file has type file, size and has no next link, link entry block and link path
        Assert.Equal(Constants.ST_FILE, fileEntry.EntryBlock.SecType);
        Assert.Equal(0U, fileEntry.Size);
        Assert.Equal(0U, fileEntry.EntryBlock.NextLink);
        Assert.Null(fileEntry.LinkEntryBlock);
        Assert.Null(fileEntry.LinkPath);
    }
}