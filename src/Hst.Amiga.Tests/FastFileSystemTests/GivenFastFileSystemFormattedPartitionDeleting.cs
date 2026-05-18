using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.Exceptions;
using Xunit;

namespace Hst.Amiga.Tests.FastFileSystemTests;

public class GivenFastFileSystemFormattedPartitionDeleting : FastFileSystemTestBase
{
    [Fact]
    public async Task When_DeletingFile_Then_FileIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file.txt file
        await ffsVolume.CreateFile($"file.txt", true, true);
        
        // act - create and overwrite file.txt
        await ffsVolume.Delete("file.txt");
        
        // assert - file is deleted
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task When_DeletingDir_Then_DirIsDeleted()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create directory
        await ffsVolume.CreateDirectory("dir");
        
        // act - delete dir
        await ffsVolume.Delete("dir");
        
        // assert - dir is deleted
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Empty(entries);
    }
    
    [Fact]
    public async Task When_DeletingDirWithFile_Then_ExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();

        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create directory
        await ffsVolume.CreateDirectory("dir");
        
        // arrange - create file.txt file in dir directory
        await ffsVolume.ChangeDirectory("dir");
        await ffsVolume.CreateFile($"file.txt", true, true);
        
        // act & assert - delete dir throw exception
        await ffsVolume.ChangeDirectory("/");
        await Assert.ThrowsAsync<DirectoryNotEmptyException>(async () => await ffsVolume.Delete("dir"));
    }
}