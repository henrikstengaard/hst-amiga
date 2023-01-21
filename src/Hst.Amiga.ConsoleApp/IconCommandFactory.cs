namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class IconCommandFactory
{
    public static Command CreateIconCommand()
    {
        var command = new Command("icon", "Icon.");

        command.AddCommand(CreateIconInfo());
        command.AddCommand(CreateIconCreate());
        //command.AddCommand(CreateIconUpdate());

        return command;
    }

    private static Command CreateIconInfo()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to icon file.");

        var allOption = new Option<bool>(
            new[] { "--all", "-a" },
            description: "Display all information.",
            getDefaultValue: () => false);
        
        var command = new Command("info", "Display info about icon file.");
        command.AddAlias("i");
        command.SetHandler(CommandHandler.IconInfo, pathArgument, allOption);
        command.AddArgument(pathArgument);
        command.AddOption(allOption);

        return command;
    }    
    
    private static Command CreateIconCreate()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to icon file.");

        var typeOption = new Option<IconType>(
            new[] { "--type", "-t" },
            description: "Type of icon to create.",
            getDefaultValue: () => IconType.Standard);
        
        var command = new Command("create", "Create icon file.");
        command.AddAlias("c");
        command.SetHandler(CommandHandler.IconCreate, pathArgument, typeOption);
        command.AddArgument(pathArgument);
        command.AddOption(typeOption);

        return command;
    }    
}