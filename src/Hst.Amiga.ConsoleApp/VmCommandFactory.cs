namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;
using Commands;

public static class VmCommandFactory
{
    public static Command CreateVmCommand()
    {
        var command = new Command("vm", "Virtual machine running Amiga CLI executables.");

        command.Subcommands.Add(CreateVmRun());
        command.Subcommands.Add(CreateVmExecute());
        command.Subcommands.Add(CreateVmShell());

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

        var command = new Command("run", "Run Amiga CLI executable.");
        command.Arguments.Add(executableArgument);
        command.Arguments.Add(argumentsArgument);
        var options = new VmCommandOptions(command);
        command.SetAction((parseResult, token) => options.Run(parseResult, VmRunMode.Run,
            parseResult.GetValue(executableArgument), parseResult.GetValue(argumentsArgument), token));

        return command;
    }

    private static Command CreateVmExecute()
    {
        var scriptArgument = new Argument<string>("Script")
        {
            Description = "Host path or Amiga path to AmigaDOS script, e.g. SYS:S/Script."
        };

        var argumentsArgument = new Argument<string[]>("Arguments")
        {
            Description = "Arguments passed to script. Use -- before arguments starting with -.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("execute",
            "Execute AmigaDOS script with Execute command from C:, e.g. from a Workbench installation.");
        command.Arguments.Add(scriptArgument);
        command.Arguments.Add(argumentsArgument);
        var options = new VmCommandOptions(command);
        command.SetAction((parseResult, token) => options.Run(parseResult, VmRunMode.Execute,
            parseResult.GetValue(scriptArgument), parseResult.GetValue(argumentsArgument), token));

        return command;
    }

    private static Command CreateVmShell()
    {
        var command = new Command("shell", "Run interactive AmigaDOS shell.");
        var options = new VmCommandOptions(command);
        command.SetAction((parseResult, token) => options.Run(parseResult, VmRunMode.Shell, null, null, token));

        return command;
    }
}
