using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using Xunit;

namespace Hst.Amiga.Tests.FastFileSystemTests;

public class GivenFastFileSystemFormattedAdfWithLinks : FastFileSystemTestBase
{
    [Fact]
    public async Task When_ListingEntriesWithLinkToFile_ThenEntriesAreListed()
    {
        // arrange - paths
        var adfPath = Path.Combine("TestData", "FastFileSystems", "ffs-file-link.adf");
        
        // arrange - read adf file to stream
        using var adfStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(adfPath));
        
        // arrange - mount fast file system volume
        var ffsVolume = await FastFileSystemVolume.MountAdf(adfStream);

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory 2 entries
        Assert.Equal(2, entries.Count);

        // assert - file and link entries exist
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file");
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(fileEntry);
        Assert.NotNull(linkEntry);
        
        // assert - file entry is a file type, has size and no link path
        Assert.Equal(EntryType.File, fileEntry.Type);
        Assert.Equal(15, fileEntry.Size);
        Assert.Null(fileEntry.LinkPath);
        
        // assert - link entry is a file link, has size of file and link path relative to file
        Assert.Equal(EntryType.FileLink, linkEntry.Type);
        Assert.Equal(15, linkEntry.Size);
        Assert.Equal("file", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_ListingEntriesWith2LinksToFile_ThenEntriesAreListed()
    {
        // arrange - paths
        var adfPath = Path.Combine("TestData", "FastFileSystems", "ffs-file-2-links.adf");
        
        // arrange - read adf file to stream
        using var adfStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(adfPath));
        
        // arrange - mount fast file system volume
        var ffsVolume = await FastFileSystemVolume.MountAdf(adfStream);

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory 3 entries
        Assert.Equal(3, entries.Count);

        // assert - file, link1 and link2 entries exist
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file");
        var link1Entry = entries.FirstOrDefault(e => e.Name == "link1");
        var link2Entry = entries.FirstOrDefault(e => e.Name == "link2");
        Assert.NotNull(fileEntry);
        Assert.NotNull(link1Entry);
        Assert.NotNull(link2Entry);
        
        // assert - file entry is a file type, has size and no link path
        Assert.Equal(EntryType.File, fileEntry.Type);
        Assert.Equal(15, fileEntry.Size);
        Assert.Null(fileEntry.LinkPath);
        
        // assert - link1 entry is a file link, has size of file and link path relative to file
        Assert.Equal(EntryType.FileLink, link1Entry.Type);
        Assert.Equal(15, link1Entry.Size);
        Assert.Equal("file", link1Entry.LinkPath);

        // assert - link2 entry is a file link, has size of file and link path relative to file
        Assert.Equal(EntryType.FileLink, link2Entry.Type);
        Assert.Equal(15, link2Entry.Size);
        Assert.Equal("file", link2Entry.LinkPath);
    }
    
    [Fact]
    public async Task When_ListingEntriesWithLinkToDir_ThenEntriesAreListed()
    {
        // arrange - paths
        var adfPath = Path.Combine("TestData", "FastFileSystems", "ffs-dir-link.adf");
        
        // arrange - read adf file to stream
        using var adfStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(adfPath));
        
        // arrange - mount fast file system volume
        var ffsVolume = await FastFileSystemVolume.MountAdf(adfStream);

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory 2 entries
        Assert.Equal(2, entries.Count);

        // assert - dir and link entries exist
        var dirEntry = entries.FirstOrDefault(e => e.Name == "dir");
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(dirEntry);
        Assert.NotNull(linkEntry);
        
        // assert - dir entry is a dir type, has no size and link path
        Assert.Equal(EntryType.Dir, dirEntry.Type);
        Assert.Equal(0, dirEntry.Size);
        Assert.Null(dirEntry.LinkPath);
        
        // assert - link entry is a dir link, has no size and link path relative to dir
        Assert.Equal(EntryType.DirLink, linkEntry.Type);
        Assert.Equal(0, linkEntry.Size);
        Assert.Equal("dir", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_ListingEntriesWithLinkToSubDirFile_ThenEntriesAreListed()
    {
        // arrange - paths
        var adfPath = Path.Combine("TestData", "FastFileSystems", "ffs-file-in-dir-link.adf");
        
        // arrange - read adf file to stream
        using var adfStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(adfPath));
        
        // arrange - mount fast file system volume
        var ffsVolume = await FastFileSystemVolume.MountAdf(adfStream);

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains 2 entries
        Assert.Equal(2, entries.Count);

        // assert - dir and link entries exists
        var dirEntry = entries.FirstOrDefault(e => e.Name == "dir");
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(dirEntry);  
        Assert.NotNull(linkEntry);  
        
        // assert - dir entry is a dir type, has no size and link path
        Assert.Equal(EntryType.Dir, dirEntry.Type);
        Assert.Equal(0, dirEntry.Size);
        Assert.Null(dirEntry.LinkPath);

        // assert - link entry is a file link, has size and link path relative to file in dir
        Assert.Equal(EntryType.FileLink, linkEntry.Type);
        Assert.Equal(15, linkEntry.Size);
        Assert.Equal("dir/file", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_ListingEntriesInSubDirWithLinkToParentSubDirFile_ThenEntriesAreListed()
    {
        // arrange - paths
        var adfPath = Path.Combine("TestData", "FastFileSystems", "ffs-2-dirs-file-link.adf");
        
        // arrange - read adf file to stream
        using var adfStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(adfPath));
        
        // arrange - mount fast file system volume
        var ffsVolume = await FastFileSystemVolume.MountAdf(adfStream);

        // arrange - change directory to dir2 which contains link to file in dir1
        await ffsVolume.ChangeDirectory("dir2");
        
        // act - list entries in dir2
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - dir2 contains 1 entry
        Assert.Single(entries);

        // assert - link entry exists
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(linkEntry);  
        
        // assert - link entry is a file link, has size and link path absolute to file in dir1
        Assert.Equal(EntryType.FileLink, linkEntry.Type);
        Assert.Equal(15, linkEntry.Size);
        Assert.Equal("/dir1/file", linkEntry.LinkPath);
    }
}