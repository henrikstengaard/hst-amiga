using System.CommandLine;
using Hst.Amiga.Roms;

namespace Hst.Amiga.ConsoleApp;

public static class EpromCommandFactory
{
    public static Command CreateEpromCommand()
    {
        var command = new Command("eprom", "EPROM.");

        command.AddCommand(CreateEpromBuild());

        return command;
    }

    public static Command CreateEpromBuild()
    {
        var command = new Command("build", "Build EPROM.");

        command.AddCommand(CreateEpromBuildA500());
        command.AddCommand(CreateEpromBuildA600());
        command.AddCommand(CreateEpromBuildA2000());
        command.AddCommand(CreateEpromBuildA1200());
        command.AddCommand(CreateEpromBuildA3000());
        command.AddCommand(CreateEpromBuildA4000());

        return command;
    }

    private static Argument<string> CreateKickstartRomPathArgument() => new(
        name: "Kickstart rom path",
        description: "Path to kickstart .rom file.");
    
    private static Option<EpromType?> CreateEpromTypeOption() => new(
        new[] { "--eprom", "-e" },
        description: $"EPROM type to build for.");
        
    private static Option<int?> CreateSizeOption() => new(
        new[] { "--size", "-s" },
        description: $"Size of EPROM in bytes to build for.");
    
    private static Command CreateEpromBuildA500()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a500", "Build EPROM .bin files for A500 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA500, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 

    private static Command CreateEpromBuildA600()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a600", "Build EPROM .bin files for A600 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA600, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 

    private static Command CreateEpromBuildA2000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a2000", "Build EPROM .bin files for A2000 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA2000, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 

    private static Command CreateEpromBuildA1200()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a1200", "Build EPROM .bin files for A1200 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA1200, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 

    private static Command CreateEpromBuildA3000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a3000", "Build EPROM .bin files for A3000 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA3000, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 

    private static Command CreateEpromBuildA4000()
    {
        var kickstartRomPathArgument = CreateKickstartRomPathArgument();
        var epromTypeOption = CreateEpromTypeOption();
        var sizeOption = CreateSizeOption();
        
        var command = new Command("a4000", "Build EPROM .bin files for A4000 from kickstart rom file.");
        command.SetHandler(CommandHandler.EpromBuildA4000, kickstartRomPathArgument, epromTypeOption, sizeOption);
        command.AddArgument(kickstartRomPathArgument);
        command.AddOption(epromTypeOption);
        command.AddOption(sizeOption);

        return command;
    } 
}