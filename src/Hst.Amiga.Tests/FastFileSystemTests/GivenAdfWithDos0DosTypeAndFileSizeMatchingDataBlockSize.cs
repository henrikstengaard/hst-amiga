using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Core.Extensions;
using Xunit;
using Directory = Hst.Amiga.FileSystems.FastFileSystem.Directory;

namespace Hst.Amiga.Tests.FastFileSystemTests;

public class GivenAdfWithDos0DosTypeAndFileSizeMatchingDataBlockSize
{
    private readonly byte[] dos0DosType = { 0x44, 0x4f, 0x53, 0 };
        
    [Fact]
    public async Task When_ReadBufferLargerThanFileSizeTwice_Then_FirstBytesReadMatchesSizeAndSecondBytesIsZero()
    {
        // arrange - create double density floppy disk
        var stream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);
            
        const uint lowCyl = FloppyDiskConstants.DoubleDensity.LowCyl;
        const uint highCyl = FloppyDiskConstants.DoubleDensity.HighCyl;
        const uint reservedBlocks = FloppyDiskConstants.DoubleDensity.ReservedBlocks;
        const uint surfaces = FloppyDiskConstants.DoubleDensity.Heads;
        const uint blocksPerTrack = FloppyDiskConstants.DoubleDensity.Sectors;
        const uint blockSize = FloppyDiskConstants.BlockSize;
        const uint fileSystemBlockSize = FloppyDiskConstants.BlockSize;

        // act - format adf
        await FastFileSystemFormatter.Format(stream, lowCyl, highCyl, reservedBlocks,
            surfaces, blocksPerTrack, blockSize, fileSystemBlockSize, dos0DosType, "Workbench");
            
        // arrange - mount adf
        var volume = await FastFileSystemHelper.MountAdf(stream);

        // arrange - write 488 bytes to file
        await using (var entryStream = await Amiga.FileSystems.FastFileSystem.File.Open(volume, volume.RootBlockOffset, "File1",
                         FileSystems.FileMode.Write))
        {
            await entryStream.WriteBytes(new byte[488]);
        }

        // act - read entries
        var entries = (await Directory.ReadEntries(volume, volume.RootBlockOffset)).ToList();

        // assert - file exist
        Assert.Single(entries);
        Assert.True(entries.All(x => x.Name == "File1"));

        var buffer = new byte[4096];
        await using (var entryStream = await Amiga.FileSystems.FastFileSystem.File.Open(volume, volume.RootBlockOffset, "File1",
                         FileSystems.FileMode.Read))
        {
            // act - read from file
            var bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);
                
            // assert - first read is equal to 488 bytes
            Assert.Equal(488, bytesRead);

            // act - read from file
            bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);

            // assert - second read is equal to 0 bytes
            Assert.Equal(0, bytesRead);
        }

    }
}