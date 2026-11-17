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
            Description =
                "Path to Amiga executable. Host path, path in adf or disk image file with rigid disk block, e.g. workbench.adf\\c\\list, disk.vhd\\c\\list in boot partition or disk.vhd\\rdb\\1\\c\\list in partition by number or device name, or Amiga path in mounted images, e.g. C:List."
        };

        var argumentsArgument = new Argument<string[]>("Arguments")
        {
            Description = "Arguments passed to Amiga executable. Use -- before arguments starting with -.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var imageContextOption = new Option<bool>("--image-context")
        {
            Description =
                "Use image context for executable in adf or disk image: true resolves paths inside the image by mounting it as volumes with SYS: set to adf or bootable partition with highest boot priority, false (default) reads executable from image and resolves host paths.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("run", "Run Amiga CLI executable.");
        command.Arguments.Add(executableArgument);
        command.Arguments.Add(argumentsArgument);
        command.Options.Add(imageContextOption);
        var options = new VmCommandOptions(command);
        command.SetAction((parseResult, token) => options.Run(parseResult, VmRunMode.Run,
            parseResult.GetValue(executableArgument), parseResult.GetValue(argumentsArgument), token,
            parseResult.GetValue(imageContextOption)));

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
