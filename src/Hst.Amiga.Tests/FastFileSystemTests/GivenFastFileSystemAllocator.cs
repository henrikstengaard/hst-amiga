namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;
using FfsDisk = FileSystems.FastFileSystem.Disk;
using FfsFile = FileSystems.FastFileSystem.File;
using FileMode = FileSystems.FileMode;

public class GivenFastFileSystemAllocator : FastFileSystemTestBase
{
    private static byte[] CreateData(int size, int seed = 0) =>
        Enumerable.Range(0, size).Select(x => (byte)((x + seed) * 7 % 251)).ToArray();

    private static async Task<Stream> CreateAdfStream()
    {
        var stream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl,
            FloppyDiskConstants.DoubleDensity.ReservedBlocks, FloppyDiskConstants.DoubleDensity.Heads,
            FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.FileSystemBlockSize,
            DosTypeHelper.FormatDosType("DOS3"), "ADF");
        return stream;
    }

    private static async Task WriteFile(FastFileSystemVolume volume, string name, byte[] data)
    {
        await volume.CreateFile(name);
        await using var entryStream = await volume.OpenFile(name, FileMode.Append);
        await entryStream.WriteAsync(data, 0, data.Length);
    }

    private static async Task<byte[]> ReadFile(FastFileSystemVolume volume, string name)
    {
        await using var entryStream = await volume.OpenFile(name, FileMode.Read);
        var output = new MemoryStream();
        await entryStream.CopyToAsync(output);
        return output.ToArray();
    }

    private static async Task<FileBlocks> GetFileBlocks(Volume volume, string name)
    {
        var entry = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset)).First(x => x.Name == name);
        var entryBlock = await FfsDisk.ReadEntryBlock(volume, entry.Sector);
        return await FfsFile.GetFileBlocks(volume, entryBlock);
    }

    [Fact]
    public async Task WhenWriteLargeFileOnEmptyDiskThenDataBlocksAreConsecutive()
    {
        // arrange - create formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        var data = CreateData(2 * 1024 * 1024);

        // act - write large file in chunks
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateFile("Large");
            await using var entryStream = await volume.OpenFile("Large", FileMode.Append);
            for (var position = 0; position < data.Length; position += 10000)
            {
                await entryStream.WriteAsync(data, position, System.Math.Min(10000, data.Length - position));
            }
        }

        // assert - data blocks are ascending and only split by file ext blocks allocated between data runs
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var internalVolume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
            partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize);
        var fileBlocks = await GetFileBlocks(internalVolume, "Large");
        Assert.Equal(4096U, fileBlocks.nbData);

        var breaks = 0;
        for (var i = 1; i < fileBlocks.data.Length; i++)
        {
            Assert.True(fileBlocks.data[i] > fileBlocks.data[i - 1]);
            if (fileBlocks.data[i] != fileBlocks.data[i - 1] + 1)
            {
                breaks++;
            }
        }

        Assert.True(breaks <= fileBlocks.nbExtens,
            $"{breaks} breaks in data blocks exceeds {fileBlocks.nbExtens} file ext blocks");

        // assert - no file ext block is located between consecutive data blocks
        foreach (var extSector in fileBlocks.extens)
        {
            Assert.DoesNotContain(extSector, fileBlocks.data);
        }

        // assert - data read is equal
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(data, await ReadFile(volume, "Large"));
        }
    }

    [Fact]
    public async Task WhenWriteAndDeleteFilesThenFreeIsEqualToFreeCountedFromBitmap()
    {
        // arrange - create formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        long expectedFree;

        // act - write files, delete some and overwrite one
        await using (var volume = await MountVolume(stream))
        {
            var freeBefore = volume.Free;
            for (var i = 0; i < 20; i++)
            {
                await WriteFile(volume, $"File{i}", CreateData(3000 * (i + 1), i));
            }

            Assert.True(volume.Free < freeBefore);

            for (var i = 0; i < 20; i += 3)
            {
                await volume.Delete($"File{i}");
            }

            await volume.CreateDirectory("Dir");
            await volume.Rename("File1", "Renamed");
            expectedFree = volume.Free;
        }

        // assert - free counted from bitmap after remount is equal
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(expectedFree, volume.Free);
        }
    }

    [Fact]
    public async Task WhenWriteFileLargerThanLargestFreeRunThenSmallHolesAreUsed()
    {
        // arrange - fill adf with small files, each using 3 blocks (header and 2 data blocks)
        var stream = await CreateAdfStream();
        var smallData = CreateData(1024);
        const int smallFiles = 570;
        await using (var volume = await FastFileSystemVolume.MountAdf(stream))
        {
            for (var i = 0; i < smallFiles; i++)
            {
                await WriteFile(volume, $"S{i}", smallData);
            }

            // arrange - delete every other small file to leave holes of 3 free blocks
            for (var i = 0; i < smallFiles; i += 2)
            {
                await volume.Delete($"S{i}");
            }
        }

        // act - write file larger than largest run of free blocks
        var data = CreateData(300 * 1024, 3);
        long expectedFree;
        await using (var volume = await FastFileSystemVolume.MountAdf(stream))
        {
            await WriteFile(volume, "Large", data);
            expectedFree = volume.Free;
        }

        // assert - large file and remaining small files are equal and free is equal after remount
        await using (var volume = await FastFileSystemVolume.MountAdf(stream))
        {
            Assert.Equal(expectedFree, volume.Free);
            Assert.Equal(data, await ReadFile(volume, "Large"));
            for (var i = 1; i < smallFiles; i += 2)
            {
                Assert.Equal(smallData, await ReadFile(volume, $"S{i}"));
            }
        }
    }
}
