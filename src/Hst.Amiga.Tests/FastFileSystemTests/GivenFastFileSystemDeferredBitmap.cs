namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Blocks;
using RigidDiskBlocks;
using Xunit;
using Constants = FileSystems.FastFileSystem.Constants;
using FileMode = FileSystems.FileMode;

public class GivenFastFileSystemDeferredBitmap : FastFileSystemTestBase
{
    private static async Task<RootBlock> ReadRootBlockFromDisk(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var volume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
            partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize);
        return volume.RootBlock;
    }

    private static async Task WriteFile(FastFileSystemVolume volume, string name, int size)
    {
        var data = Enumerable.Range(0, size).Select(x => (byte)(x % 256)).ToArray();
        await volume.CreateFile(name);
        await using var entryStream = await volume.OpenFile(name, FileMode.Append);
        await entryStream.WriteAsync(data, 0, data.Length);
    }

    [Fact]
    public async Task WhenWriteFileThenBitmapIsWrittenOnFlush()
    {
        // arrange - create formatted disk, create file and get free bytes (create file updates bitmap)
        var stream = await CreateFastFileSystemFormattedDisk();
        var volume = await MountVolume(stream);
        await volume.CreateFile("File1");
        var freeBefore = volume.Free;

        // act - write file data without flushing volume
        var data = new byte[100000];
        await using (var entryStream = await volume.OpenFile("File1", FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // assert - root block bitmap flags are invalid and bitmap on disk is not updated
        Assert.Equal(Constants.BM_INVALID, (await ReadRootBlockFromDisk(stream)).BitmapFlags);
        await using (var otherVolume = await MountVolume(stream, true))
        {
            Assert.Equal(freeBefore, otherVolume.Free);
        }

        // act - flush volume
        await volume.Flush();

        // assert - root block bitmap flags are valid and bitmap on disk is updated
        Assert.Equal(Constants.BM_VALID, (await ReadRootBlockFromDisk(stream)).BitmapFlags);
        await using (var otherVolume = await MountVolume(stream, true))
        {
            Assert.Equal(volume.Free, otherVolume.Free);
            Assert.True(otherVolume.Free < freeBefore);
        }
    }

    [Fact]
    public async Task WhenWriteFilesAndDisposeVolumeThenBitmapIsWritten()
    {
        // arrange - create formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        long expectedFree;

        // act - write files and dispose volume
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < 10; i++)
            {
                await WriteFile(volume, $"File{i}", 5000 * (i + 1));
            }

            expectedFree = volume.Free;
        }

        // assert - root block bitmap flags are valid and bitmap on disk is updated
        Assert.Equal(Constants.BM_VALID, (await ReadRootBlockFromDisk(stream)).BitmapFlags);
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(expectedFree, volume.Free);

            // act - write another file after remount, so blocks used by files written before are not reused
            await WriteFile(volume, "File10", 50000);
            Assert.Equal(11, (await volume.ListEntries()).Count());
        }

        // assert - files are equal after remount
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < 10; i++)
            {
                await using var entryStream = await volume.OpenFile($"File{i}", FileMode.Read);
                var output = new MemoryStream();
                await entryStream.CopyToAsync(output);
                Assert.Equal(Enumerable.Range(0, 5000 * (i + 1)).Select(x => (byte)(x % 256)).ToArray(),
                    output.ToArray());
            }
        }
    }
}
