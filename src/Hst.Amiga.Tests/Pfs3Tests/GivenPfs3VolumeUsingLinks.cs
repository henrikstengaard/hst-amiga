using System;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.Exceptions;
using Hst.Amiga.FileSystems.Pfs3;
using Xunit;
using FileMode = Hst.Amiga.FileSystems.FileMode;

namespace Hst.Amiga.Tests.Pfs3Tests;

public class GivenPfs3VolumeUsingLinks : Pfs3TestBase
{
    [Fact]
    public async Task When_CreatingLinkToFile_Then_FileLinkIsCreated()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file
        await pfs3Volume.CreateFile("file", true, true);
        
        // act - create link to file
        await pfs3Volume.CreateLink("link", "file");

        // assert - 2 entries exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Equal(2, dirEntries.Count);
        
        // assert - file and link exists
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        var linkEntry = dirEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(fileEntry);
        Assert.NotNull(linkEntry);

        // assert - file has type file and has link to link entry
        Assert.Equal(Constants.ST_FILE, fileEntry.type);
        Assert.Equal(linkEntry.anode, fileEntry.ExtraFields.link);
        
        // assert - link has type link file and has link to file entry
        Assert.Equal(Constants.ST_LINKFILE, linkEntry.type);
        Assert.Equal(fileEntry.anode, linkEntry.ExtraFields.link);
    }

    [Fact]
    public async Task When_CreatingLinkToDir_Then_DirLinkIsCreated()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create dir
        await pfs3Volume.CreateDirectory("dir");
        
        // act - create link to dir
        await pfs3Volume.CreateLink("link", "dir");
        
        // assert - 2 entries exists
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - dir exists and has type dir
        var dirEntry = entries.FirstOrDefault(x => x.Name == "dir");
        Assert.NotNull(dirEntry);
        Assert.Equal(EntryType.Dir, dirEntry.Type);
        
        // assert - link exists and has type dir link
        var linkEntry = entries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(EntryType.DirLink, linkEntry.Type);
    }

    [Fact]
    public async Task When_CreatingLinkToFileInSubDirectory_Then_FileLinkIsCreated()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directory with file
        await pfs3Volume.CreateDirectory("dir");
        await pfs3Volume.ChangeDirectory("dir");
        await pfs3Volume.CreateFile("file", true, true);

        // arrange - data to write
        var data = new byte[10];
        Array.Fill<byte>(data, 1);
        
        // arrange - append data to file
        using (var fileStream = await pfs3Volume.OpenFile("file", FileMode.Append, true))
        {
            await fileStream.WriteAsync(data);
        }
        
        // act - create link in root directory to file in directory
        await pfs3Volume.ChangeDirectory("/");
        await pfs3Volume.CreateLink("link", "dir/file");

        // assert - 2 entries exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Equal(2, dirEntries.Count);
        
        // assert - dir and link exists
        var dirEntry = dirEntries.FirstOrDefault(x => x.Name == "dir");
        var linkEntry = dirEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(dirEntry);
        Assert.NotNull(linkEntry);

        // assert - dir has type user dir and has size 0
        Assert.Equal(Constants.ST_USERDIR, dirEntry.type);
        Assert.Equal(0U, dirEntry.Size);
        
        // assert - link has type link file and has a link
        Assert.Equal(Constants.ST_LINKFILE, linkEntry.type);
        Assert.True(linkEntry.ExtraFields.link != 0);
        
        // assert - read data from link and assert data matches data written to file
        var actualData = new byte[10];
        int bytesRead;
        using (var fileStream = await pfs3Volume.OpenFile("link", FileMode.Read))
        {
            bytesRead = await fileStream.ReadAsync(actualData, 0, actualData.Length);
        }
        Assert.Equal(10, bytesRead);
        Assert.Equal(data, actualData);
    }
    
    [Fact]
    public async Task When_CreatingLinkToNonExistingEntry_Then_ErrorIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act and assert - create link to file that does not exists throws exception
        await Assert.ThrowsAsync<PathNotFoundException>(async () => await pfs3Volume.CreateLink("link", "file"));
    }

    [Fact]
    public async Task When_CreatingWritingDataToLink_Then_FileAndLinkHasSameData()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);
        
        // arrange - create file
        await pfs3Volume.CreateFile("file", true, true);
        
        // arrange - append data to file
        using (var fileStream = await pfs3Volume.OpenFile("file", FileMode.Append, true))
        {
            var data = new byte[10];
            Array.Fill<byte>(data, 1);
            await fileStream.WriteAsync(data);
        }
        
        // arrange - create link to file
        await pfs3Volume.CreateLink("link", "file");
        
        // act - write data to entry with name "link"
        using (var fileStream = await pfs3Volume.OpenFile("link", FileMode.Write, true))
        {
            var data = new byte[1];
            Array.Fill<byte>(data, 2);
            fileStream.Write(data);
        }

        // assert - 2 entries exists
        var entries = (await pfs3Volume.ListEntries()).ToList();
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
        using (var fileStream = await pfs3Volume.OpenFile("file", FileMode.Read))
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
        using (var fileStream = await pfs3Volume.OpenFile("link", FileMode.Read))
        {
            bytesRead = await fileStream.ReadAsync(actualData, 0, actualData.Length);
        }
        Assert.Equal(1, bytesRead);
        Assert.Equal(expectedData, actualData);
    }

    [Fact]
    public async Task When_CreatingFileWithExistingLinkEntryAndOverwrite_Then_FileAndLinkIsEmpty()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);
        
        // arrange - create file
        await pfs3Volume.CreateFile("file", true, true);
        
        // arrange - write data to file
        using (var fileStream = await pfs3Volume.OpenFile("file", FileMode.Append, true))
        {
            var data = new byte[10];
            Array.Fill<byte>(data, 1);
            await fileStream.WriteAsync(data);
        }
        
        // arrange - create link to file
        await pfs3Volume.CreateLink("link", "file");
        
        // act - create file with filename "link" to overwrite entry.
        // this should resolve the link to the file and overwrite the file.
        // the link is not changed and should still link to the file, but the file should be overwritten and have 0 byte in size.
        await pfs3Volume.CreateFile("link", true, true);

        // assert - 2 entries exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Equal(2, dirEntries.Count);
        
        // assert - file exists, has type file and is 0 byte in size
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);
        Assert.Equal(Constants.ST_FILE, fileEntry.type);
        Assert.Equal(0U, fileEntry.fsize);
        
        // assert - link exists, has type file and is 0 byte in size
        var linkEntry = dirEntries.FirstOrDefault(x => x.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(Constants.ST_LINKFILE, linkEntry.type);
        Assert.Equal(0U, linkEntry.fsize);
    }
    
    [Fact]
    public async Task When_DeletingLinkToFile_Then_LinkIsDeleted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file
        await pfs3Volume.CreateFile("file", true, true);
        
        // arrange - create link to file
        await pfs3Volume.CreateLink("link", "file");

        // act - delete link entry
        await pfs3Volume.Delete("link");
        
        // assert - 1 entry exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Single(dirEntries);
        
        // assert - file entry exists
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);

        // assert - file entry has type file and has no link
        Assert.Equal(Constants.ST_FILE, fileEntry.type);
        Assert.Equal(0U, fileEntry.ExtraFields.link);
    }
    
    [Fact]
    public async Task When_DeletingLinkToDir_Then_LinkIsDeleted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create dir
        await pfs3Volume.CreateDirectory("dir");
        
        // arrange - create link to file
        await pfs3Volume.CreateLink("link", "dir");

        // act - delete link entry
        await pfs3Volume.Delete("link");
        
        // assert - 1 entry exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Single(dirEntries);
        
        // assert - dir entry exists
        var dirEntry = dirEntries.FirstOrDefault(x => x.Name == "dir");
        Assert.NotNull(dirEntry);

        // assert - dir entry has type user dir and has no link
        Assert.Equal(Constants.ST_USERDIR, dirEntry.type);
        Assert.Equal(0U, dirEntry.ExtraFields.link);
    }
    
    [Fact]
    public async Task When_DeletingLinkToFileInSubDirectory_Then_LinkIsDeleted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directory with file
        await pfs3Volume.CreateDirectory("dir");
        await pfs3Volume.ChangeDirectory("dir");
        await pfs3Volume.CreateFile("file", true, true);

        // arrange - create link in root directory to file in directory
        await pfs3Volume.ChangeDirectory("/");
        await pfs3Volume.CreateLink("link", "dir/file");
        
        // act - delete link entry from root directory
        await pfs3Volume.Delete("link");

        // assert - 1 entry exists
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Single(dirEntries);
        
        // assert - dir entry exists
        var dirEntry = dirEntries.FirstOrDefault(x => x.Name == "dir");
        Assert.NotNull(dirEntry);

        // assert - dir entry has type user dir and has size 0
        Assert.Equal(Constants.ST_USERDIR, dirEntry.type);
        Assert.Equal(0U, dirEntry.Size);
        
        // assert - 1 entry exist in dir        
        await pfs3Volume.ChangeDirectory("dir");
        dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();
        Assert.Single(dirEntries);

        // assert - file entry exists
        var fileEntry = dirEntries.FirstOrDefault(x => x.Name == "file");
        Assert.NotNull(fileEntry);

        // assert - file entry has type file and has no link
        Assert.Equal(Constants.ST_FILE, fileEntry.type);
        Assert.Equal(0U, fileEntry.ExtraFields.link);
    }
}