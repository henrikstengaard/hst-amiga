using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Xunit;

namespace Hst.Amiga.Tests.FastFileSystemTests;

public class GivenFastFileSystemFormattedPartitionRenamingMoving : FastFileSystemTestBase
{
    [Fact]
    public async Task When_RenamingFile_Then_FileIsRenamed()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file.txt file
        await ffsVolume.CreateFile($"file.txt", true, true);
        
        // act - rename file.txt to renamed.txt
        await ffsVolume.Rename("file.txt", "renamed.txt");
        
        // assert - file is renamed
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }

    [Fact]
    public async Task When_RenamingDirectory_Then_DirectoryIsRenamed()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create dir directory
        await ffsVolume.CreateDirectory("dir");
        
        // act - rename dir to renamed
        await ffsVolume.Rename("dir", "renamed");
        
        // assert - dir is renamed
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
    }
    
    [Fact]
    public async Task When_MovingFileToSubDirectory_Then_FileIsMoved()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file.txt file
        await ffsVolume.CreateFile($"file.txt", true, true);
        
        // arrange - create dir directory
        await ffsVolume.CreateDirectory("dir");
        
        // act - rename file.txt to renamed.txt in dir directory
        await ffsVolume.Rename("file.txt", "dir/renamed.txt");
        
        // assert - root directory contains dir entry
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("dir", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - dir directory contains file.txt entry
        await ffsVolume.ChangeDirectory("dir");
        entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }
    
    [Fact]
    public async Task When_MovingDirectoryToSubDirectory_Then_DirectoryIsMoved()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // arrange - create dir1 directory in root directory
        await ffsVolume.CreateDirectory("dir1");

        // arrange - create file.txt file in dir1 directory
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateFile("file.txt", true, true);
        
        // arrange - create dir2 directory in root directory
        await ffsVolume.ChangeDirectory("/");
        await ffsVolume.CreateDirectory("dir2");
        
        // act - rename dir1 to renamed in dir2 directory
        await ffsVolume.Rename("dir1", "dir2/renamed");
        
        // assert - root directory contains dir2 entry
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("dir2", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - dir2 directory contains renamed directory entry
        await ffsVolume.ChangeDirectory("dir2");
        entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("renamed", entries[0].Name);
        Assert.Equal(EntryType.Dir, entries[0].Type);
        
        // assert - renamed directory contains file.txt file entry
        await ffsVolume.ChangeDirectory("renamed");
        entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Single(entries);
        Assert.Equal("file.txt", entries[0].Name);
        Assert.Equal(EntryType.File, entries[0].Type);
    }

    [Fact]
    public async Task When_MovingFileFromSubDirectoryToRootDirectory_Then_FileIsMoved()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // arrange - create dir1 directory in root directory
        await ffsVolume.CreateDirectory("dir1");

        // arrange - create dir2 directory in dir1 directory
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateDirectory("dir2");
        
        // arrange - create file.txt file in dir2 directory
        await ffsVolume.ChangeDirectory("dir2");
        await ffsVolume.CreateFile("file.txt", true, true);

        // act - rename file.txt to file.txt in root directory
        await ffsVolume.Rename("file.txt", "/file.txt");

        // assert - dir2 directory is empty
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Empty(entries);
        
        // assert - root directory contains 2 entries
        await ffsVolume.ChangeDirectory("/");
        entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Equal(2, entries.Count);

        // assert - root directory contains dir1 entry
        var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir1");
        Assert.NotNull(dir1Entry);
        Assert.Equal(EntryType.Dir, dir1Entry.Type);

        // assert - root directory contains file.txt entry
        var fileTxtEntry = entries.FirstOrDefault(x => x.Name == "file.txt");
        Assert.NotNull(fileTxtEntry);
        Assert.Equal(EntryType.File, fileTxtEntry.Type);
    }
    
    [Fact]
    public async Task When_MovingDirectoryFromSubDirectoryToRootDirectory_Then_DirectoryIsMoved()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // arrange - create dir1 directory in root directory
        await ffsVolume.CreateDirectory("dir1");

        // arrange - create dir2 directory in dir1 directory
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateDirectory("dir2");
        
        // arrange - create file.txt file in dir2 directory
        await ffsVolume.ChangeDirectory("dir2");
        await ffsVolume.CreateFile("file.txt", true, true);

        // act - rename dir2 in dir1 to root directory
        await ffsVolume.ChangeDirectory("/dir1");
        await ffsVolume.Rename("dir2", "/dir2");

        // assert - dir1 directory is empty
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Empty(entries);
        
        // assert - root directory contains 2 entries
        await ffsVolume.ChangeDirectory("/");
        entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Equal(2, entries.Count);

        // assert - root directory contains dir1 entry
        var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir1");
        Assert.NotNull(dir1Entry);
        Assert.Equal(EntryType.Dir, dir1Entry.Type);

        // assert - root directory contains dir2 entry
        var dir2Entry = entries.FirstOrDefault(x => x.Name == "dir2");
        Assert.NotNull(dir2Entry);
        Assert.Equal(EntryType.Dir, dir2Entry.Type);

        // assert - dir2 directory contains file.txt entry
        await ffsVolume.ChangeDirectory("dir2");
        entries = (await ffsVolume.ListEntries()).ToList();
        var fileTxtEntry = entries.FirstOrDefault(x => x.Name == "file.txt");
        Assert.NotNull(fileTxtEntry);
        Assert.Equal(EntryType.File, fileTxtEntry.Type);
    }
}