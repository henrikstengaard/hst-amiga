namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConsoleApp.Commands;
using Roms;
using Vm;
using Vm.Libraries;
using Vm.Libraries.Exec;
using Vm.Libraries.Structs;
using Vm.Processor;
using Xunit;

public class GivenKickstartRom
{
    private const uint BaseAddress = 0xF80000;
    private const int RomSize = 512 * 1024;
    private const int ExecOffset = 0x100;
    private const int ScsiOffset = 0x1000;

    private static byte[] CreateRomBytes()
    {
        var bytes = new byte[RomSize];

        // rom header with reset vector and kickstart version 39.106
        WriteUInt16(bytes, 0, 0x1114);
        WriteUInt16(bytes, 2, 0x4EF9);
        WriteUInt32(bytes, 4, BaseAddress + 0xD2);
        WriteUInt16(bytes, 0x0C, 39);
        WriteUInt16(bytes, 0x0E, 106);

        WriteResident(bytes, ExecOffset, 0x800, 39, NodeTypes.Library, 0x200, 0x220);
        WriteString(bytes, 0x200, "exec.library");
        WriteString(bytes, 0x220, "exec 39.47 (28.8.92)\r\n");

        WriteResident(bytes, ScsiOffset, 0x2000, 37, 3, 0x1100, 0x1120);
        WriteString(bytes, 0x1100, "scsi.device");
        WriteString(bytes, 0x1120, "scsidisk 37.64 (14.8.92)\r\n");
        return bytes;
    }

    private static (VmContext Context, ExecLibrary Exec) CreateVm(KickstartRom rom,
        CpuType cpuType = CpuType.M68020)
    {
        var context = new VmContext(new VmOptions { CpuType = cpuType, KickstartRom = rom, Log = _ => { } });
        var exec = new ExecLibrary(context);
        exec.Install();
        context.AddLibrary(exec);
        var version = new VersionLibrary(context, exec);
        version.Install();
        context.AddLibrary(version);
        exec.SetResidentModules(context.Libraries);

        var stack = context.Allocator.Allocate(4096);
        context.Cpu.A[7] = stack + 4096;
        return (context, exec);
    }

    private static uint FindResident(VmContext context, ExecLibrary exec, string name)
    {
        var nameAddress = context.Allocator.Allocate((uint)name.Length + 1);
        context.Ram.WriteCString(nameAddress, name);
        context.Cpu.A[1] = nameAddress;
        return context.CallSubroutine((uint)(exec.BaseAddress + exec.GetOffset("FindResident")));
    }

    [Theory]
    [InlineData(CpuType.M68000)]
    [InlineData(CpuType.M68020)]
    public void When_KickstartRomIsLoaded_Then_RomIsReadableAtBaseAddressAndWriteProtected(CpuType cpuType)
    {
        var (context, _) = CreateVm(KickstartRomReader.Read(CreateRomBytes()), cpuType);

        Assert.Equal(0x11144EF9U, context.Ram.ReadLong(BaseAddress));
        Assert.Equal("scsi.device", context.Ram.ReadCString(BaseAddress + 0x1100));
        Assert.Equal(VmOptions.DefaultMemorySize, context.Ram.Size);
        Assert.Throws<BusErrorException>(() => context.Ram.WriteLong(BaseAddress, 0));
        Assert.Throws<BusErrorException>(() => context.Ram.WriteByte(VmOptions.DefaultMemorySize, 0));

        // 68000 has 24-bit addresses, which wrap around after rom at end of address space
        if (cpuType == CpuType.M68020)
        {
            Assert.Throws<BusErrorException>(() => context.Ram.ReadByte(BaseAddress + RomSize));
        }
    }

