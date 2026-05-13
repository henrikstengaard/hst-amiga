using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Xunit;

namespace Hst.Amiga.Tests.Pfs3Tests;

public class GivenPfs3FormattedPartitionRenamingMoving : Pfs3TestBase
{
    [Fact]
    public async Task When_RenamingFile_Then_FileIsRenamed()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file.txt file
        await pfs3Volume.CreateFile($"file.txt", true, true);
        
        // act - rename file.txt to renamed.txt
        await pfs3Volume.Rename("file.txt", "renamed.txt");
        
        // assert - file is renamed
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }

    [Fact]
    public async Task When_RenamingDirectory_Then_DirectoryIsRenamed()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create dir directory
        await pfs3Volume.CreateDirectory("dir");
        
        // act - rename dir to renamed
        await pfs3Volume.Rename("dir", "renamed");
        
        // assert - dir is renamed
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
    }
    
    [Fact]
    public async Task When_MovingFileToSubDirectory_Then_FileIsMoved()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file.txt file
        await pfs3Volume.CreateFile($"file.txt", true, true);
        
        // arrange - create dir directory
        await pfs3Volume.CreateDirectory("dir");
        
        // act - rename file.txt to renamed.txt in dir directory
        await pfs3Volume.Rename("file.txt", "dir/renamed.txt");
        
        // assert - root directory contains dir entry
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("dir", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - dir directory contains file.txt entry
        await pfs3Volume.ChangeDirectory("dir");
        entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }
    
    [Fact]
    public async Task When_MovingDirectoryToSubDirectory_Then_DirectoryIsMoved()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        
        // arrange - create dir1 directory in root directory
        await pfs3Volume.CreateDirectory("dir1");

        // arrange - create file.txt file in dir1 directory
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateFile("file.txt", true, true);
        
        // arrange - create dir2 directory in root directory
        await pfs3Volume.ChangeDirectory("/");
        await pfs3Volume.CreateDirectory("dir2");
        
        // act - rename dir1 to renamed in dir2 directory
        await pfs3Volume.Rename("dir1", "dir2/renamed");
        
        // assert - root directory contains dir2 entry
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("dir2", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - dir2 directory contains renamed directory entry
        await pfs3Volume.ChangeDirectory("dir2");
        entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - renamed directory contains file.txt file entry
        await pfs3Volume.ChangeDirectory("renamed");
        entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("file.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }
}