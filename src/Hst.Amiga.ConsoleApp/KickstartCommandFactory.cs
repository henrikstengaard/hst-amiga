using System.CommandLine;

namespace Hst.Amiga.ConsoleApp;

public static class KickstartCommandFactory
{
    public static Command CreateKickstartCommand()
    {
        var command = new Command("kickstart", "Inspect and extract Kickstart ROM modules.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateExtractCommand());
        return command;
    }

    private static Argument<string> CreateRomPathArgument() => new("rom path")
    {
        Description = "Path to a raw or encrypted Kickstart ROM file."
    };

    private static Option<string> CreateKeyFileOption() => new("--key-file", "-k")
    {
        Description = "Path to Amiga Forever rom.key (defaults to rom.key beside the ROM file)."
    };

    private static Command CreateListCommand()
    {
        var romPathArgument = CreateRomPathArgument();
        var keyFileOption = CreateKeyFileOption();
        var command = new Command("list", "List resident modules in a Kickstart ROM.");
        command.Arguments.Add(romPathArgument);
        command.Options.Add(keyFileOption);
        command.SetAction((parseResult, _) => CommandHandler.KickstartList(
            parseResult.GetValue(romPathArgument), parseResult.GetValue(keyFileOption)));
        return command;
    }

    private static Command CreateExtractCommand()
    {
        var romPathArgument = CreateRomPathArgument();
        var outputDirectoryArgument = new Argument<string>("output directory")
        {
            Description = "Directory to write extracted module files (defaults to <rom-name>.modules)."
        };
        outputDirectoryArgument.Arity = ArgumentArity.ZeroOrOne;
        var keyFileOption = CreateKeyFileOption();
        var hunkOption = new Option<bool>("--hunk")
        {
            Description = "Wrap each extracted ROM module as a relocatable Amiga Hunk file."
        };
        var command = new Command("extract", "Extract resident modules from a Kickstart ROM.");
        command.Arguments.Add(romPathArgument);
        command.Arguments.Add(outputDirectoryArgument);
        command.Options.Add(keyFileOption);
        command.Options.Add(hunkOption);
        command.SetAction((parseResult, _) => CommandHandler.KickstartExtract(
            parseResult.GetValue(romPathArgument), parseResult.GetValue(outputDirectoryArgument),
            parseResult.GetValue(keyFileOption), parseResult.GetValue(hunkOption)));
        return command;
    }
}
