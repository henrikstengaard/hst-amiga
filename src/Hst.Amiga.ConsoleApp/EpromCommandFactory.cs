using System.CommandLine;
using Hst.Amiga.Roms;

namespace Hst.Amiga.ConsoleApp;

public static class EpromCommandFactory
{
    public static Command CreateEpromCommand()
    {
        var command = new Command("eprom", "EPROM.");

        command.Subcommands.Add(CreateEpromBuild());
        command.Subcommands.Add(CreateEpromFill());
        command.Subcommands.Add(CreateEpromByteSwap());

        return command;
    }

    public static Command CreateEpromBuild()
    {
        var command = new Command("build", "Build EPROM.");

        command.Subcommands.Add(CreateEpromBuildA500());
        command.Subcommands.Add(CreateEpromBuildA600());
        command.Subcommands.Add(CreateEpromBuildA2000());
        command.Subcommands.Add(CreateEpromBuildA1200());
        command.Subcommands.Add(CreateEpromBuildA3000());
        command.Subcommands.Add(CreateEpromBuildA4000());

        return command;
    }

    private static Argument<string> CreateKickstartRomPathArgument() => new("Kickstart rom path")
    {
        Description = "Path to kickstart .rom file."
    };

    private static Option<EpromType?> CreateEpromTypeOption() => new("--eprom", "-e")
    {
        Description = "EPROM type to use."
    };

    private static Option<int?> CreateSizeOption() => new("--size", "-s")
    {
        Description = "Size of EPROM in bytes to use."
    };

    private static Command CreateEpromBuildA500()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a500", "Build EPROM .bin files for A500 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA500(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromBuildA600()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a600", "Build EPROM .bin files for A600 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA600(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromBuildA2000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a2000", "Build EPROM .bin files for A2000 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA2000(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromBuildA1200()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a1200", "Build EPROM .bin files for A1200 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA1200(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromBuildA3000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a3000", "Build EPROM .bin files for A3000 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA3000(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromBuildA4000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var command = new Command("a4000", "Build EPROM .bin files for A4000 from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromBuildA4000(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);

        return command;
    }

    private static Command CreateEpromFill()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();

        var zeroFillOption = new Option<bool?>("--zero-fill", "-z")
        {
            Description = "Fill zeroes until size of EPROM instead of concatenating Kickstart rom multiple times."
        };


        var command = new Command("fill", "Fill EPROM .bin file from kickstart rom file concatenated multiple times.");
        command.SetAction((parseResult, _) => CommandHandler.EpromFill(
            parseResult.GetValue(kickstartRomPathArgument), parseResult.GetValue(epromTypeOption),
            parseResult.GetValue(sizeOption), parseResult.GetValue(zeroFillOption)));
        command.Arguments.Add(kickstartRomPathArgument);
        command.Options.Add(epromTypeOption);
        command.Options.Add(sizeOption);
        command.Options.Add(zeroFillOption);

        return command;
    }

    private static Command CreateEpromByteSwap()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();

        var command = new Command("byteswap", "Byte swap EPROM .bin file from kickstart rom file.");
        command.SetAction((parseResult, _) => CommandHandler.EpromByteSwap(
            parseResult.GetValue(kickstartRomPathArgument)));
        command.Arguments.Add(kickstartRomPathArgument);

        return command;
    }
}