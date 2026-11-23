namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HunkTests;
using Vm;
using Vm.Libraries.Dos;
using Xunit;
using static M68kAssembler;

public class GivenDosLibraryAssigns : IDisposable
{
    private const int OpenLibrary = -552;
    private const uint SharedLock = 0xFFFFFFFE;
    private const int LdfDevices = 4;
    private const int LdfVolumes = 8;
    private const int LdfAssigns = 16;
    private const int LdfAll = LdfDevices | LdfVolumes | LdfAssigns;
    private const int LdfRead = 1;
    private const int LdfWrite = 2;
    private const int DltLate = 3;

    private readonly string sysPath;

    public GivenDosLibraryAssigns()
    {
        sysPath = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-assigns-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(sysPath, "Work"));
        Directory.CreateDirectory(Path.Combine(sysPath, "A"));
        Directory.CreateDirectory(Path.Combine(sysPath, "B"));
        File.WriteAllText(Path.Combine(sysPath, "Work", "file"), "work");
        File.WriteAllText(Path.Combine(sysPath, "B", "fileB"), "b");
    }

    public void Dispose()
    {
        Directory.Delete(sysPath, true);
    }

    private static int Lvo(string function) => -(30 + 6 * Array.IndexOf(DosFunctionNames.Names, function));

    private static M68kAssembler DosProgram() =>
        new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("dosname"), 1)
            .Moveq(36, 0)
            .CallLib(OpenLibrary)
            .Move(Size.Long, D(0), A(6));

    private async Task<(VmResult Result, string Output)> Run(M68kAssembler program,
        Action<VmOptions> configure = null)
    {
        program.Label("dosname").String("dos.library");
        var executable = new HunkFileBuilder().AddCode(program.ToArray()).Build();
        var output = new MemoryStream();
        var options = new VmOptions
        {
            Input = new MemoryStream(),
            Output = output,
            Error = new MemoryStream(),
            Interactive = false,
            MaxInstructions = 10_000_000,
            Log = _ => { }
        };
        options.Volumes["SYS"] = sysPath;
        configure?.Invoke(options);

        var result = await new AmigaVm(options).Run(executable, "test", string.Empty);
        return (result, Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public async Task When_AssigningLockAndRemovingAssign_Then_AssignResolvesUntilRemoved()
    {
        var program = DosProgram()
            .LeaD("dir", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .Move(Size.Long, D(0), D(2)).LeaD("name", 1).CallLib(Lvo("AssignLock"))
            .PrintLockName("file")
            .LeaD("name", 1).Moveq(0, 2).CallLib(Lvo("AssignLock"))
            .LeaD("file", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .CallLib(Lvo("IoErr"))
            .Rts()
            .Label("dir").String("SYS:Work")
            .Label("name").String("W")
            .Label("file").String("W:file")
            .Label("buffer").Bytes(new byte[260]);

        var (result, output) = await Run(program);

        Assert.Null(result.Error);
        Assert.Equal("SYS:Work/file", output);
        Assert.Equal(DosErrors.DeviceNotMounted, result.ReturnCode);
    }

    [Fact]
    public async Task When_AddingDirectoryToAssign_Then_MultiAssignResolvesAndDeviceProcIteratesDirectories()
    {
        var program = DosProgram()
            .LeaD("dirA", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .Move(Size.Long, D(0), D(2)).LeaD("name", 1).CallLib(Lvo("AssignLock"))
            .LeaD("dirB", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .Move(Size.Long, D(0), D(2)).LeaD("name", 1).CallLib(Lvo("AssignAdd"))
            .PrintLockName("file")
            // count device procs returned for multi assign
            .Moveq(0, 4)
            .Moveq(0, 2)
            .Label("loop")
            .LeaD("device", 1).CallLib(Lvo("GetDeviceProc"))
            .Tst(Size.Long, D(0))
            .Beq("done")
            .Move(Size.Long, D(0), D(2))
            .Addq(Size.Long, 1, D(4))
            .Bra("loop")
            .Label("done")
            .Move(Size.Long, D(4), D(0))
            .Rts()
            .Label("dirA").String("SYS:A")
            .Label("dirB").String("SYS:B")
            .Label("name").String("M")
            .Label("device").String("M:")
            .Label("file").String("M:fileB")
            .Label("buffer").Bytes(new byte[260]);

        var (result, output) = await Run(program);

        Assert.Null(result.Error);
        Assert.Equal("SYS:B/fileB", output);
        Assert.Equal(2, result.ReturnCode);
    }

    [Fact]
    public async Task When_ListingDosList_Then_VolumesAndAssignsAreListed()
    {
        var program = DosProgram()
            .LeaD("lname", 1).LeaD("lpath", 2).CallLib(Lvo("AssignLate"))
            .Move(Size.Long, Imm(LdfRead | LdfAll), D(1)).CallLib(Lvo("LockDosList"))
            .Move(Size.Long, D(0), D(3))
            .Label("loop")
            .Move(Size.Long, D(3), D(1)).Moveq(LdfAll, 2).CallLib(Lvo("NextDosEntry"))
            .Tst(Size.Long, D(0))
            .Beq("done")
            .Move(Size.Long, D(0), D(3))
            // print zero terminated BCPL name skipping length byte
            .Move(Size.Long, D(3), A(0))
            .Move(Size.Long, Disp(40, 0), D(1))
            .Add(Size.Long, D(1), 1)
            .Add(Size.Long, D(1), 1)
            .Addq(Size.Long, 1, D(1))
            .CallLib(Lvo("PutStr"))
            .LeaD("newline", 1).CallLib(Lvo("PutStr"))
            .Bra("loop")
            .Label("done")
            .Move(Size.Long, Imm(LdfRead | LdfAll), D(1)).CallLib(Lvo("UnLockDosList"))
            // return type of late assign
            .Move(Size.Long, Imm(LdfRead | LdfAssigns), D(1)).CallLib(Lvo("LockDosList"))
            .Move(Size.Long, D(0), D(1)).LeaD("lname", 2).Moveq(LdfAssigns, 3).CallLib(Lvo("FindDosEntry"))
            .Move(Size.Long, D(0), A(0))
            .Move(Size.Long, Disp(4, 0), D(0))
            .Rts()
            .Label("lname").String("LT")
            .Label("lpath").String("SYS:Later")
            .Label("newline").String("\n");

        var (result, output) = await Run(program, options => options.Assigns["W"] = "SYS:Work");

        Assert.Null(result.Error);
        var names = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("SYS", names);
        Assert.Contains("W", names);
        Assert.Contains("LT", names);
        Assert.Contains("C", names);
        Assert.Equal(DltLate, result.ReturnCode);
    }

    [Fact]
    public async Task When_RemovingDosEntry_Then_AssignIsRemovedAndEntryIsOwnedByProgram()
    {
        var program = DosProgram()
            .Move(Size.Long, Imm(LdfWrite | LdfAssigns), D(1)).CallLib(Lvo("LockDosList"))
            .Move(Size.Long, D(0), D(1)).LeaD("name", 2).Moveq(LdfAssigns, 3).CallLib(Lvo("FindDosEntry"))
            .Move(Size.Long, D(0), D(4))
            .Move(Size.Long, D(4), D(1)).CallLib(Lvo("RemDosEntry"))
            .Move(Size.Long, Imm(LdfWrite | LdfAssigns), D(1)).CallLib(Lvo("UnLockDosList"))
            // lock of removed entry is still valid
            .Move(Size.Long, D(4), A(0))
            .Move(Size.Long, Disp(12, 0), D(5))
            .Move(Size.Long, D(5), D(1)).LeaD("buffer", 2).Move(Size.Long, Imm(256), D(3))
            .CallLib(Lvo("NameFromLock"))
            .LeaD("buffer", 1).CallLib(Lvo("PutStr"))
            .Move(Size.Long, D(5), D(1)).CallLib(Lvo("UnLock"))
            .Move(Size.Long, D(4), D(1)).CallLib(Lvo("FreeDosEntry"))
            .LeaD("file", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .CallLib(Lvo("IoErr"))
            .Rts()
            .Label("name").String("W")
            .Label("file").String("W:file")
            .Label("buffer").Bytes(new byte[260]);

        var (result, output) = await Run(program, options => options.Assigns["W"] = "SYS:Work");

        Assert.Null(result.Error);
        Assert.Equal("SYS:Work", output);
        Assert.Equal(DosErrors.DeviceNotMounted, result.ReturnCode);
    }

    [Fact]
    public async Task When_LockingThroughAssign_Then_LockVolumeIsVolumeEntryInDosList()
    {
        // fl_Volume is used by Assign to show directories, e.g. as "Volume: ???" if it's zero
        var program = DosProgram()
            .LeaD("file", 1).Move(Size.Long, Imm(SharedLock), D(2)).CallLib(Lvo("Lock"))
            .Add(Size.Long, D(0), 0)
            .Add(Size.Long, D(0), 0)
            .Move(Size.Long, D(0), A(0))
            .Move(Size.Long, Disp(16, 0), D(0)) // fl_Volume
            .Add(Size.Long, D(0), 0)
            .Add(Size.Long, D(0), 0)
            .Move(Size.Long, D(0), A(0))
            .Move(Size.Long, Disp(4, 0), D(4)) // dol_Type
            .Move(Size.Long, Disp(40, 0), D(1)) // dol_Name
            .Add(Size.Long, D(1), 1)
            .Add(Size.Long, D(1), 1)
            .Addq(Size.Long, 1, D(1))
            .CallLib(Lvo("PutStr"))
            .Move(Size.Long, D(4), D(0))
            .Rts()
            .Label("file").String("W:file");

        var (result, output) = await Run(program, options => options.Assigns["W"] = "SYS:Work");

        Assert.Null(result.Error);
        Assert.Equal("SYS", output);
        Assert.Equal(2, result.ReturnCode); // DLT_VOLUME
    }

    [Theory]
    [InlineData("+SYS:Work", "SYS:Work/file", 2)]
    [InlineData("SYS:Work", "SYS:Work/file", 1)]
    public async Task When_AssignInOptionsStartsWithPlus_Then_PathIsAddedToDefaultAssign(string assign,
        string expectedName, int expectedDirectories)
    {
        Directory.CreateDirectory(Path.Combine(sysPath, "C"));
        var program = DosProgram()
            .PrintLockName("file")
            .Moveq(0, 4)
            .Moveq(0, 2)
            .Label("loop")
            .LeaD("device", 1).CallLib(Lvo("GetDeviceProc"))
            .Tst(Size.Long, D(0))
            .Beq("done")
            .Move(Size.Long, D(0), D(2))
            .Addq(Size.Long, 1, D(4))
            .Bra("loop")
            .Label("done")
            .Move(Size.Long, D(4), D(0))
            .Rts()
            .Label("device").String("C:")
            .Label("file").String("C:file")
            .Label("buffer").Bytes(new byte[260]);

        // default assign C: is SYS:C
        var (result, output) = await Run(program, options => options.Assigns["C"] = assign);

        Assert.Null(result.Error);
        Assert.Equal(expectedName, output);
        Assert.Equal(expectedDirectories, result.ReturnCode);
    }

    [Fact]
    public async Task When_FindingDosEntryFromEntry_Then_EntryPassedIsIncluded()
    {
        // Assign command finds entries starting with first entry returned by NextDosEntry()
        var program = DosProgram()
            .Move(Size.Long, Imm(LdfRead | LdfAssigns), D(1)).CallLib(Lvo("LockDosList"))
            .Move(Size.Long, D(0), D(1)).Moveq(LdfAssigns, 2).CallLib(Lvo("NextDosEntry"))
            .Move(Size.Long, D(0), D(4))
            .Move(Size.Long, D(0), D(1)).LeaD("name", 2).Moveq(LdfAssigns, 3).CallLib(Lvo("FindDosEntry"))
            .Cmp(Size.Long, D(0), 4)
            .Bne("notfound")
            .Moveq(1, 0)
            .Rts()
            .Label("notfound")
            .Moveq(0, 0)
            .Rts()
            .Label("name").String("AAA");

        // AAA is sorted as first assign in dos list
        var (result, _) = await Run(program, options => options.Assigns["AAA"] = "SYS:Work");

        Assert.Null(result.Error);
        Assert.Equal(1, result.ReturnCode);
    }

    [Fact]
    public async Task When_AssigningNameOfVolume_Then_AssignFailsWithObjectExists()
    {
        var program = DosProgram()
            .LeaD("name", 1).LeaD("path", 2).CallLib(Lvo("AssignPath"))
            .Tst(Size.Long, D(0))
            .Bne("failed")
            .CallLib(Lvo("IoErr"))
            .Rts()
            .Label("failed")
            .Moveq(-1, 0)
            .Rts()
            .Label("name").String("SYS")
            .Label("path").String("SYS:Work");

        var (result, _) = await Run(program);

        Assert.Equal(DosErrors.ObjectExists, result.ReturnCode);
    }
}

internal static class AssignAssemblerExtensions
{
    /// <summary>
    /// Load address of label into data register.
    /// </summary>
    public static M68kAssembler LeaD(this M68kAssembler assembler, string label, int dn) =>
        assembler.Lea(Pc(label), 0).Move(Size.Long, A(0), D(dn));

    /// <summary>
    /// Lock name at label and print name from lock using buffer at label "buffer".
    /// </summary>
    public static M68kAssembler PrintLockName(this M68kAssembler assembler, string label) =>
        assembler
            .LeaD(label, 1).Move(Size.Long, Imm(0xFFFFFFFE), D(2)).CallLib(Lvo("Lock"))
            .Move(Size.Long, D(0), D(1)).LeaD("buffer", 2).Move(Size.Long, Imm(256), D(3))
            .CallLib(Lvo("NameFromLock"))
            .LeaD("buffer", 1).CallLib(Lvo("PutStr"));

    private static int Lvo(string function) => -(30 + 6 * Array.IndexOf(DosFunctionNames.Names, function));
}
