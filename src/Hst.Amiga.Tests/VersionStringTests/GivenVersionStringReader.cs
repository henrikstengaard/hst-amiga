namespace Hst.Amiga.Tests.VersionStringTests
{
    using System.IO;
    using System.Threading.Tasks;
    using VersionStrings;
    using Xunit;

    public class GivenVersionStringReader
    {
        [Fact]
        public async Task WhenReadVersionThenVersionMatch()
        {
            await using var pfs3AioStream =
                new MemoryStream(await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio")));
            var version = await VersionStringReader.Read(pfs3AioStream);

            Assert.Equal(
                "$VER: Professional-File-System-III 19.2 PFS3AIO-VERSION (2.10.2018) written by Michiel Pelt and copyright (c) 1994-2012 Peltin BV",
                version);
        }

        [Fact]
        public void WhenParseVersionThenFileVersionMatch()
        {
            var version =
                "$VER: Professional-File-System-III 19.2 PFS3AIO-VERSION (2.10.2018) written by Michiel Pelt and copyright (c) 1994-2012 Peltin BV";
            var fileVersion = VersionStringReader.Parse(version);

            Assert.Equal("Professional-File-System-III", fileVersion.Name);
            Assert.Equal(19, fileVersion.Version);
            Assert.Equal(2, fileVersion.Revision);
            Assert.Equal(2, fileVersion.DateDay);
            Assert.Equal(10, fileVersion.DateMonth);
            Assert.Equal(2018, fileVersion.DateYear);
        }
    }
}