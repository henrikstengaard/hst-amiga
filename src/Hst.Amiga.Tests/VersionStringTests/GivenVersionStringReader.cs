namespace Hst.Amiga.Tests.VersionStringTests
{
    using System.IO;
    using System.Threading.Tasks;
    using HunkTests;
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

        private const string RomTagIdString = "TestFileSystem 2.15 (03.04.2025) test";

        [Fact]
        public async Task WhenReadVersionFromHunkFileStreamWithoutVersionStringThenRomTagIdStringIsReturned()
        {
            await using var stream = new MemoryStream(
                HunkFileBuilder.CreateRomTagHunkFile("TestFileSystem", $"{RomTagIdString}\r\n"));
            var version = await VersionStringReader.Read(stream);

            Assert.Equal(RomTagIdString, version);
        }

        [Fact]
        public void WhenReadVersionFromHunkFileBytesWithoutVersionStringThenRomTagIdStringIsReturned()
        {
            var data = HunkFileBuilder.CreateRomTagHunkFile("TestFileSystem", $"{RomTagIdString}\r\n");
            var version = VersionStringReader.Read(data);

            Assert.Equal(RomTagIdString, version);
        }

        [Fact]
        public async Task WhenReadVersionFromTruncatedHunkFileWithoutVersionStringThenNullIsReturned()
        {
            var data = HunkFileBuilder.CreateRomTagHunkFile("TestFileSystem", RomTagIdString);
            await using var stream = new MemoryStream(data, 0, 40);

            Assert.Null(await VersionStringReader.Read(stream));
        }

        [Fact]
        public void WhenParseRomTagIdStringFromSmartFilesystemThenFileVersionMatch()
        {
            var fileVersion = VersionStringReader.Parse(
                "SmartFilesystem 1.280 (02.06.2017) AmigaOS m68k (c) Joerg Strohmayer <j_s@gmx.de>");

            Assert.NotNull(fileVersion);
            Assert.Equal("SmartFilesystem", fileVersion.Name);
            Assert.Equal(1, fileVersion.Version);
            Assert.Equal(280, fileVersion.Revision);
            Assert.Equal(2, fileVersion.DateDay);
            Assert.Equal(6, fileVersion.DateMonth);
            Assert.Equal(2017, fileVersion.DateYear);
        }

        [Fact]
        public void WhenReadVersionFromDataWithoutVersionStringOrHunkHeaderThenNullIsReturned()
        {
            Assert.Null(VersionStringReader.Read(new byte[] { 0x4a, 0xfc, 0, 0, 0, 0, 1, 2, 3, 4 }));
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