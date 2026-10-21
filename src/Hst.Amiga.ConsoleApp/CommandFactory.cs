namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class CommandFactory
{
    public static readonly Option<bool> VerboseOption = new("--verbose")
    {
        Description = "Verbose output.",
        Recursive = true
    };

    public static Command CreateRootCommand()
    {
        var rootCommand = new RootCommand
        {
            Description = "Hst Amiga."
        };

        rootCommand.Options.Add(VerboseOption);
        rootCommand.Subcommands.Add(IconCommandFactory.CreateIconCommand());
        rootCommand.Subcommands.Add(EpromCommandFactory.CreateEpromCommand());
        rootCommand.Subcommands.Add(CreateScriptCommand());

        return rootCommand;
    }

    private static Command CreateScriptCommand()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to script file."
        };

        var scriptCommand = new Command("script", "Run a script.");
        scriptCommand.Arguments.Add(pathArgument);
        scriptCommand.SetAction((parseResult, _) => CommandHandler.Script(parseResult.GetValue(pathArgument)));

        return scriptCommand;
    }
}