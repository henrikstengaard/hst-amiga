namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FileMode = FileSystems.FileMode;

public class GivenFastFileSystemEntryStream : FastFileSystemTestBase
{
    private static readonly byte[] Dos0DosType = { 0x44, 0x4f, 0x53, 0x0 };

    private const string FileName = "New File";

    // large enough to use data blocks in file header block and multiple file extension blocks
    private const int FileSize = 200000;

    public static TheoryData<byte[], int> DosTypes => new()
    {
        { Dos0DosType, 512 },
        { Dos3DosType, 512 },
        { Dos3DosType, 1024 }
    };

    private static byte[] CreateData(int size) =>
        Enumerable.Range(0, size).Select(x => (byte)(x * 7 % 251)).ToArray();

    private async Task<Stream> CreateFileAndOpenForRead(byte[] dosType, int fileSystemBlockSize, byte[] data)
    {
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType, fileSystemBlockSize);
        var ffsVolume = await MountVolume(stream);

        await ffsVolume.CreateFile(FileName);
        await using (var entryStream = await ffsVolume.OpenFile(FileName, FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        return await ffsVolume.OpenFile(FileName, FileMode.Read);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenSeekAndReadWithOffsetAndCountThenDataIsReadFromPosition(byte[] dosType,
        int fileSystemBlockSize)
    {
        // arrange - create file with data
        var data = CreateData(FileSize);
        await using var entryStream = await CreateFileAndOpenForRead(dosType, fileSystemBlockSize, data);

        var positions = new[]
        {
            100000, 0, 1, 487, 488, 489, 511, 512, 513, 72 * 488, 72 * 512 - 1, 72 * 512, 72 * 512 + 1,
            144 * 488, 144 * 512, 144 * 1024, 150000, FileSize - 1000, 10
        };

        foreach (var position in positions)
        {
            // act - seek to position
            var newPosition = entryStream.Seek(position, SeekOrigin.Begin);
            Assert.Equal(position, newPosition);
            Assert.Equal(position, entryStream.Position);

            // act - read 1000 bytes into buffer at offset 10
            var buffer = new byte[2000];
            var bytesRead = await entryStream.ReadAsync(buffer, 10, 1000);

            // assert - bytes read matches data at position and rest of buffer is untouched
            Assert.Equal(1000, bytesRead);
            Assert.Equal(position + 1000, entryStream.Position);
            Assert.Equal(data.Skip(position).Take(1000).ToArray(), buffer.Skip(10).Take(1000).ToArray());
            Assert.All(buffer.Take(10), x => Assert.Equal(0, x));
            Assert.All(buffer.Skip(1010), x => Assert.Equal(0, x));
        }
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenSeekFromCurrentAndEndThenPositionIsRelativeToOrigin(byte[] dosType,
        int fileSystemBlockSize)
    {
        // arrange - create file with data
        var data = CreateData(FileSize);
        await using var entryStream = await CreateFileAndOpenForRead(dosType, fileSystemBlockSize, data);
        var buffer = new byte[100];

        // act - seek from end and read
        Assert.Equal(FileSize - 100, entryStream.Seek(-100, SeekOrigin.End));
        Assert.Equal(100, entryStream.Read(buffer, 0, buffer.Length));
        Assert.Equal(data.Skip(FileSize - 100).ToArray(), buffer);

        // act - read at end returns 0 bytes
        Assert.Equal(0, entryStream.Read(buffer, 0, buffer.Length));

        // act - seek backwards from current and read
        Assert.Equal(50000, entryStream.Seek(50000 - FileSize, SeekOrigin.Current));
        Assert.Equal(100, entryStream.Read(buffer, 0, buffer.Length));
        Assert.Equal(data.Skip(50000).Take(100).ToArray(), buffer);

        // act - seek forward from current and read
        Assert.Equal(120000, entryStream.Seek(120000 - 50100, SeekOrigin.Current));
        Assert.Equal(100, entryStream.Read(buffer, 0, buffer.Length));
        Assert.Equal(data.Skip(120000).Take(100).ToArray(), buffer);

        // act - seek before beginning throws exception
        Assert.Throws<IOException>(() => entryStream.Seek(-1, SeekOrigin.Begin));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenSeekBackToBeginningAndCopyThenDataIsEqual(byte[] dosType, int fileSystemBlockSize)
    {
        // arrange - create file with data
        var data = CreateData(FileSize);
        await using var entryStream = await CreateFileAndOpenForRead(dosType, fileSystemBlockSize, data);

        // act - seek into file, then back to beginning and copy
        entryStream.Position = 150000;
        entryStream.Position = 0;
        var output = new MemoryStream();
        await entryStream.CopyToAsync(output, 1000);

        // assert - data read is equal
        Assert.Equal(data, output.ToArray());
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task WhenWriteWithOffsetThenDataIsWrittenFromOffset(byte[] dosType, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType, fileSystemBlockSize);
        await using var ffsVolume = await MountVolume(stream);
        var data = CreateData(FileSize);

        // act - write data in chunks with buffer offset
        await ffsVolume.CreateFile(FileName);
        await using (var entryStream = await ffsVolume.OpenFile(FileName, FileMode.Append))
        {
            var buffer = new byte[1100];
            for (var position = 0; position < data.Length; position += 1000)
            {
                var count = Math.Min(1000, data.Length - position);
                Array.Copy(data, position, buffer, 100, count);
                entryStream.Write(buffer, 100, count);
            }
        }

        // assert - data read is equal
        await using (var entryStream = await ffsVolume.OpenFile(FileName, FileMode.Read))
        {
            var output = new MemoryStream();
            await entryStream.CopyToAsync(output);
            Assert.Equal(data, output.ToArray());
        }
    }
}
