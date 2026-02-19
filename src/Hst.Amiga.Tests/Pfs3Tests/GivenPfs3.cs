using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Amiga.Tests.Pfs3Tests;

public class GivenPfs3 : Pfs3TestBase
{
    [Fact]
    public async Task When_ListingRawDirEntries_Then_RawDirEntriesAreReturned()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directories and files
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateFile("file1", true, true);
        await pfs3Volume.CreateFile("file2", true, true);
        
        // act - list raw dir entries
        var dirEntries = (await pfs3Volume.ListRawDirEntries()).ToList();

        // assert - 3 dir entries exist
        Assert.Equal(3, dirEntries.Count);
        
        // assert - dir1 exists and has type 2 (directory)
        var dir1DirEntry = dirEntries.FirstOrDefault(x => x.Name == "dir1");
        Assert.NotNull(dir1DirEntry);
        Assert.Equal(2, dir1DirEntry.type);
        Assert.Equal(0U, dir1DirEntry.fsize);
        
        // assert - file1 exists and has type -3 (file)
        var file1DirEntry = dirEntries.FirstOrDefault(x => x.Name == "file1");
        Assert.NotNull(file1DirEntry);
        Assert.Equal(-3, file1DirEntry.type);
        Assert.Equal(0U, file1DirEntry.fsize);
        
        // assert - file2 exists and has type -3 (file)
        var file2DirEntry = dirEntries.FirstOrDefault(x => x.Name == "file2");
        Assert.NotNull(file2DirEntry);
        Assert.Equal(-3, file2DirEntry.type);
        Assert.Equal(0U, file2DirEntry.fsize);
    }
}