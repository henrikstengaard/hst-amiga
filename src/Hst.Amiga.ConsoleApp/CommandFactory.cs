namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class CommandFactory
{
    public static Command CreateRootCommand()
    {
        var rootCommand = new RootCommand
        {
            Description = "Hst Amiga."
        };

        rootCommand.AddCommand(IconCommandFactory.CreateIconCommand());

        return rootCommand;
    }
}