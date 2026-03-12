namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class CommandFactory
{
    public static readonly Option<bool> VerboseOption = new(
        new []{ "--verbose" },
        description: "Verbose output.");
    
    public static Command CreateRootCommand()
    {
        var rootCommand = new RootCommand
        {
            Description = "Hst Amiga."
        };

        rootCommand.AddGlobalOption(VerboseOption);
        rootCommand.AddCommand(IconCommandFactory.CreateIconCommand());
        rootCommand.AddCommand(EpromCommandFactory.CreateEpromCommand());
        rootCommand.AddCommand(CreateScriptCommand());

        return rootCommand;
    }
    
    private static Command CreateScriptCommand()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to script file.");

        var scriptCommand = new Command("script", "Run a script.");
        scriptCommand.AddArgument(pathArgument);
        scriptCommand.SetHandler(CommandHandler.Script, pathArgument);

        return scriptCommand;
    }
}