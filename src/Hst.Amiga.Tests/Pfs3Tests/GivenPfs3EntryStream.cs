namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FileMode = FileSystems.FileMode;

public class GivenPfs3EntryStream : Pfs3TestBase
{
    private const string FileName = "New File";
    private const int FileSize = 200000;

    private static byte[] CreateData(int size) =>
        Enumerable.Range(0, size).Select(x => (byte)(x * 7 % 251)).ToArray();

    [Fact]
    public async Task WhenSeekAndReadWithOffsetAndCountThenDataIsReadFromPosition()
    {
        // arrange - create pfs3 formatted disk and file with data
        var stream = await CreatePfs3FormattedDisk();
        await using var pfs3Volume = await MountVolume(stream);
        var data = CreateData(FileSize);
        await pfs3Volume.CreateFile(FileName);
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Read))
        {
            foreach (var position in new[] { 100000, 0, 1, 511, 512, 513, 150000, FileSize - 1000, 10 })
            {
                // act - seek to position
                Assert.Equal(position, entryStream.Seek(position, SeekOrigin.Begin));
                Assert.Equal(position, entryStream.Position);

                // act - read 1000 bytes into buffer at offset 10
                var buffer = new byte[2000];
                var bytesRead = await entryStream.ReadAsync(buffer, 10, 1000);

                // assert - only count bytes are read at offset
                Assert.Equal(1000, bytesRead);
                Assert.Equal(position + 1000, entryStream.Position);
                Assert.Equal(data.Skip(position).Take(1000).ToArray(), buffer.Skip(10).Take(1000).ToArray());
                Assert.All(buffer.Take(10), x => Assert.Equal(0, x));
                Assert.All(buffer.Skip(1010), x => Assert.Equal(0, x));
            }

            // act - seek relative to end and current
            Assert.Equal(FileSize - 100, entryStream.Seek(-100, SeekOrigin.End));
            Assert.Equal(FileSize - 50, entryStream.Seek(50, SeekOrigin.Current));
            var endBuffer = new byte[100];
            Assert.Equal(50, entryStream.Read(endBuffer, 0, endBuffer.Length));
            Assert.Equal(data.Skip(FileSize - 50).ToArray(), endBuffer.Take(50).ToArray());

            // act - seek past end throws exception
            Assert.Throws<IOException>(() => entryStream.Seek(FileSize + 1, SeekOrigin.Begin));
        }
    }

    [Theory]
    [InlineData(1000, 3000)]
    [InlineData(1024, 2048)]
    [InlineData(1500, 100)]
    [InlineData(100000, 50000)]
    public async Task WhenSeekAndOverwriteDataInExistingFileThenDataIsOverwritten(int position, int count)
    {
        // arrange - create pfs3 formatted disk and file with data
        var stream = await CreatePfs3FormattedDisk();
        await using var pfs3Volume = await MountVolume(stream);
        var data = CreateData(FileSize);
        await pfs3Volume.CreateFile(FileName);
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - seek and overwrite data
        var overwriteData = Enumerable.Range(0, count + 10).Select(x => (byte)(x % 13 + 1)).ToArray();
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Append))
        {
            entryStream.Seek(position, SeekOrigin.Begin);
            await entryStream.WriteAsync(overwriteData, 10, count);
        }

        // assert - data read has overwritten data at position
        var expectedData = data.ToArray();
        Array.Copy(overwriteData, 10, expectedData, position, count);
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Read))
        {
            var all = new byte[FileSize];
            Assert.Equal(FileSize, await entryStream.ReadAsync(all, 0, FileSize));
            Assert.Equal(expectedData, all);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task WhenWriteWithOffsetThenDataIsWrittenFromOffset(int offset)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        await using var pfs3Volume = await MountVolume(stream);
        var data = CreateData(FileSize);

        // act - write data in chunks with buffer offset
        await pfs3Volume.CreateFile(FileName);
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Append))
        {
            var buffer = new byte[offset + 1000];
            for (var position = 0; position < data.Length; position += 1000)
            {
                var count = Math.Min(1000, data.Length - position);
                Array.Copy(data, position, buffer, offset, count);
                entryStream.Write(buffer, offset, count);
            }
        }

        // assert - data read is equal
        await using (var entryStream = await pfs3Volume.OpenFile(FileName, FileMode.Read))
        {
            var all = new byte[FileSize];
            var read = await entryStream.ReadAsync(all, 0, FileSize);
            Assert.Equal(FileSize, read);
            Assert.Equal(data, all);
        }
    }
}