    [Fact]
    public void When_FindingResidentModules_Then_RomResidentsAreFoundAtRomAddresses()
    {
        var (context, exec) = CreateVm(KickstartRomReader.Read(CreateRomBytes()));

        var scsi = FindResident(context, exec, "scsi.device");
        var execResident = FindResident(context, exec, "exec.library");
        var version = FindResident(context, exec, "version.library");

        Assert.Equal(BaseAddress + ScsiOffset, scsi);
        Assert.Equal(37, context.Ram.ReadByte(scsi + 11));
        Assert.Equal("scsidisk 37.64 (14.8.92)\r\n", context.Ram.ReadCString(context.Ram.ReadLong(scsi + 18)));
        Assert.Equal(BaseAddress + 0x2000, context.Ram.ReadLong(scsi + 6));
        Assert.Equal(BaseAddress + ExecOffset, execResident);

        // version.library isn't in rom and has resident module created in memory
        Assert.NotEqual(0U, version);
        Assert.True(version < VmOptions.DefaultMemorySize);
        Assert.Equal(0U, FindResident(context, exec, "nosuch.device"));
    }

    [Fact]
    public void When_KickstartRomIsLoaded_Then_ExecBaseTellsKickstartVersionOfRom()
    {
        var (context, exec) = CreateVm(KickstartRomReader.Read(CreateRomBytes()));

        Assert.Equal(39, context.Ram.ReadWord(exec.BaseAddress + LibraryStruct.Version));
        Assert.Equal(47, context.Ram.ReadWord(exec.BaseAddress + LibraryStruct.Revision));
        Assert.Equal(106, context.Ram.ReadWord(exec.BaseAddress + ExecBaseStruct.SoftVer));
        Assert.Equal(0U, exec.OpenLibrary("exec.library", 40));
        Assert.Equal(exec.BaseAddress, exec.OpenLibrary("exec.library", 39));
    }

    [Fact]
    public void When_RomHeaderDoesNotMatchExecVersion_Then_SoftVerIsExecRevision()
    {
        var bytes = CreateRomBytes();
        WriteUInt16(bytes, 0x0C, 0xFFFF);
        var (context, exec) = CreateVm(KickstartRomReader.Read(bytes));

        Assert.Equal(47, context.Ram.ReadWord(exec.BaseAddress + ExecBaseStruct.SoftVer));
    }

    [Fact]
    public void When_MemorySizeOverlapsKickstartRom_Then_ArgumentExceptionIsThrown()
    {
        var options = new VmOptions
        {
            MemorySize = 16 * 1024 * 1024,
            KickstartRom = KickstartRomReader.Read(CreateRomBytes()),
            Log = _ => { }
        };

        Assert.Throws<ArgumentException>(() => new VmContext(options));
    }

    [Fact]
    public async Task When_RunningWithInvalidKickstartRom_Then_VmExceptionIsThrown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hst-amiga-invalid-{Guid.NewGuid():N}.rom");
        await File.WriteAllBytesAsync(path, new byte[1024]);
        try
        {
            var command = new VmRunCommand("program", Array.Empty<string>(),
                new VmOptions { Log = _ => { } }, kickstartPath: path);

            var exception = await Assert.ThrowsAsync<VmException>(() => command.Execute(null, default));

            Assert.Contains("Invalid Kickstart ROM", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteResident(byte[] bytes, int offset, int endOffset, byte version, byte type,
        int nameOffset, int idStringOffset)
    {
        WriteUInt16(bytes, offset, 0x4AFC);
        WriteUInt32(bytes, offset + 2, BaseAddress + (uint)offset);
        WriteUInt32(bytes, offset + 6, BaseAddress + (uint)endOffset);
        bytes[offset + 10] = 0x80;
        bytes[offset + 11] = version;
        bytes[offset + 12] = type;
        WriteUInt32(bytes, offset + 14, BaseAddress + (uint)nameOffset);
        WriteUInt32(bytes, offset + 18, BaseAddress + (uint)idStringOffset);
    }

    private static void WriteString(byte[] bytes, int offset, string value) =>
        Encoding.Latin1.GetBytes(value).CopyTo(bytes, offset);

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
}
