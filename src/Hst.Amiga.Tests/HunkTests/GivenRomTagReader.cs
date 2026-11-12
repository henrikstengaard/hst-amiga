namespace Hst.Amiga.Tests.HunkTests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DataTypes.Hunks;
    using Xunit;

    public class GivenRomTagReader
    {
        [Fact]
        public async Task WhenReadingHunksWithRomTagThenRomTagIsRead()
        {
            // arrange
            var data = HunkFileBuilder.CreateRomTagHunkFile("TestFileSystem",
                "TestFileSystem 2.15 (03.04.2025) test\r\n", version: 2);
            var hunks = await HunkReader.Read(new MemoryStream(data));

            // act
            var romTag = RomTagReader.Read(hunks);

            // assert
            Assert.NotNull(romTag);
            Assert.Equal(0, romTag.HunkNumber);
            Assert.Equal(0x10U, romTag.Offset);
            Assert.Equal(1, romTag.Flags);
            Assert.Equal(2, romTag.Version);
            Assert.Equal(0, romTag.Type);
            Assert.Equal(0, romTag.Priority);
            Assert.Equal("TestFileSystem", romTag.Name);
            Assert.Equal("TestFileSystem 2.15 (03.04.2025) test", romTag.IdString);
        }

        [Fact]
        public async Task WhenReadingRomTagInSecondHunkWithStringsInOtherHunksThenRomTagIsRead()
        {
            // arrange
            var code = new byte[64];
            HunkFileBuilder.WriteString(code, 0, "TestName");

            var data = new byte[64];
            HunkFileBuilder.WriteRomTag(data, 8, 0x81, 3, 9, -5, 0, 4);

            var strings = new byte[32];
            HunkFileBuilder.WriteString(strings, 4, "TestName 3.1 (01.02.2003)");

            var hunkFile = new HunkFileBuilder()
                .AddCode(code)
                .AddData(data, 0, HunkFileBuilder.RomTagRelocations(8, 1, 0, 3))
                .AddBss(128)
                .AddData(strings)
                .Build();
            var hunks = await HunkReader.Read(new MemoryStream(hunkFile));

            // act
            var romTag = RomTagReader.Read(hunks);

            // assert
            Assert.NotNull(romTag);
            Assert.Equal(1, romTag.HunkNumber);
            Assert.Equal(8U, romTag.Offset);
            Assert.Equal(0x81, romTag.Flags);
            Assert.Equal(3, romTag.Version);
            Assert.Equal(9, romTag.Type);
            Assert.Equal(-5, romTag.Priority);
            Assert.Equal("TestName", romTag.Name);
            Assert.Equal("TestName 3.1 (01.02.2003)", romTag.IdString);
        }

        [Fact]
        public async Task WhenReadingHunksWithMultipleRomTagsThenAllRomTagsAreRead()
        {
            // arrange
            var code = new byte[128];
            HunkFileBuilder.WriteString(code, 64, "First");
            HunkFileBuilder.WriteString(code, 80, "Second");
            HunkFileBuilder.WriteRomTag(code, 0, 0, 1, 0, 0, 64, 64);
            HunkFileBuilder.WriteRomTag(code, 32, 0, 2, 0, 0, 80, 80);

            var hunkFile = new HunkFileBuilder()
                .AddCode(code, 0, HunkFileBuilder.RomTagRelocations(0, 0, 0, 0)
                    .Concat(HunkFileBuilder.RomTagRelocations(32, 0, 0, 0)).ToArray())
                .Build();
            var hunks = await HunkReader.Read(new MemoryStream(hunkFile));

            // act
            var romTags = RomTagReader.ReadAll(hunks).ToList();

            // assert
            Assert.Equal(new[] { "First", "Second" }, romTags.Select(x => x.Name));
            Assert.Equal(new byte[] { 1, 2 }, romTags.Select(x => x.Version));
            Assert.Equal("First", RomTagReader.Read(hunks).IdString);
        }

        [Fact]
        public async Task WhenReadingRomTagWithIdStringNotRelocatedThenIdStringIsNull()
        {
            // arrange
            var code = new byte[64];
            HunkFileBuilder.WriteString(code, 32, "TestName");
            HunkFileBuilder.WriteRomTag(code, 0, 0, 1, 0, 0, 32, 0);

            var hunkFile = new HunkFileBuilder()
                .AddCode(code, 0, HunkFileBuilder.RomTagRelocations(0, 0, 0, 0).Take(3).ToArray())
                .Build();
            var hunks = await HunkReader.Read(new MemoryStream(hunkFile));

            // act
            var romTag = RomTagReader.Read(hunks);

            // assert
            Assert.NotNull(romTag);
            Assert.Equal("TestName", romTag.Name);
            Assert.Null(romTag.IdString);
        }

        [Fact]
        public async Task WhenReadingMatchWordWithoutMatchTagRelocationThenNoRomTagIsRead()
        {
            // arrange
            var code = new byte[64];
            HunkFileBuilder.WriteString(code, 32, "TestName");
            HunkFileBuilder.WriteRomTag(code, 0, 0, 1, 0, 0, 32, 32);

            // relocations for all pointers except rt_MatchTag
            var hunkFile = new HunkFileBuilder()
                .AddCode(code, 0, HunkFileBuilder.RomTagRelocations(0, 0, 0, 0).Skip(1).ToArray())
                .Build();
            var hunks = await HunkReader.Read(new MemoryStream(hunkFile));

            // act & assert
            Assert.Empty(RomTagReader.ReadAll(hunks));
        }

        [Fact]
        public void WhenReadingHunksWithMatchWordNotPointingToItselfThenNoRomTagIsRead()
        {
            // arrange
            var data = new byte[32];
            data[4] = 0x4a;
            data[5] = 0xfc;
            data[9] = 0x10; // rt_MatchTag points to offset 0x10 instead of 0x4
            var hunks = new IHunk[]
            {
                new Code { Data = data },
                new ReLoc32 { Entries = new[] { new ReLoc32Entry { HunkNumber = 0, Offsets = new[] { 6U } } } },
                new End()
            };

            // act & assert
            Assert.Empty(RomTagReader.ReadAll(hunks));
        }
    }
}
