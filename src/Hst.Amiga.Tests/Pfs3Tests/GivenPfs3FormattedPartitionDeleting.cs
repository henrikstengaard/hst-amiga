using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.Exceptions;
using Xunit;

namespace Hst.Amiga.Tests.Pfs3Tests;

public class GivenPfs3FormattedPartitionDeleting : Pfs3TestBase
{
    [Fact]
    public async Task When_DeletingFile_Then_FileIsDeleted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file.txt file
        await pfs3Volume.CreateFile($"file.txt", true, true);
        
        // act - create and overwrite file.txt
        await pfs3Volume.Delete("file.txt");
        
        // assert - file is deleted
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task When_DeletingDir_Then_DirIsDeleted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directory
        await pfs3Volume.CreateDirectory("dir");
        
        // act - delete dir
        await pfs3Volume.Delete("dir");
        
        // assert - dir is deleted
        var entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Empty(entries);
    }
    
    [Fact]
    public async Task When_DeletingDirWithFile_Then_ExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directory
        await pfs3Volume.CreateDirectory("dir");
        
        // arrange - create file.txt file in dir directory
        await pfs3Volume.ChangeDirectory("dir");
        await pfs3Volume.CreateFile($"file.txt", true, true);
        
        // act & assert - delete dir throw exception
        await pfs3Volume.ChangeDirectory("/");
        await Assert.ThrowsAsync<DirectoryNotEmptyException>(async () => await pfs3Volume.Delete("dir"));
    }
}