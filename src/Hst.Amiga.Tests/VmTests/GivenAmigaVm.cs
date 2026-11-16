namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HunkTests;
using Vm;
using Vm.Processor;
using Xunit;
using static M68kAssembler;

public class GivenAmigaVm : IDisposable
{
    // exec.library offsets
    private const int OpenLibrary = -552;
    private const int AddTask = -282;
    private const int RawDoFmt = -522;

    // dos.library offsets
    private const int Open = -30;
    private const int Close = -36;
    private const int Read = -42;
    private const int Write = -48;
    private const int Input = -54;
    private const int Output = -60;
    private const int Lock = -84;
    private const int UnLock = -90;
    private const int Examine = -102;
    private const int ExNext = -108;
    private const int Exit = -144;
    private const int AllocDosObject = -228;
    private const int ReadArgs = -798;
    private const int FreeArgs = -858;
    private const int PutStr = -948;
    private const int VPrintf = -954;

    private readonly string sysPath;

    public GivenAmigaVm()
    {
        sysPath = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-sys-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sysPath);
    }

    public void Dispose()
    {
        Directory.Delete(sysPath, true);
    }

    /// <summary>
    /// Create program opening dos.library with library base in a6. Original a0 and d0 with arguments are saved
    /// in a2 and d2.
    /// </summary>
    private static M68kAssembler DosProgram() =>
        new M68kAssembler()
            .Move(Size.Long, A(0), A(2))
            .Move(Size.Long, D(0), D(2))
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("dosname"), 1)
            .Moveq(36, 0)
            .CallLib(OpenLibrary)
            .Move(Size.Long, D(0), A(6));

    private static M68kAssembler DosName(M68kAssembler assembler) =>
        assembler.Label("dosname").String("dos.library");

    private async Task<(VmResult Result, string Output)> Run(M68kAssembler program, string arguments = "",
        string input = "", CpuType cpuType = CpuType.M68020)
    {
        var executable = new HunkFileBuilder().AddCode(DosName(program).ToArray()).Build();
        var output = new MemoryStream();
        var error = new MemoryStream();
        var options = new VmOptions
        {
            CpuType = cpuType,
            Input = new MemoryStream(Encoding.ASCII.GetBytes(input)),
            Output = output,
            Error = error,
            Interactive = false,
            MaxInstructions = 10_000_000,
            Log = _ => { }
        };
        options.Volumes["SYS"] = sysPath;

        var vm = new AmigaVm(options);
        var result = await vm.Run(executable, "test", arguments);
        return (result, Encoding.ASCII.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData(CpuType.M68000)]
    [InlineData(CpuType.M68020)]
    public async Task When_RunningHelloWorld_Then_OutputIsWrittenToConsole(CpuType cpuType)
    {
        var program = DosProgram()
            .Lea(Pc("message"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(PutStr)
            .Moveq(0, 0)
            .Rts()
            .Label("message").String("Hello, world!\n");

        var (result, output) = await Run(program, cpuType: cpuType);

        Assert.Equal(VmExitReason.Returned, result.ExitReason);
        Assert.Null(result.Error);
        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("Hello, world!\n", output);
    }

    [Fact]
    public async Task When_RunningProgramEchoingArguments_Then_ArgumentStringIsPassedInRegisters()
    {
        var program = DosProgram()
            .Move(Size.Long, D(2), D(3))
            .CallLib(Output)
            .Move(Size.Long, D(0), D(1))
            .Move(Size.Long, A(2), D(2))
            .CallLib(Write)
            .Moveq(0, 0)
            .Rts();

        var (result, output) = await Run(program, "hello \"amiga\" world");

        Assert.Equal(VmExitReason.Returned, result.ExitReason);
        Assert.Equal("hello \"amiga\" world\n", output);
    }

    [Fact]
    public async Task When_ProgramReturnsReturnCode_Then_ReturnCodeIsReturned()
    {
        var program = new M68kAssembler()
            .Moveq(5, 0)
            .Rts();

        var (result, _) = await Run(program);

        Assert.Equal(VmExitReason.Returned, result.ExitReason);
        Assert.Equal(5, result.ReturnCode);
    }

    [Fact]
    public async Task When_ProgramCallsExit_Then_ReturnCodeIsReturned()
    {
        var program = DosProgram()
            .Moveq(10, 1)
            .CallLib(Exit)
            .Moveq(0, 0)
            .Rts();

        var (result, _) = await Run(program);

        Assert.Equal(VmExitReason.Exit, result.ExitReason);
        Assert.Equal(10, result.ReturnCode);
    }

    [Fact]
    public async Task When_ProgramWritesFile_Then_FileIsCreatedInHostDirectory()
    {
        var program = DosProgram()
            .Lea(Pc("name"), 0)
            .Move(Size.Long, A(0), D(1))
            .Move(Size.Long, Imm(1006), D(2))
            .CallLib(Open)
            .Move(Size.Long, D(0), D(4))
            .Beq("fail")
            .Move(Size.Long, D(4), D(1))
            .Lea(Pc("data"), 0)
            .Move(Size.Long, A(0), D(2))
            .Moveq(6, 3)
            .CallLib(Write)
            .Move(Size.Long, D(4), D(1))
            .CallLib(Close)
            .Moveq(0, 0)
            .Rts()
            .Label("fail")
            .Moveq(20, 0)
            .Rts()
            .Label("name").String("SYS:out.txt")
            .Label("data").String("Amiga!");

        var (result, _) = await Run(program);

        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("Amiga!", await File.ReadAllTextAsync(Path.Combine(sysPath, "out.txt")));
    }

    [Fact]
    public async Task When_ProgramListsDirectory_Then_EntriesAreExamined()
    {
        await File.WriteAllTextAsync(Path.Combine(sysPath, "b.txt"), "b");
        await File.WriteAllTextAsync(Path.Combine(sysPath, "a.txt"), "a");

        var program = DosProgram()
            .Lea(Pc("sys"), 0)
            .Move(Size.Long, A(0), D(1))
            .Moveq(-2, 2)
            .CallLib(Lock)
            .Move(Size.Long, D(0), D(4))
            .Moveq(2, 1)
            .Moveq(0, 2)
            .CallLib(AllocDosObject)
            .Move(Size.Long, D(0), D(5))
            .Move(Size.Long, D(4), D(1))
            .Move(Size.Long, D(5), D(2))
            .CallLib(Examine)
            .Label("loop")
            .Move(Size.Long, D(4), D(1))
            .Move(Size.Long, D(5), D(2))
            .CallLib(ExNext)
            .Tst(Size.Long, D(0))
            .Beq("done")
            .Move(Size.Long, D(5), D(1))
            .Addq(Size.Long, 8, D(1))
            .CallLib(PutStr)
            .Lea(Pc("newline"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(PutStr)
            .Bra("loop")
            .Label("done")
            .Move(Size.Long, D(4), D(1))
            .CallLib(UnLock)
            .Moveq(0, 0)
            .Rts()
            .Label("sys").String("SYS:")
            .Label("newline").String("\n");

        var (result, output) = await Run(program);

        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("a.txt\nb.txt\n", output);
    }

    [Fact]
    public async Task When_ProgramReadsArgumentsWithTemplate_Then_ArgumentsAreParsed()
    {
        var program = DosProgram()
            .Lea(Pc("template"), 0)
            .Move(Size.Long, A(0), D(1))
            .Lea(Pc("array"), 0)
            .Move(Size.Long, A(0), D(2))
            .Moveq(0, 3)
            .CallLib(ReadArgs)
            .Move(Size.Long, D(0), D(5))
            .Beq("fail")
            .Lea(Pc("array"), 0)
            .Move(Size.Long, Ind(0), D(1))
            .CallLib(PutStr)
            .Lea(Pc("array"), 0)
            .Move(Size.Long, Disp(4, 0), A(1))
            .Move(Size.Long, Ind(1), D(6))
            .Move(Size.Long, D(5), D(1))
            .CallLib(FreeArgs)
            .Move(Size.Long, D(6), D(0))
            .Rts()
            .Label("fail")
            .Moveq(20, 0)
            .Rts()
            .Align()
            .Label("array").Long(0).Long(0).Long(0)
            .Label("template").String("NAME/A,COUNT/N,LOUD/S");

        var (result, output) = await Run(program, "Amiga COUNT=7 LOUD");

        Assert.Equal(VmExitReason.Returned, result.ExitReason);
        Assert.Equal(7, result.ReturnCode);
        Assert.Equal("Amiga", output);
    }

    [Fact]
    public async Task When_ProgramPrintsFormattedString_Then_ArgumentsAreFormatted()
    {
        var program = DosProgram()
            .Lea(Pc("name"), 0)
            .Lea(Pc("args"), 1)
            .Move(Size.Long, A(0), Ind(1))
            .Move(Size.Long, Imm(1234), Disp(4, 1))
            .Lea(Pc("format"), 0)
            .Move(Size.Long, A(0), D(1))
            .Move(Size.Long, A(1), D(2))
            .CallLib(VPrintf)
            .Moveq(0, 0)
            .Rts()
            .Align()
            .Label("args").Long(0).Long(0)
            .Label("format").String("%s has %ld bytes\n")
            .Label("name").String("file");

        var (result, output) = await Run(program);

        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("file has 1234 bytes\n", output);
    }

    [Fact]
    public async Task When_ProgramFormatsWithPutCharProcedure_Then_ProcedureIsCalledForEachCharacter()
    {
        var program = DosProgram()
            .Move(Size.Long, A(6), A(5))
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("format"), 0)
            .Lea(Pc("args"), 1)
            .Lea(Pc("putch"), 2)
            .Lea(Pc("buffer"), 3)
            .CallLib(RawDoFmt)
            .Move(Size.Long, A(5), A(6))
            .Lea(Pc("buffer"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(PutStr)
            .Moveq(0, 0)
            .Rts()
            .Label("putch")
            .Move(Size.Byte, D(0), PostInc(3))
            .Nop()
            .Rts()
            .Align()
            .Label("args").Word(42)
            .Label("format").String("value=%d\n")
            .Align()
            .Label("buffer").Long(0).Long(0).Long(0).Long(0);

        var (result, output) = await Run(program);

        Assert.Null(result.Error);
        Assert.Equal("value=42\n", output);
    }

    [Fact]
    public async Task When_ProgramReadsInput_Then_ConsoleInputIsRead()
    {
        var program = DosProgram()
            .CallLib(Input)
            .Move(Size.Long, D(0), D(1))
            .Lea(Pc("buffer"), 0)
            .Move(Size.Long, A(0), D(2))
            .Moveq(16, 3)
            .CallLib(Read)
            .Move(Size.Long, D(0), D(3))
            .CallLib(Output)
            .Move(Size.Long, D(0), D(1))
            .Lea(Pc("buffer"), 0)
            .Move(Size.Long, A(0), D(2))
            .CallLib(Write)
            .Moveq(0, 0)
            .Rts()
            .Align()
            .Label("buffer").Long(0).Long(0).Long(0).Long(0);

        var (result, output) = await Run(program, input: "abc\n");

        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("abc\n", output);
    }

    [Fact]
    public async Task When_ProgramCallsUnimplementedFunction_Then_FunctionNameIsReported()
    {
        var program = new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .CallLib(AddTask)
            .Rts();

        var (result, _) = await Run(program);

        Assert.Equal(VmExitReason.UnimplementedFunction, result.ExitReason);
        Assert.Contains("exec.library/AddTask", result.Error);
    }

    [Fact]
    public async Task When_ProgramExecutesIllegalInstruction_Then_CpuExceptionIsReported()
    {
        var program = new M68kAssembler()
            .Word(0x4AFC)
            .Rts();

        var (result, _) = await Run(program);

        Assert.Equal(VmExitReason.CpuException, result.ExitReason);
        Assert.Contains("Illegal instruction", result.Error);
        Assert.Equal(20, result.ReturnCode);
    }

    [Theory]
    [InlineData("intuition.library", 0)]
    [InlineData("mathieeesingbas.library", 0)]
    [InlineData("reqtools.library", 20)]
    public async Task When_ProgramOpensLibrary_Then_StubLibrariesCanBeOpened(string libraryName,
        int expectedReturnCode)
    {
        var program = new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("library"), 1)
            .Moveq(33, 0)
            .CallLib(OpenLibrary)
            .Tst(Size.Long, D(0))
            .Beq("fail")
            .Moveq(0, 0)
            .Rts()
            .Label("fail")
            .Moveq(20, 0)
            .Rts()
            .Label("library").String(libraryName);

        var (result, _) = await Run(program);

        Assert.Equal(expectedReturnCode, result.ReturnCode);
    }

    [Fact]
    public async Task When_ProgramLoopsForever_Then_InstructionLimitStopsProgram()
    {
        var program = new M68kAssembler()
            .Label("loop")
            .Bra("loop");

        var (result, _) = await Run(program);

        Assert.Equal(VmExitReason.InstructionLimit, result.ExitReason);
    }
}
