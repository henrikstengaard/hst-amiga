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

public class GivenShell : IDisposable
{
    private const int OpenLibrary = -552;

    private readonly string sysPath;

    public GivenShell()
    {
        sysPath = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-shell-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(sysPath, "C"));
    }

    public void Dispose()
    {
        Directory.Delete(sysPath, true);
    }

    private static int Lvo(string function) => -(30 + 6 * Array.IndexOf(DosFunctionNames.Names, function));

    /// <summary>
    /// Program opening dos.library with library base in a6.
    /// </summary>
    private static M68kAssembler DosProgram() =>
        new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("dosname"), 1)
            .Moveq(36, 0)
            .CallLib(OpenLibrary)
            .Move(Size.Long, D(0), A(6));

    private void AddCommand(string name, M68kAssembler program, bool dos = true)
    {
        if (dos)
        {
            program.Label("dosname").String("dos.library");
        }

        File.WriteAllBytes(Path.Combine(sysPath, "C", name),
            new HunkFileBuilder().AddCode(program.ToArray()).Build());
    }

    private void AddReturnCodeCommand(string name, sbyte returnCode) =>
        AddCommand(name, new M68kAssembler().Moveq(returnCode, 0).Rts(), false);

    private async Task<(VmResult Result, string Output)> RunShell(string input)
    {
        var output = new MemoryStream();
        var options = new VmOptions
        {
            Input = new MemoryStream(Encoding.ASCII.GetBytes(input)),
            Output = output,
            Error = new MemoryStream(),
            Interactive = false,
            MaxInstructions = 10_000_000,
            Log = _ => { }
        };
        options.Volumes["SYS"] = sysPath;

        var result = await new AmigaVm(options).Shell();
        return (result, Encoding.ASCII.GetString(output.ToArray()).Replace("\r", string.Empty));
    }

    [Fact]
    public async Task When_RunningBuiltinCommands_Then_VariablesAndConditionsAreHandled()
    {
        var (result, output) = await RunShell(
            "echo hello  ; comment\n" +
            "set x 5\n" +
            "echo \"x is $x\"\n" +
            "if $x eq 5\n" +
            "  echo yes\n" +
            "else\n" +
            "  echo no\n" +
            "endif\n" +
            "if $x gt 9 val\n" +
            "  echo greater\n" +
            "endif\n" +
            "echo `echo inner` outer\n" +
            "alias say echo said []\n" +
            "say it\n" +
            "echo abcdef first 3 len 2\n" +
            "echo abc first 9\n");

        Assert.Null(result.Error);
        Assert.Equal("hello\nx is 5\nyes\ninner outer\nsaid it\ncd\nc\n", output);
    }

    [Fact]
    public async Task When_RunningDiskCommands_Then_ReturnCodeIsSetForIfAndVariables()
    {
        AddReturnCodeCommand("rc5", 5);

        var (result, output) = await RunShell(
            "rc5\n" +
            "if warn\n" +
            "  echo warned\n" +
            "endif\n" +
            "rc5\n" +
            "echo rc is $RC\n" +
            "foo\n" +
            "why\n");

        Assert.Null(result.Error);
        Assert.Equal("warned\nrc is 5\nUnknown command foo\nLast command failed because object not found\n",
            output);
    }

    [Fact]
    public async Task When_CommandCallsExit_Then_ShellContinuesWithReturnCode()
    {
        AddCommand("exit7", DosProgram().Moveq(7, 1).CallLib(Lvo("Exit")).Moveq(0, 0).Rts());

        var (result, output) = await RunShell("exit7\necho rc $RC\n");

        Assert.Null(result.Error);
        Assert.Equal("rc 7\n", output);
    }

    [Fact]
    public async Task When_RedirectingOutput_Then_OutputIsWrittenToFile()
    {
        var (_, output) = await RunShell("echo >SYS:out.txt one\necho >>SYS:out.txt two\necho done\n");

        Assert.Equal("done\n", output);
        Assert.Equal("one\ntwo\n", File.ReadAllText(Path.Combine(sysPath, "out.txt")));
    }

    [Fact]
    public async Task When_CommandReadsItemFromInput_Then_ArgumentsAreRead()
    {
        AddCommand("readitem", DosProgram()
            .Lea(Pc("buffer"), 0)
            .Move(Size.Long, A(0), D(1))
            .Moveq(80, 2)
            .Moveq(0, 3)
            .CallLib(Lvo("ReadItem"))
            .Lea(Pc("buffer"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(Lvo("PutStr"))
            .Moveq(0, 0)
            .Rts()
            .Label("buffer").Bytes(new byte[84]));

        var (_, output) = await RunShell("readitem \"quoted item\" second\n");

        Assert.Equal("quoted item", output);
    }

    [Fact]
    public async Task When_ProgramSetsCurrentInputToScript_Then_ShellRunsScript()
    {
        // program like execute opening script and setting it as current input of cli
        AddCommand("startscript", DosProgram()
            .Lea(Pc("script"), 0)
            .Move(Size.Long, A(0), D(1))
            .Move(Size.Long, Imm(1005), D(2))
            .CallLib(Lvo("Open"))
            .Move(Size.Long, D(0), D(3))
            .CallLib(Lvo("Cli"))
            .Move(Size.Long, D(0), A(0))
            .Move(Size.Long, D(3), Disp(32, 0))
            .Moveq(0, 0)
            .Rts()
            .Label("script").String("SYS:script"));
        AddReturnCodeCommand("rc10", 10);
        File.WriteAllText(Path.Combine(sysPath, "script"),
            "echo in script\n" +
            "skip later\n" +
            "echo skipped\n" +
            "lab later\n" +
            "rc10\n" +
            "echo not reached\n");

        var (result, output) = await RunShell("startscript\necho after script\n");

        Assert.Null(result.Error);
        Assert.Equal("in script\nrc10 failed returncode 10\nafter script\n", output);
    }

    [Fact]
    public async Task When_ScriptQuits_Then_ReturnCodeIsReturned()
    {
        var output = new MemoryStream();
        File.WriteAllText(Path.Combine(sysPath, "script"), "echo before\nquit 3\necho not reached\n");
        AddCommand("startscript", DosProgram()
            .Lea(Pc("script"), 0)
            .Move(Size.Long, A(0), D(1))
            .Move(Size.Long, Imm(1005), D(2))
            .CallLib(Lvo("Open"))
            .Move(Size.Long, D(0), D(3))
            .CallLib(Lvo("Cli"))
            .Move(Size.Long, D(0), A(0))
            .Move(Size.Long, D(3), Disp(32, 0))
            .Moveq(0, 0)
            .Rts()
            .Label("script").String("SYS:script"));
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

        var result = await new AmigaVm(options).Run(Path.Combine(sysPath, "C", "startscript"));

        Assert.Null(result.Error);
        Assert.Equal(3, result.ReturnCode);
        Assert.Equal("before\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public async Task When_ProgramCallsSystemTagList_Then_CommandIsRunAndReturnCodeReturned()
    {
        AddReturnCodeCommand("rc5", 5);
        AddCommand("system", DosProgram()
            .Lea(Pc("command1"), 0)
            .Move(Size.Long, A(0), D(1))
            .Moveq(0, 2)
            .CallLib(Lvo("SystemTagList"))
            .Lea(Pc("command2"), 0)
            .Move(Size.Long, A(0), D(1))
            .Moveq(0, 2)
            .CallLib(Lvo("SystemTagList"))
            .Rts()
            .Label("command1").String("echo from system")
            .Label("command2").String("rc5"));

        var (result, output) = await RunShell("system\necho rc $RC\n");

        Assert.Null(result.Error);
        Assert.Equal("from system\nrc 5\n", output);
    }

    [Fact]
    public async Task When_UsingPathAndCd_Then_CommandsAreFoundAndDirectoryChanged()
    {
        Directory.CreateDirectory(Path.Combine(sysPath, "Tools"));
        File.WriteAllBytes(Path.Combine(sysPath, "Tools", "tool"),
            new HunkFileBuilder().AddCode(new M68kAssembler().Moveq(6, 0).Rts().ToArray()).Build());

        var (_, output) = await RunShell(
            "tool\n" +
            "path SYS:Tools add\n" +
            "tool\n" +
            "echo rc $RC\n" +
            "cd SYS:Tools\n" +
            "cd\n" +
            "SYS:\n" +
            "cd\n");

        Assert.Equal("Unknown command tool\nrc 6\nSYS:Tools\nSYS:\n", output);
    }

    [Fact]
    public async Task When_ExecutingScriptWithoutExecuteCommand_Then_ErrorIsReturned()
    {
        var options = new VmOptions
        {
            Input = new MemoryStream(),
            Output = new MemoryStream(),
            Error = new MemoryStream(),
            Interactive = false,
            Log = _ => { }
        };
        options.Volumes["SYS"] = sysPath;

        var result = await new AmigaVm(options).Execute("SYS:script");

        Assert.Equal(VmExitReason.Error, result.ExitReason);
        Assert.Contains("Execute command not found", result.Error);
    }

    [Fact]
    public async Task When_ShellIsInteractive_Then_PromptIsWritten()
    {
        var output = new MemoryStream();
        var options = new VmOptions
        {
            Input = new MemoryStream(Encoding.ASCII.GetBytes("prompt \"%N.%S> \"\necho hi\n")),
            Output = output,
            Error = new MemoryStream(),
            Interactive = true,
            Log = _ => { }
        };
        options.Volumes["SYS"] = sysPath;

        await new AmigaVm(options).Shell();

        Assert.Equal("1> 1.SYS:> hi\n1.SYS:> ", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public async Task When_EndShell_Then_RemainingInputIsNotRead()
    {
        var (_, output) = await RunShell("echo one\nendshell\necho two\n");

        Assert.Equal("one\n", output);
    }
}
