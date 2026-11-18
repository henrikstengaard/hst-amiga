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

        var hunkBytes = rom.GetModuleHunkBytes(rom.Modules[0]);
        var hunks = (await HunkReader.Read(new MemoryStream(hunkBytes))).ToList();

        Assert.Equal(HunkIdentifiers.Header, hunks[0].Identifier);
        Assert.Equal(HunkIdentifiers.Code, hunks[1].Identifier);
        var code = Assert.IsType<Code>(hunks[1]).Data;
        Assert.Equal(0U, ReadUInt32(code, 2)); // rt_MatchTag rebased to the code hunk
        Assert.Equal(64U, ReadUInt32(code, 6)); // rt_EndSkip rebased to the code hunk
        Assert.Equal(104U, ReadUInt32(code, 14)); // rt_Name rebased to the code hunk
        Assert.Equal(120U, ReadUInt32(code, 18)); // rt_IdString rebased to the code hunk
        Assert.Equal(HunkIdentifiers.ReLoc32, hunks[2].Identifier);
        var relocations = Assert.IsType<ReLoc32>(hunks[2]).Entries.Single();
        Assert.Equal(0U, relocations.HunkNumber);
        Assert.Contains(2U, relocations.Offsets); // rt_MatchTag
        Assert.Contains(6U, relocations.Offsets); // rt_EndSkip
        Assert.Contains(14U, relocations.Offsets); // rt_Name
        Assert.Contains(18U, relocations.Offsets); // rt_IdString
        Assert.Equal(HunkIdentifiers.End, hunks[3].Identifier);
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
