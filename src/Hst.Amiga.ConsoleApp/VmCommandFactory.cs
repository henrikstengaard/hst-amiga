namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class VmCommandFactory
{
    public static Command CreateVmCommand()
    {
        var command = new Command("vm", "Virtual machine running Amiga CLI executables.");

        command.Subcommands.Add(CreateVmRun());

        return command;
    }

    private static Command CreateVmRun()
    {
        var executableArgument = new Argument<string>("Executable")
        {
            Description = "Path to Amiga executable."
        };

        var argumentsArgument = new Argument<string[]>("Arguments")
        {
            Description = "Arguments passed to Amiga executable. Use -- before arguments starting with -.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var cpuOption = new Option<string>("--cpu", "-c")
        {
            Description = "Cpu to emulate, 68000 or 68020.",
            DefaultValueFactory = _ => "68020"
        };
        cpuOption.AcceptOnlyFromAmong("68000", "68020");

        var memoryOption = new Option<int?>("--memory", "-m")
        {
            Description = "Memory size in MB, default 8 MB (max 16 MB for 68000)."
        };

        var stackOption = new Option<int?>("--stack", "-s")
        {
            Description = "Stack size in bytes, default 65536."
        };

        var volumeOption = new Option<string[]>("--volume", "-V")
        {
            Description =
                "Map Amiga volume to host directory as name=path, e.g. SYS=c:\\amiga. Default maps SYS to current directory.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        var assignOption = new Option<string[]>("--assign", "-a")
        {
            Description = "Add assign to Amiga path as name=path, e.g. C=SYS:C. Separate multiple paths with ;.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        var stubLibraryOption = new Option<string[]>("--stub-lib")
        {
            Description =
                "Add stub library, which can be opened, but stops program if any function is called. Default stubs are intuition.library and graphics.library.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false
        };

        var currentDirectoryOption = new Option<string>("--cwd")
        {
            Description = "Amiga path of current directory, e.g. SYS:Work."
        };

        var maxInstructionsOption = new Option<long?>("--max-instructions")
        {
            Description = "Max number of instructions to execute."
        };

        var traceOption = new Option<bool>("--trace")
        {
            Description = "Trace instructions executed to standard error."
        };

        var traceLibrariesOption = new Option<bool>("--trace-libs")
        {
            Description = "Trace library function calls to standard error."
        };

        var uaeMetadataOption = new Option<string>("--uae-metadata")
        {
            Description =
                "UAE metadata for Amiga names, protection bits and comments of files: auto detects UAEFSDB (WinUAE) and UAE metafiles (FS-UAE) per directory, uaefsdb, uaemetafile or none.",
            DefaultValueFactory = _ => "auto"
        };
        uaeMetadataOption.AcceptOnlyFromAmong("auto", "uaefsdb", "uaemetafile", "none");

        var writeUaeMetadataOption = new Option<bool>("--write-uae-metadata")
        {
            Description =
                "Write changes to protection bits, comments and names to UAE metadata files. Default keeps changes in memory and never modifies metadata files."
        };

        var command = new Command("run", "Run Amiga CLI executable.");
        command.SetAction((parseResult, token) => CommandHandler.VmRun(
            parseResult.GetValue(executableArgument),
            parseResult.GetValue(argumentsArgument),
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
            token));
        command.Arguments.Add(executableArgument);
        command.Arguments.Add(argumentsArgument);
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

        return command;
    }
}
