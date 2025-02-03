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
