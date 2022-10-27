namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems;
using Xunit;

public class GivenDos7FormattedFastFileSystemPartition : FastFileSystemTestBase
{
    [Fact]
    public async Task WhenCreateNewDirectoryWithLongFileNameInRootDirectoryThenDirectoryExist()
    {
        var longDirname = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";

        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create dir in root directory
        await ffsVolume.CreateDirectory(longDirname);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains dir created
        Assert.Single(entries);
        var entry = entries.FirstOrDefault(x => x.Name == longDirname);
        Assert.NotNull(entry);
        Assert.Equal(EntryType.Dir, entry.Type);
        Assert.Equal(string.Empty, entry.Comment);
    }

    [Fact]
    public async Task WhenCreateNewDirectoryWithLongFileNameInRootDirectoryAndSetCommentThenDirectoryExistWithComment()
    {
        var longDirname = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
        var comment = "1234567890123456789012345678901234567890123456789012345678901234567890123456789";

        // arrange - create fast file system formatted disk
        await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create dir in root directory
        await ffsVolume.CreateDirectory(longDirname);
        
        // act - set comment for dir in root directory
        await ffsVolume.SetComment(longDirname, comment);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains dir created
        Assert.Single(entries);
        var entry = entries.FirstOrDefault(x => x.Name == longDirname);
        Assert.NotNull(entry);
        Assert.Equal(EntryType.Dir, entry.Type);
        Assert.Equal(comment, entry.Comment);
    }
    
    [Fact]
    public async Task WhenCreateNewFileWithLongFileNameInRootDirectoryThenFileExist()
    {
        var longFilename = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890.txt";

        // arrange - create fast file system formatted disk
        await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        await ffsVolume.CreateFile(longFilename);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var entry = entries.FirstOrDefault(x => x.Name == longFilename);
        Assert.NotNull(entry);
        Assert.Equal(EntryType.File, entry.Type);
        Assert.Equal(string.Empty, entry.Comment);
    }
    
    [Fact]
    public async Task WhenCreateNewFileWithLongFilenameInRootDirectoryAndSetCommentThenFileExistsWithComment()
    {
        var longFilename = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890.txt";
        var comment = "1234567890123456789012345678901234567890123456789012345678901234567890123456789";

        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos7DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        await ffsVolume.CreateFile(longFilename);
        
        // act - set comment for file in root directory
        await ffsVolume.SetComment(longFilename, comment);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var entry = entries.FirstOrDefault(x => x.Name == longFilename);
        Assert.NotNull(entry);
        Assert.Equal(EntryType.File, entry.Type);
        Assert.Equal(comment, entry.Comment);
    }    
}