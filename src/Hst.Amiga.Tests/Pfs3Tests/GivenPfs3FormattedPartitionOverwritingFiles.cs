using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Xunit;

namespace Hst.Amiga.Tests.Pfs3Tests;

public class GivenPfs3FormattedPartitionOverwritingFiles : Pfs3TestBase
{
    /// <summary>
    /// When pfs3 overwrites a file with size enough to match the size of the partition, then the file
    /// is overwritten and not deleted. This is because pfs3 does not delete the file when overwriting,
    /// but instead marks the file as deleted and creates a new file with the same name.
    /// If the new file has the same size as the old file, then pfs3 will reuse the same blocks for the new file,
    /// which means that the old file is not actually deleted. This test verifies that when overwriting
    /// a file with size enough to match the size of the partition, then the file is overwritten and not deleted.
    /// </summary>
    [Fact]
    public async Task When_OverwritingFileWithSizeEnoughToMatchSizeOfPartition_Then_FileIsOverwritten()
    {
        // arrange - disk and file size
        var diskSize = 100.MB();
        const int fileSize = 1024 * 1024 * 10;
        
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create directory
        await pfs3Volume.CreateDirectory("dir");

        // act - create and overwrite file.txt 10 times
        for (var i = 0; i < 10; i++)
        {
            // act - create and overwrite file.txt
            await pfs3Volume.CreateFile($"file.txt", true, true);

            // act - write data to file.txt
            await using var fileStream = await pfs3Volume.OpenFile($"file.txt", FileMode.Append);
            await fileStream.WriteAsync(new byte[fileSize]);
        }
        
        // act - create and overwrite file.txt
        await pfs3Volume.CreateFile("file.txt", true, true);
    }
}