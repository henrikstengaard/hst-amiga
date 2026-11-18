using System.CommandLine;

namespace Hst.Amiga.ConsoleApp;

public static class KickstartCommandFactory
{
    public static Command CreateKickstartCommand()
    {
        var command = new Command("kickstart", "Inspect and extract Kickstart ROM modules.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateExtractCommand());
        command.Subcommands.Add(CreateScsiCommand());
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

    private static Option<string> CreateRemusSplitDataOption() => new("--remus-split-data")
    {
        Description = "Path to a Remus/ROMSplit split-data .dat file. When extracting, requires --hunk."
    };

    private static Command CreateListCommand()
    {
        var romPathArgument = CreateRomPathArgument();
        var keyFileOption = CreateKeyFileOption();
        var remusSplitDataOption = CreateRemusSplitDataOption();
        var command = new Command("list", "List resident modules in a Kickstart ROM.");
        command.Arguments.Add(romPathArgument);
        command.Options.Add(keyFileOption);
        command.Options.Add(remusSplitDataOption);
        command.SetAction((parseResult, _) => CommandHandler.KickstartList(
            parseResult.GetValue(romPathArgument), parseResult.GetValue(keyFileOption),
            parseResult.GetValue(remusSplitDataOption)));
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
        var remusSplitDataOption = CreateRemusSplitDataOption();
        var command = new Command("extract", "Extract resident modules from a Kickstart ROM.");
        command.Arguments.Add(romPathArgument);
        command.Arguments.Add(outputDirectoryArgument);
        command.Options.Add(keyFileOption);
        command.Options.Add(hunkOption);
        command.Options.Add(remusSplitDataOption);
        command.SetAction((parseResult, _) => CommandHandler.KickstartExtract(
            parseResult.GetValue(romPathArgument), parseResult.GetValue(outputDirectoryArgument),
            parseResult.GetValue(keyFileOption), parseResult.GetValue(hunkOption),
            parseResult.GetValue(remusSplitDataOption)));
        return command;
    }

    private static Command CreateScsiCommand()
    {
        var command = new Command("scsi", "Extract or patch scsi.device for GetSCSI 1.1 compatibility.");
        command.Subcommands.Add(CreateScsiExtractCommand());
        command.Subcommands.Add(CreateScsiPatchCommand());
        return command;
    }

    private static Command CreateScsiExtractCommand()
    {
        var input = CreateRomPathArgument();
        var keyFile = CreateKeyFileOption();
        var output = new Argument<string>("output path")
        {
            Description = "Destination file, as an alternative to --output.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var outputOption = new Option<string>("--output", "-o")
        {
            Description = "Destination file (defaults to scsi.device in the current directory)."
        };
        var command = new Command("extract", "Extract only scsi.device with the GetSCSI compatibility patch.");
        command.Arguments.Add(input);
        command.Arguments.Add(output);
        command.Options.Add(keyFile);
        command.Options.Add(outputOption);
        command.Validators.Add(result =>
        {
            if (result.GetValue(output) != null && result.GetValue(outputOption) != null)
            {
                result.AddError("Specify the destination as either an argument or --output, not both.");
            }
        });
        command.SetAction((result, _) => CommandHandler.KickstartScsiExtract(result.GetValue(input),
            result.GetValue(outputOption) ?? result.GetValue(output), result.GetValue(keyFile)));
        return command;
    }

    private static Command CreateScsiPatchCommand()
    {
        var input = new Argument<string>("device path") { Description = "Path to an existing scsi.device Hunk." };
        var output = new Argument<string>("output path")
        {
            Description = "Destination file (defaults to updating the input file).",
            Arity = ArgumentArity.ZeroOrOne
        };
        var command = new Command("patch", "Apply the GetSCSI compatibility patch to an existing scsi.device Hunk.");
        command.Arguments.Add(input);
        command.Arguments.Add(output);
        command.SetAction((result, _) => CommandHandler.KickstartScsiPatch(result.GetValue(input), result.GetValue(output)));
        return command;
    }
}
