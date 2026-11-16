namespace Hst.Amiga.Tests.VmTests;

using System;
using System.Collections.Generic;
using System.IO;
using Vm;
using Vm.FileSystem;
using Vm.Libraries.Dos;
using Vm.Libraries.Exec;
using Vm.Libraries.Structs;
using Xunit;

public class GivenDosLibraryPatterns : IDisposable
{
    private const int DosTrue = -1;
    private const byte ApfDoDir = 1 << 2;
    private const byte ApfDidDir = 1 << 3;
    private const int BufferSize = 256;

    private readonly string sysPath;
    private readonly VmContext context;
    private readonly DosLibrary dos;

    public GivenDosLibraryPatterns()
    {
        sysPath = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-patterns-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(sysPath, "C"));
        File.WriteAllText(Path.Combine(sysPath, "C", "List"), "list");
        File.WriteAllText(Path.Combine(sysPath, "Disk.info"), "info");

        context = new VmContext(new VmOptions { Log = _ => { } });
        var exec = new ExecLibrary(context);
        exec.Install();
        context.AddLibrary(exec);

        var volumes = new VolumeManager();
        volumes.AddVolume("SYS", sysPath);
        dos = new DosLibrary(context, exec, volumes);
        dos.Install();
        context.AddLibrary(dos);

        var stack = context.Allocator.Allocate(4096);
        context.Cpu.A[7] = stack + 4096;
        dos.SetupProcess(new ProcessSetup
        {
            ProgramName = "test",
            Arguments = "\n",
            StackLower = stack,
            StackUpper = stack + 4096,
            StackSize = 4096,
            CurrentDirectory = volumes.Resolve("SYS:", null),
            Input = new MemoryStream(),
            Output = new MemoryStream(),
            Error = new MemoryStream()
        });
    }

    public void Dispose()
    {
        Directory.Delete(sysPath, true);
    }

    private uint Call(string function, uint d1 = 0, uint d2 = 0)
    {
        context.Cpu.D[1] = d1;
        context.Cpu.D[2] = d2;
        return context.CallSubroutine((uint)(dos.BaseAddress + dos.GetOffset(function)));
    }

    private uint String(string value)
    {
        var bytes = Ram.GetBytes(value);
        var address = context.Allocator.Allocate((uint)bytes.Length + 1);
        context.Ram.WriteBytes(address, bytes);
        return address;
    }

    private uint CreateAnchor()
    {
        var anchor = context.Allocator.Allocate(AnchorPathStruct.Size + BufferSize);
        context.Ram.WriteBytes(anchor, new byte[AnchorPathStruct.Size + BufferSize]);
        context.Ram.WriteWord(anchor + AnchorPathStruct.Strlen, BufferSize);
        return anchor;
    }

    private string MatchedName(uint anchor) => context.Ram.ReadCString(anchor + AnchorPathStruct.Buf);

    /// <summary>
    /// Match all entries like list does, entering each matched directory with do dir flag.
    /// </summary>
    private List<string> MatchAll(string pattern)
    {
        var anchor = CreateAnchor();
        var names = new List<string>();
        var error = (int)Call("MatchFirst", String(pattern), anchor);
        while (error == 0)
        {
            var flags = context.Ram.ReadByte(anchor + AnchorPathStruct.Flags);
            var isDirectory = (int)context.Ram.ReadLong(anchor + AnchorPathStruct.Info +
                                                        FileInfoBlockStruct.DirEntryType) > 0;
            if ((flags & ApfDidDir) == 0)
            {
                names.Add(MatchedName(anchor));
                if (isDirectory)
                {
                    context.Ram.WriteByte(anchor + AnchorPathStruct.Flags, (byte)(flags | ApfDoDir));
                }
            }

            error = (int)Call("MatchNext", anchor);
        }

        Call("MatchEnd", anchor);
        Assert.Equal(DosErrors.NoMoreEntries, error);
        return names;
    }

    [Fact]
    public void When_MatchingVolumeRootAndEnteringDirectories_Then_AllEntriesAreMatched()
    {
        var names = MatchAll("sys:");

        Assert.Equal(new[] { "sys:", "sys:C", "sys:C/List", "sys:Disk.info" }, names);
    }

    [Fact]
    public void When_MatchingRelativeWildcard_Then_EntriesInCurrentDirectoryAreMatched()
    {
        var names = MatchAll("#?.info");

        Assert.Equal(new[] { "Disk.info" }, names);
    }

    [Theory]
    [InlineData("", DosTrue)]
    [InlineData("#?.info", DosTrue)]
    [InlineData("sys:", DosTrue)]
    [InlineData(":c", DosTrue)]
    [InlineData("unknown:", 0)]
    public void When_CheckingIsFileSystem_Then_RelativeNamesAndKnownVolumesAreFileSystems(string name,
        int expected)
    {
        Assert.Equal(unchecked((uint)expected), Call("IsFileSystem", String(name)));
    }
}
