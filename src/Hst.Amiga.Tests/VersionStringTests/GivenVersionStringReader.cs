namespace Hst.Amiga.Tests.VersionStringTests
{
    using System.IO;
    using System.Threading.Tasks;
    using VersionStrings;
    using Xunit;

    public class GivenVersionStringReader
    {
        [Fact]
        public async Task WhenReadVersionFromPfs3AioFileThenVersionMatch()
        {
            await using var pfs3AioStream =
                new MemoryStream(await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio")));
            var version = await VersionStringReader.Read(pfs3AioStream);

            Assert.Equal(
                "$VER: Professional-File-System-III 19.2 PFS3AIO-VERSION (2.10.2018) written by Michiel Pelt and copyright (c) 1994-2012 Peltin BV",
                version);
        }

        [Fact]
        public void WhenParseVersionFromPfs3AioThenFileVersionMatch()
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
        
        [Fact]
        public void WhenParseVersionFromFat95ThenFileVersionMatch()
        {
            var version =
                "$VER: fat95 file system 3.18 (01.03.2013) © Torsten Jager";
            var fileVersion = VersionStringReader.Parse(version);

            Assert.Equal("fat95 file system", fileVersion.Name);
            Assert.Equal(3, fileVersion.Version);
            Assert.Equal(18, fileVersion.Revision);
            Assert.Equal(1, fileVersion.DateDay);
            Assert.Equal(3, fileVersion.DateMonth);
            Assert.Equal(2013, fileVersion.DateYear);
        }
    }
}