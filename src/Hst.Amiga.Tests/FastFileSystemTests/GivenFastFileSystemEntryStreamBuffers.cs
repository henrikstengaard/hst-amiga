namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FileMode = FileSystems.FileMode;

public class GivenFastFileSystemEntryStreamBuffers : FastFileSystemTestBase
{
    private static readonly byte[] Dos0DosType = { 0x44, 0x4f, 0x53, 0x0 };
    private const int FileSize = 200000;

    public static TheoryData<byte[], int, bool> DosTypes => new()
    {
        { Dos0DosType, 512, false },
        { Dos3DosType, 512, false },
        { Dos3DosType, 512, true },
        { Dos3DosType, 1024, true }
    };

    private static byte[] CreateData(int size, int seed = 0) =>
        Enumerable.Range(0, size).Select(x => (byte)((x + seed) * 7 % 251)).ToArray();

    private async Task<Stream> CreateDisk(byte[] dosType, int fileSystemBlockSize, bool fragmented)
    {
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType, fileSystemBlockSize);
        if (!fragmented)
        {
            return stream;
        }

        // create small files and delete every other, so files written fill holes of free blocks
        await using (var volume = await MountVolume(stream))
        {
            var smallData = CreateData(3 * fileSystemBlockSize);
            for (var i = 0; i < 40; i++)
            {
                await volume.CreateFile($"Small{i}");
                await using var entryStream = await volume.OpenFile($"Small{i}", FileMode.Append);
                await entryStream.WriteAsync(smallData, 0, smallData.Length);
            }

            for (var i = 0; i < 40; i += 2)
            {
                await volume.Delete($"Small{i}");
            }
        }

        return stream;
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenReadWithDifferentBufferSizesThenDataIsEqual(byte[] dosType, int fileSystemBlockSize,
        bool fragmented)
    {
        // arrange - create file
        var stream = await CreateDisk(dosType, fileSystemBlockSize, fragmented);
        var data = CreateData(FileSize, 1);
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateFile("File");
            await using var entryStream = await volume.OpenFile("File", FileMode.Append);
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        await using (var volume = await MountVolume(stream))
        {
            foreach (var bufferSize in new[] { 1, 100, 511, 512, 513, 1024, 4096, 70000, 300000 })
            {
                // act - read file with buffer size
                await using var entryStream = await volume.OpenFile("File", FileMode.Read);
                var output = new MemoryStream();
                var buffer = new byte[bufferSize + 10];
                int bytesRead;
                while ((bytesRead = await entryStream.ReadAsync(buffer, 10, bufferSize)) > 0)
                {
                    output.Write(buffer, 10, bytesRead);
                }

                // assert - data read is equal
                Assert.Equal(data, output.ToArray());
            }

            // act - seek and read mixed sizes, so data blocks are read directly into buffer and read ahead
            await using (var entryStream = await volume.OpenFile("File", FileMode.Read))
            {
                foreach (var (position, size) in new[]
                         {
                             (100, 50000), (150000, 30000), (37000, 512), (37512, 2048), (0, FileSize),
                             (FileSize - 1000, 1000), (60001, 80000)
                         })
                {
                    entryStream.Position = position;
                    var buffer = new byte[size];
                    var total = 0;
                    while (total < size)
                    {
                        var n = await entryStream.ReadAsync(buffer, total, size - total);
                        Assert.True(n > 0);
                        total += n;
                    }

                    Assert.Equal(data.Skip(position).Take(size).ToArray(), buffer);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenWriteWithDifferentBufferSizesAndFlushThenDataIsEqual(byte[] dosType,
        int fileSystemBlockSize, bool fragmented)
    {
        // arrange - create disk
        var stream = await CreateDisk(dosType, fileSystemBlockSize, fragmented);
        var data = CreateData(FileSize, 2);
        var sizes = new[] { 1, 511, 512, 513, 7000, 70000, 100 };

        // act - write data with different buffer sizes and flush entry stream in between
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateFile("File");
            await using var entryStream = await volume.OpenFile("File", FileMode.Append);
            var position = 0;
            var i = 0;
            while (position < data.Length)
            {
                var size = Math.Min(sizes[i % sizes.Length], data.Length - position);
                await entryStream.WriteAsync(data, position, size);
                position += size;
                if (i % 3 == 0)
                {
                    await entryStream.FlushAsync();
                }

                i++;
            }
        }

        // assert - data read is equal
        await using (var volume = await MountVolume(stream))
        {
            await using var entryStream = await volume.OpenFile("File", FileMode.Read);
            var output = new MemoryStream();
            await entryStream.CopyToAsync(output);
            Assert.Equal(data, output.ToArray());
        }
    }

    [Fact]
    public async Task WhenWriteAndReadWithSpanAndMemoryThenDataIsEqual()
    {
        // arrange - create disk
        var stream = await CreateFastFileSystemFormattedDisk();
        var data = CreateData(FileSize, 3);

        // act - write data with span and memory
        await using (var volume = await MountVolume(stream))
        {
            await volume.CreateFile("File");
            await using var entryStream = await volume.OpenFile("File", FileMode.Append);
            entryStream.Write(new ReadOnlySpan<byte>(data, 0, 50000));
            await entryStream.WriteAsync(new ReadOnlyMemory<byte>(data, 50000, FileSize - 50000));
        }

        // assert - data read with span and memory is equal
        await using (var volume = await MountVolume(stream))
        {
            await using var entryStream = await volume.OpenFile("File", FileMode.Read);
            var buffer = new byte[FileSize];
            var total = 0;
            while (total < 70000)
            {
                total += entryStream.Read(new Span<byte>(buffer, total, 70000 - total));
            }

            while (total < FileSize)
            {
                total += await entryStream.ReadAsync(new Memory<byte>(buffer, total, FileSize - total));
            }

            Assert.Equal(data, buffer);
        }
    }
}
