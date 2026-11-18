using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.Hunks;
using Hst.Amiga.Roms;
using Xunit;

namespace Hst.Amiga.Tests.RomTests;

public class GivenKickstartRomReader
{
    private const uint BaseAddress = 0xF80000;

    [Theory]
    [InlineData(0x20005)]
    [InlineData(0x20007)]
    public async Task WhenExtractingWithRemusDataThenCatalogControlsModuleLayoutAndFixes(int version)
    {
        var (rom, catalog) = CreateRemusFixture(version);

        // The image deliberately has no resident tags: the catalog defines the entire module.
        Assert.Equal((8, 16, "test.device"), Assert.Single(RemusRomSplitter.List(rom, catalog)));
        var (offset, name, bytes) = Assert.Single(RemusRomSplitter.Extract(rom, catalog));
        var hunks = (await HunkReader.Read(new MemoryStream(bytes))).ToList();
        var code = Assert.IsType<Code>(hunks[1]).Data;
        Assert.Equal(8, offset);
        Assert.Equal("test.device", name);
        Assert.Equal(16, code.Length);
        Assert.Equal(12U, ReadUInt32(code, 0)); // catalog relocation uses the module start
        Assert.Equal(BaseAddress + 20, ReadUInt32(code, 4)); // equally plausible pointer, not listed
        Assert.Equal(0x11223344U, ReadUInt32(code, 8)); // extraction fix
        Assert.Equal(0U, ReadUInt32(code, 12)); // optional patch skipped
        Assert.Equal(0U, Assert.Single(Assert.Single(Assert.IsType<ReLoc32>(hunks[2]).Entries).Offsets));
    }

    [Fact]
    public void WhenRomChangesWithoutUpdatingItsChecksumThenRemusDataIsRejected()
    {
        var (rom, catalog) = CreateRemusFixture(0x20007);
        rom[0] = 1;

        var error = Assert.Throws<InvalidDataException>(() => RemusRomSplitter.Extract(rom, catalog));

        Assert.Contains("No matching ROM", error.Message);
    }

    [Fact]
    public void WhenReadingRawRomThenResidentModulesAreFound()
    {
        var bytes = CreateRomBytes();

        var rom = KickstartRomReader.Read(bytes);

        Assert.False(rom.IsEncrypted);
        Assert.Equal(BaseAddress, rom.BaseAddress);
        Assert.Equal(2, rom.Modules.Count);
        Assert.Equal("exec.library", rom.Modules[0].Name);
        Assert.Equal("exec 40.10 (01.01.1993)", rom.Modules[0].IdString);
        Assert.Equal(40, rom.Modules[0].Version);
        Assert.Equal(10, rom.Modules[0].Revision);
        Assert.Equal(16, rom.Modules[0].Offset);
        Assert.Equal(64, rom.Modules[0].Size);
        Assert.Equal(bytes.Skip(16).Take(64), rom.GetModuleBytes(rom.Modules[0]));
    }

    [Fact]
    public void WhenReadingCloantoRomThenKeyIsAppliedToPayloadOnly()
    {
        var key = new byte[] { 0xAA, 0x55, 0x11, 0x22, 0x33 };
        var decodedBytes = CreateRomBytes();
        var wrappedBytes = WrapCloanto(decodedBytes, key);

        var rom = KickstartRomReader.Read(wrappedBytes, key);

        Assert.True(rom.IsEncrypted);
        Assert.Equal(decodedBytes, rom.Bytes);
        Assert.Equal(2, rom.Modules.Count);
    }

    [Fact]
    public void WhenReadingKickImageThen512ByteContainerHeaderIsSkipped()
    {
        var image = CreateRomBytes();
        var kickImage = new byte[512 + image.Length];
        Encoding.ASCII.GetBytes("KICK").CopyTo(kickImage, 0);
        image.CopyTo(kickImage, 512);

        var rom = KickstartRomReader.Read(kickImage);

        Assert.Equal(image, rom.Bytes);
        Assert.Equal(2, rom.Modules.Count);
    }

