namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Commands;

/// <summary>
/// Options shared by virtual machine commands.
/// </summary>
public class VmCommandOptions
{
    private readonly Option<string> cpuOption;
    private readonly Option<int?> memoryOption;
    private readonly Option<int?> stackOption;
    private readonly Option<string[]> volumeOption;
    private readonly Option<string[]> assignOption;
    private readonly Option<string[]> stubLibraryOption;
    private readonly Option<string> currentDirectoryOption;
    private readonly Option<long?> maxInstructionsOption;
    private readonly Option<bool> traceOption;
    private readonly Option<bool> traceLibrariesOption;
    private readonly Option<string> uaeMetadataOption;
    private readonly Option<bool> writeUaeMetadataOption;

    /// <summary>
    /// Create options and add them to command.
    /// </summary>
    public VmCommandOptions(Command command)
    {
        cpuOption = new Option<string>("--cpu", "-c")
        {
            Description = "Cpu to emulate, 68000 or 68020.",
            DefaultValueFactory = _ => "68020"
        };
        cpuOption.AcceptOnlyFromAmong("68000", "68020");

        memoryOption = new Option<int?>("--memory", "-m")
        {
            Description = "Memory size in MB, default 8 MB (max 16 MB for 68000)."
        };

        stackOption = new Option<int?>("--stack", "-s")
        {
            Description = "Stack size in bytes, default 65536."
        };

        volumeOption = new Option<string[]>("--volume", "-V")
        {
            Description =
                "Map Amiga volume to host directory as name=path, e.g. SYS=c:\\amiga. Default maps SYS to current directory.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        assignOption = new Option<string[]>("--assign", "-a")
        {
            Description = "Add assign to Amiga path as name=path, e.g. C=SYS:C. Separate multiple paths with ;.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        stubLibraryOption = new Option<string[]>("--stub-lib")
        {
            Description =
                "Add stub library, which can be opened, but stops program if any function is called. Default stubs are intuition.library and graphics.library.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        currentDirectoryOption = new Option<string>("--cwd")
        {
            Description = "Amiga path of current directory, e.g. SYS:Work."
        };

        maxInstructionsOption = new Option<long?>("--max-instructions")
        {
            Description = "Max number of instructions to execute."
        };

        traceOption = new Option<bool>("--trace")
        {
            Description = "Trace instructions executed to standard error."
        };

        traceLibrariesOption = new Option<bool>("--trace-libs")
        {
            Description = "Trace library function calls to standard error."
        };

        uaeMetadataOption = new Option<string>("--uae-metadata")
        {
            Description =
                "UAE metadata for Amiga names, protection bits and comments of files: auto detects UAEFSDB (WinUAE) and UAE metafiles (FS-UAE) per directory, uaefsdb, uaemetafile or none.",
            DefaultValueFactory = _ => "auto"
        };
        uaeMetadataOption.AcceptOnlyFromAmong("auto", "uaefsdb", "uaemetafile", "none");

        writeUaeMetadataOption = new Option<bool>("--write-uae-metadata")
        {
            Description =
                "Write changes to protection bits, comments and names to UAE metadata files. Default keeps changes in memory and never modifies metadata files."
        };

        command.Options.Add(cpuOption);
        command.Options.Add(memoryOption);
        command.Options.Add(stackOption);
        command.Options.Add(volumeOption);
        command.Options.Add(assignOption);
        command.Options.Add(stubLibraryOption);
        command.Options.Add(currentDirectoryOption);
        command.Options.Add(maxInstructionsOption);
        command.Options.Add(traceOption);
        command.Options.Add(traceLibrariesOption);
        command.Options.Add(uaeMetadataOption);
        command.Options.Add(writeUaeMetadataOption);
    }

    /// <summary>
    /// Run virtual machine command with parsed option values.
    /// </summary>
    public Task<int> Run(ParseResult parseResult, VmRunMode mode, string path, string[] arguments,
        CancellationToken token) =>
        CommandHandler.VmRun(mode, path, arguments,
            parseResult.GetValue(cpuOption),
            parseResult.GetValue(memoryOption),
            parseResult.GetValue(stackOption),
            parseResult.GetValue(volumeOption),
            parseResult.GetValue(assignOption),
            parseResult.GetValue(stubLibraryOption),
            parseResult.GetValue(currentDirectoryOption),
            parseResult.GetValue(maxInstructionsOption),
            parseResult.GetValue(traceOption),
            parseResult.GetValue(traceLibrariesOption),
            parseResult.GetValue(uaeMetadataOption),
            parseResult.GetValue(writeUaeMetadataOption),
            token);
}
