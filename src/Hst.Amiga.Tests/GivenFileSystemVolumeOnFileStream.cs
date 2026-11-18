namespace Hst.Amiga.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;
using static FileSystemVolumeTestHelper;
using FileMode = FileSystems.FileMode;

public class GivenFileSystemVolumeOnFileStream : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"hst-amiga-{Guid.NewGuid()}.img");

    public static TheoryData<string> FileSystemNames => new() { Ffs, Pfs3, Sfs };

    public void Dispose()
    {
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }

    private static byte[] CreateData(int size, int seed) =>
        Enumerable.Range(0, size).Select(x => (byte)((x + seed) * 7 % 251)).ToArray();

    private FileStream OpenFile() =>
        new(path, System.IO.FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

    [Theory]
    [MemberData(nameof(FileSystemNames))]
    public async Task WhenWriteAndReadFilesThenDataIsEqual(string fileSystem)
    {
        // arrange - create formatted disk in file
        await using (var stream = OpenFile())
        {
            await CreateFormattedDisk(fileSystem, stream);
        }

        // act - write files and directories
        await using (var stream = OpenFile())
        {
            await using var volume = await MountVolume(fileSystem, stream);
            await volume.CreateDirectory("Dir");
            for (var i = 0; i < 20; i++)
            {
                var data = CreateData(10000 * (i + 1), i);
                await volume.CreateFile($"File{i}", false, false);
                await using var entryStream = await volume.OpenFile($"File{i}", FileMode.Append, false, false);
                await entryStream.WriteAsync(data, 0, data.Length);
            }
        }

        // assert - files are equal, when file is opened again
        await using (var stream = OpenFile())
        {
            await using var volume = await MountVolume(fileSystem, stream);
            Assert.Equal(21, (await volume.ListEntries()).Count());
            for (var i = 0; i < 20; i++)
            {
                await using var entryStream = await volume.OpenFile($"File{i}", FileMode.Read, false, false);
                var output = new MemoryStream();
                await entryStream.CopyToAsync(output);
                Assert.Equal(CreateData(10000 * (i + 1), i), output.ToArray());
            }
        }
    }

    [Fact]
    public async Task WhenFileStreamHasBufferedBlocksChangedByVolumeThenFileStreamReadsChangedBlocks()
    {
        // arrange - create formatted ffs disk in file
        await using var stream = OpenFile();
        await CreateFormattedDisk(Ffs, stream);
        var partitionBlock = (await RigidDiskBlockReader.Read(stream)).PartitionBlocks.First();
        var internalVolume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
            partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize);
        var rootBlockOffset = internalVolume.PartitionStartOffset + internalVolume.RootBlockOffset * 512L;

        // act - mount volume, write file and dispose volume
        await using (var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlock))
        {
            // arrange - read root block and following blocks with sequential reads, so file stream buffers
            // blocks and read buffer is fully consumed
            var before = new byte[512];
            stream.Seek(rootBlockOffset, SeekOrigin.Begin);
            for (var i = 0; i < 8; i++)
            {
                Assert.Equal(before.Length, await stream.ReadAsync(before, 0, before.Length));
            }

            for (var i = 0; i < 10; i++)
            {
                await volume.CreateFile($"File{i}");
            }
        }

        // assert - root block read with file stream is equal to root block in file
        var buffer = new byte[512];
        stream.Seek(rootBlockOffset, SeekOrigin.Begin);
        Assert.Equal(buffer.Length, await stream.ReadAsync(buffer, 0, buffer.Length));

        var expected = new byte[buffer.Length];
        await using (var otherStream = OpenFile())
        {
            otherStream.Seek(rootBlockOffset, SeekOrigin.Begin);
            Assert.Equal(expected.Length, await otherStream.ReadAsync(expected, 0, expected.Length));
        }

        Assert.Equal(expected, buffer);

        // assert - volume mounted again with file stream lists files
        await using (var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlock))
        {
            Assert.Equal(10, (await volume.ListEntries()).Count());
        }
    }
}