    [Fact]
    public void WhenReadingCloantoRomWithoutKeyThenErrorIsThrown()
    {
        var encryptedBytes = WrapCloanto(CreateRomBytes(), new byte[] { 1, 2, 3 });

        var error = Assert.Throws<InvalidDataException>(() => KickstartRomReader.Read(encryptedBytes));

        Assert.Contains("rom.key", error.Message);
    }

    [Fact]
    public void WhenReadingRomWithIncorrectKeyThenErrorIsThrown()
    {
        var encryptedBytes = WrapCloanto(CreateRomBytes(), new byte[] { 1, 2, 3 });

        Assert.Throws<InvalidDataException>(() => KickstartRomReader.Read(encryptedBytes, new byte[] { 3, 2, 1 }));
    }

    [Fact]
    public void WhenReadingNonKickstartRomThenErrorIsThrown()
    {
        Assert.Throws<InvalidDataException>(() => KickstartRomReader.Read(new byte[512 * 1024]));
    }

    [Fact]
    public async Task WhenWrappingModuleAsHunkThenCodeAndRelocationsAreWritten()
    {
        var rom = KickstartRomReader.Read(CreateRomBytes());
        // The synthetic ROM stores the first module's resident strings after its end pointer.
        // Extend its test slice so all resident relocations target bytes in the Hunk segment.
        rom.Modules[0].Size = 160;
        // A short branch immediately before the resident tag is the module's entry prelude.
        rom.Bytes[0] = 0x60;
        rom.Bytes[1] = 0x00;
        rom.Bytes[2] = 0x00;
        rom.Bytes[3] = 0x0E;

        var hunkBytes = rom.GetModuleHunkBytes(rom.Modules[0]);
        var hunks = (await HunkReader.Read(new MemoryStream(hunkBytes))).ToList();

        Assert.Equal(HunkIdentifiers.Header, hunks[0].Identifier);
        Assert.Equal(HunkIdentifiers.Code, hunks[1].Identifier);
        var code = Assert.IsType<Code>(hunks[1]).Data;
        Assert.Equal(0x60, code[0]); // preserve the branch prelude before the resident tag
        Assert.Equal(16U, ReadUInt32(code, 18)); // rt_MatchTag rebased to the code hunk
        Assert.Equal(80U, ReadUInt32(code, 22)); // rt_EndSkip rebased to the code hunk
        Assert.Equal(120U, ReadUInt32(code, 30)); // rt_Name rebased to the code hunk
        Assert.Equal(136U, ReadUInt32(code, 34)); // rt_IdString rebased to the code hunk
        Assert.Equal(HunkIdentifiers.ReLoc32, hunks[2].Identifier);
        var relocations = Assert.IsType<ReLoc32>(hunks[2]).Entries.Single();
        Assert.Equal(0U, relocations.HunkNumber);
        Assert.Contains(18U, relocations.Offsets); // rt_MatchTag
        Assert.Contains(22U, relocations.Offsets); // rt_EndSkip
        Assert.Contains(30U, relocations.Offsets); // rt_Name
        Assert.Contains(34U, relocations.Offsets); // rt_IdString
        Assert.Equal(HunkIdentifiers.End, hunks[3].Identifier);
    }

