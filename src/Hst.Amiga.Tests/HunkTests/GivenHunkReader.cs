using Hst.Amiga.DataTypes.Hunks;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Amiga.Tests.HunkTests
{
    public class GivenHunkReader
    {
        [Fact]
        public async Task When_ReadingPfs3AioFileSystem_Then_HunksAreRead()
        {
            // arrange
            var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
            var memoryStream = new MemoryStream(await File.ReadAllBytesAsync(pfs3AioPath));

            // act
            var hunks = (await HunkReader.Read(memoryStream)).ToList();

            // assert
            Assert.Equal(4, hunks.Count);
            Assert.Equal(new[] { HunkIdentifiers.Header, HunkIdentifiers.Code, HunkIdentifiers.ReLoc32, HunkIdentifiers.End },
                hunks.Select(hunk => hunk.Identifier));
        }

        [Fact]
        public async Task When_ReadingCodeHunkWithReLoc32_Then_ReLoc32EntriesAreGroupedByHunkNumber()
        {
            // arrange
            var data = new HunkFileBuilder()
                .AddCode(new byte[16], 0, (4, 0), (8, 1), (12, 0))
                .AddData(new byte[8])
                .Build();
            var memoryStream = new MemoryStream(data);

            // act
            var hunks = (await HunkReader.Read(memoryStream)).ToList();

            // assert
            Assert.Equal(memoryStream.Length, memoryStream.Position);
            var reLoc32 = Assert.IsType<ReLoc32>(hunks[2]);
            var entries = reLoc32.Entries.ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(0U, entries[0].HunkNumber);
            Assert.Equal(new[] { 4U, 12U }, entries[0].Offsets);
            Assert.Equal(1U, entries[1].HunkNumber);
            Assert.Equal(new[] { 8U }, entries[1].Offsets);
            Assert.Equal(new[] { 0U, 4U, 12U, 1U, 8U }, reLoc32.Offsets);
        }

        [Fact]
        public async Task When_ReadingMultipleHunks_Then_AllHunksAreRead()
        {
            // arrange
            var data = new HunkFileBuilder()
                .AddCode(new byte[] { 0x4e, 0x75, 0, 0 })
                .AddData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
                .AddBss(64)
                .Build();
            var memoryStream = new MemoryStream(data);

            // act
            var hunks = (await HunkReader.Read(memoryStream)).ToList();

            // assert
            Assert.Equal(new[]
                {
                    HunkIdentifiers.Header,
                    HunkIdentifiers.Code, HunkIdentifiers.End,
                    HunkIdentifiers.Data, HunkIdentifiers.End,
                    HunkIdentifiers.Bss, HunkIdentifiers.End
                },
                hunks.Select(hunk => hunk.Identifier));
            Assert.Equal(new byte[] { 0x4e, 0x75, 0, 0 }, Assert.IsType<Code>(hunks[1]).Data);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, Assert.IsType<Data>(hunks[3]).Bytes);
            Assert.Equal(64U, Assert.IsType<Bss>(hunks[5]).Size);
        }

        [Fact]
        public async Task When_ReadingHunksWithMemoryFlags_Then_HunksAreRead()
        {
            // arrange
            var data = new HunkFileBuilder()
                .AddCode(new byte[8], HunkFileBuilder.MemoryChip)
                .AddData(new byte[4], HunkFileBuilder.MemoryExtended)
                .Build();
            var memoryStream = new MemoryStream(data);

            // act
            var hunks = (await HunkReader.Read(memoryStream)).ToList();

            // assert
            Assert.Equal(memoryStream.Length, memoryStream.Position);
            var header = Assert.IsType<Header>(hunks[0]);
            Assert.Equal(new[] { 2U | HunkFileBuilder.MemoryChip, 1U | HunkFileBuilder.MemoryExtended },
                header.HunkSizes);
            Assert.Equal(8, Assert.IsType<Code>(hunks[1]).Data.Length);
            Assert.Equal(4, Assert.IsType<Data>(hunks[3]).Bytes.Length);
        }

        [Fact]
        public async Task When_ReadingTruncatedHunkFile_Then_ExceptionIsThrown()
        {
            // arrange
            var data = new HunkFileBuilder()
                .AddCode(new byte[1024])
                .Build();
            var memoryStream = new MemoryStream(data, 0, 100);

            // act & assert
            await Assert.ThrowsAsync<IOException>(async () => await HunkReader.Read(memoryStream));
        }

        [Fact]
        public async Task When_ReadingPngImage_Then_ExceptionIsThrown()
        {
            // arrange
            var pngPath = Path.Combine("TestData", "DiskObjects", "bubble_bobble1.png");
            var memoryStream = new MemoryStream(await File.ReadAllBytesAsync(pngPath));

            // act & assert
            await Assert.ThrowsAsync<IOException>(async () => await HunkReader.Read(memoryStream));
        }
    }
}
