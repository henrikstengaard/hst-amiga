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
}