    [Fact]
    public async Task WhenExtractingA1200ScsiDeviceThenLibraryReferencesMatchGetScsi()
    {
        var bytes = CreateScsiRomBytes();
        var rom = KickstartRomReader.Read(bytes);
        var hunkBytes = GetScsiCompatibility.Extract(rom);
        var hunks = (await HunkReader.Read(new MemoryStream(hunkBytes))).ToList();
        var code = Assert.IsType<Code>(hunks[1]).Data;

        Assert.Equal(0x29D8, code.Length);
        Assert.Equal("dos.library", Encoding.ASCII.GetString(code, 0x34, 11));
        Assert.Equal("expansion.library", Encoding.ASCII.GetString(code, 0x29B4, 17));
        Assert.Equal(0x6100291AU, ReadUInt32(code, 0x90));
        Assert.Equal(0x61001F6AU, ReadUInt32(code, 0xA40));
        Assert.Equal(0x61001EEEU, ReadUInt32(code, 0xAD8));
        Assert.Equal(0x40U, ReadUInt32(code, 0x42));
        Assert.Equal(0x29ACU, ReadUInt32(code, 0x46));
        var relocations = Assert.IsType<ReLoc32>(hunks[2]).Entries.Single();
        Assert.Equal(new uint[] { 0x42, 0x46, 0x4E, 0x52, 0x56 }, relocations.Offsets);
        Assert.Equal(bytes, rom.Bytes); // extraction must not patch the source ROM
    }

    [Fact]
    public async Task WhenPatchingExtractedScsiHunkThenItMatchesDedicatedExtractionAndIsIdempotent()
    {
        var rom = KickstartRomReader.Read(CreateScsiRomBytes());
        var original = rom.GetModuleHunkBytes(rom.Modules[0]);
        var hunks = (await HunkReader.Read(new MemoryStream(original))).ToList();
        Assert.Equal(0x29AC, Assert.IsType<Code>(hunks[1]).Data.Length);

        var patched = await GetScsiCompatibility.Patch(original);
        Assert.Equal(GetScsiCompatibility.Extract(rom), patched);
        Assert.Same(patched, await GetScsiCompatibility.Patch(patched));
    }

    [Fact]
    public async Task WhenScsiPreludeAndCallOffsetsDifferThenBothCommandsPatchTheDetectedLayout()
    {
        var bytes = CreateScsiRomBytes();
        Array.Clear(bytes, 0x40, 16); // ROM has the DOS helper but lacks the 16-byte branch stub.
        Encoding.ASCII.GetBytes("scsidisk 40.20 (18.2.94)\0").CopyTo(bytes, 0xA6);
        Array.Copy(bytes, 0x80 + 0xA00 - 6, bytes, 0x80 + 0xB30 - 6, 14);
        Array.Clear(bytes, 0x80 + 0xA00 - 6, 14);
        var rom = KickstartRomReader.Read(bytes);

        var patched = GetScsiCompatibility.Extract(rom);
        Assert.Equal(patched, await GetScsiCompatibility.Patch(rom.GetModuleHunkBytes(rom.Modules[0])));
        var hunks = (await HunkReader.Read(new MemoryStream(patched))).ToArray();
        var code = Assert.IsType<Code>(hunks[1]).Data;
        Assert.Equal(0x30U, ReadUInt32(code, 0x32)); // rt_MatchTag uses the shorter prelude
        Assert.Equal(0x61001E3AU, ReadUInt32(code, 0x30 + 0xB30)); // BSR to the appended helper
    }

    [Fact]
    public async Task WhenScsiHasAnExternalRomJumpThenNeitherCommandWritesAStandalonePatchedHunk()
    {
        var bytes = CreateScsiRomBytes();
        WriteUInt16(bytes, 0x80 + 0xD00, 0x4EF9);
        WriteUInt32(bytes, 0x80 + 0xD02, BaseAddress + 0x2A00);
        var rom = KickstartRomReader.Read(bytes);

        Assert.Contains("outside the module", Assert.Throws<InvalidDataException>(() => GetScsiCompatibility.Extract(rom)).Message);
        Assert.Contains("unrelocated", (await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetScsiCompatibility.Patch(rom.GetModuleHunkBytes(rom.Modules[0])))).Message);
    }

    [Theory]
    [InlineData(-1, 4)] // TaggedOpenLibrary would open dos.library instead.
    [InlineData(3, 0)]  // The target vector no longer matches.
    public async Task WhenScsiCallSiteDiffersThenCompatibilityPatchIsRejected(int relativeOffset, byte value)
    {
        var rom = KickstartRomReader.Read(CreateScsiRomBytes());
        rom.Bytes[0x80 + 0x50 + relativeOffset] = value;

        Assert.Throws<InvalidDataException>(() => GetScsiCompatibility.Extract(rom));
        await Assert.ThrowsAsync<InvalidDataException>(() => GetScsiCompatibility.Patch(
            rom.GetModuleHunkBytes(rom.Modules[0])));
    }

    [Fact]
    public async Task WhenWordBranchTargetsBeforeResidentThenItIsNotIncludedAsPrefix()
    {
        var rom = KickstartRomReader.Read(CreateRomBytes());
        rom.Bytes[0] = 0x60;
        WriteUInt16(rom.Bytes, 2, 12); // target 14, not resident offset 16

        var hunks = (await HunkReader.Read(new MemoryStream(rom.GetModuleHunkBytes(rom.Modules[0])))).ToList();

        Assert.Equal(0x4A, Assert.IsType<Code>(hunks[1]).Data[0]);
    }

    [Fact]
    public async Task WhenExtractingRomThenModuleFilesAreWritten()
    {
        var romPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".rom");
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var bytes = CreateRomBytes();

        try
        {
            await File.WriteAllBytesAsync(romPath, bytes);
            var command = new KickstartCommand(romPath, outputDirectory, null, true);

            var result = await command.Execute(CancellationToken.None);

            Assert.True(result.IsSuccess);
            var files = Directory.GetFiles(outputDirectory).OrderBy(x => x).ToArray();
            Assert.Equal(2, files.Length);
            Assert.Equal(bytes.Skip(16).Take(64), await File.ReadAllBytesAsync(files[0]));
        }
        finally
        {
            if (File.Exists(romPath))
            {
                File.Delete(romPath);
            }

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }
        }
    }

    private static (byte[] Rom, byte[] Catalog) CreateRemusFixture(int version)
    {
        var rom = new byte[32];
        WriteUInt32(rom, 8, BaseAddress + 20);
        WriteUInt32(rom, 12, BaseAddress + 20);
        WriteUInt32(rom, 28, 0xFE0FFFD7); // complement of the two pointer values

        var catalog = new byte[0x90];
        uint[] header = { 0x524D5346, (uint)version, 1, 28, 0xFE0FFFD7, 32, 0, BaseAddress, 0 };
        for (var i = 0; i < header.Length; i++)
        {
            WriteUInt32(catalog, i * 4, header[i]);
        }

        var entry = version == 0x20007 ? 40 : 36;
        WriteUInt32(catalog, entry, 0x80); // module name offset
        WriteUInt32(catalog, entry + 4, 8);
        WriteUInt32(catalog, entry + 8, 16);
        WriteUInt32(catalog, entry + 12, 0x50); // extras offset
        WriteUInt32(catalog, entry + 16, uint.MaxValue);
        WriteUInt32(catalog, entry + 20, uint.MaxValue);
        WriteUInt16(catalog, 0x50, 0x85); // short patches, short relocations, fixes
        WriteUInt16(catalog, 0x52, 1);
        WriteUInt16(catalog, 0x54, 12);
        WriteUInt32(catalog, 0x56, 0xFFFFFFFF);
        WriteUInt16(catalog, 0x5A, 1);
        WriteUInt16(catalog, 0x5C, 0);
        WriteUInt16(catalog, 0x5E, 1);
        WriteUInt32(catalog, 0x60, 8);
        WriteUInt32(catalog, 0x64, 0x11223344);
        Encoding.ASCII.GetBytes("test.device\0").CopyTo(catalog, 0x80);
        return (rom, catalog);
    }

    private static byte[] CreateScsiRomBytes()
    {
        var bytes = new byte[0x2B00];
        WriteResident(bytes, 0x80, 0x29EC, 40, 0x9A, 0xA6);
        Encoding.ASCII.GetBytes("scsi.device\0").CopyTo(bytes, 0x9A);
        Encoding.ASCII.GetBytes("scsidisk 40.12 (21.12.93)\0").CopyTo(bytes, 0xA6);
        WriteUInt32(bytes, 0x80 + 22, BaseAddress + 0xC2);
        WriteResident(bytes, 0x29EC, bytes.Length, 40, 0x2A10, 0x2A30);
        Encoding.ASCII.GetBytes("dos.library\0").CopyTo(bytes, 0x2A10);
        Encoding.ASCII.GetBytes("dos 40.3 (01.01.1993)\0").CopyTo(bytes, 0x2A30);
        WriteUInt32(bytes, 0x40, 0x6000200A);
        WriteUInt32(bytes, 0x44, 0x4CDF4C00);
        WriteUInt32(bytes, 0x50, 0x90000000);
        WriteUInt32(bytes, 0x5E, 0x43FA0014);
        WriteUInt32(bytes, 0x62, 0x4EAEFFA0);
        Encoding.ASCII.GetBytes("dos.library\0").CopyTo(bytes, 0x74);
        WriteUInt32(bytes, 0x80 + 0x50 - 8, 0x4EAEFEDA);
        WriteUInt16(bytes, 0x80 + 0x50 - 4, 0x2A00);
        WriteUInt16(bytes, 0x80 + 0x50 - 2, 0x7006);
        WriteUInt32(bytes, 0x80 + 0x50, 0x4EAEFCD6);
        WriteUInt16(bytes, 0x80 + 0x50 + 4, 0x4A80);
        WriteUInt16(bytes, 0x80 + 0xA00 - 6, 0x6600);
        WriteUInt16(bytes, 0x80 + 0xA00 - 2, 0x7006);
        WriteUInt32(bytes, 0x80 + 0xA00, 0x4EAEFCD6);
        WriteUInt32(bytes, 0x80 + 0xA00 + 4, 0x4A806700);
        WriteUInt32(bytes, 0x80 + 0xA98 - 8, 0x226D003C);
        WriteUInt32(bytes, 0x80 + 0xA98 - 4, 0x72012003);
        WriteUInt32(bytes, 0x80 + 0xA98, 0x4EAEFFDC);
        WriteUInt16(bytes, 0x80 + 0xA98 + 4, 0x2F0E);
        WriteUInt32(bytes, 0x80 + 0xA98 + 6, 0x2C6D0024);
        return bytes;
    }

    private static byte[] CreateRomBytes()
    {
        var bytes = new byte[208];
        WriteResident(bytes, 16, 80, 40, 120, 136);
        WriteResident(bytes, 80, 208, 40, 160, 176);
        Encoding.Latin1.GetBytes("exec.library").CopyTo(bytes, 120);
        Encoding.Latin1.GetBytes("exec 40.10 (01.01.1993)").CopyTo(bytes, 136);
        Encoding.Latin1.GetBytes("dos.library").CopyTo(bytes, 160);
        Encoding.Latin1.GetBytes("dos 40.3 (01.01.1993)").CopyTo(bytes, 176);
        return bytes;
    }

    private static void WriteResident(byte[] bytes, int offset, int endOffset, byte version, int nameOffset,
        int idStringOffset)
    {
        WriteUInt16(bytes, offset, 0x4AFC);
        WriteUInt32(bytes, offset + 2, BaseAddress + (uint)offset);
        WriteUInt32(bytes, offset + 6, BaseAddress + (uint)endOffset);
        bytes[offset + 11] = version;
        bytes[offset + 12] = 0;
        WriteUInt32(bytes, offset + 14, BaseAddress + (uint)nameOffset);
        WriteUInt32(bytes, offset + 18, BaseAddress + (uint)idStringOffset);
    }

    private static byte[] WrapCloanto(byte[] bytes, byte[] key)
    {
        var result = new byte[11 + bytes.Length];
        Encoding.ASCII.GetBytes("AMIROMTYPE1").CopyTo(result, 0);
        for (var i = 0; i < bytes.Length; i++)
        {
            result[i + 11] = (byte)(bytes[i] ^ key[i % key.Length]);
        }

        return result;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
}
