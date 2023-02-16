namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class IconCommandFactory
{
    public static Command CreateIconCommand()
    {
        var command = new Command("icon", "Icon.");

        command.AddCommand(CreateIconInfo());
        //command.AddCommand(CreateIconCreate());
        command.AddCommand(CreateIconUpdate());

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
    
    private static Command CreateIconUpdate()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to icon file.");

        var typeOption = new Option<int?>(
            new[] { "--type", "-t" },
            description: "Update icon type.");

        var xOption = new Option<int?>(
            new[] { "--current-x", "-x" },
            description: "Update x position of icon.");

        var yOption = new Option<int?>(
            new[] { "--current-y", "-y" },
            description: "Update y position of icon.");

        var stackSizeOption = new Option<int?>(
            new[] { "--stack-size", "-s" },
            description: "Update stack size of icon.");

        var drawerXOption = new Option<int?>(
            new[] { "--drawer-x", "-dx" },
            description: "Update drawer x position of icon.");

        var drawerYOption = new Option<int?>(
            new[] { "--drawer-y", "-dy" },
            description: "Update drawer y position of icon.");

        var drawerWidthOption = new Option<int?>(
            new[] { "--drawer-width", "-dw" },
            description: "Update drawer width of icon.");

        var drawerHeightOption = new Option<int?>(
            new[] { "--drawer-height", "-dh" },
            description: "Update drawer height of icon.");
        
        var command = new Command("update", "Update icon file.");
        command.AddAlias("u");
        
        command.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var type = context.ParseResult.GetValueForOption(typeOption);
            var x = context.ParseResult.GetValueForOption(xOption);
            var y = context.ParseResult.GetValueForOption(yOption);
            var stackSize = context.ParseResult.GetValueForOption(stackSizeOption);
            var drawerX = context.ParseResult.GetValueForOption(drawerXOption);
            var drawerY = context.ParseResult.GetValueForOption(drawerYOption);
            var drawerWidth = context.ParseResult.GetValueForOption(drawerWidthOption);
            var drawerHeight = context.ParseResult.GetValueForOption(drawerHeightOption);

            await CommandHandler.IconUpdate(path, type, x, y, stackSize, drawerX, drawerY, drawerWidth, drawerHeight);
        });
        
        command.AddArgument(pathArgument);
        command.AddOption(typeOption);
        command.AddOption(xOption);
        command.AddOption(yOption);
        command.AddOption(stackSizeOption);
        command.AddOption(drawerXOption);
        command.AddOption(drawerYOption);
        command.AddOption(drawerWidthOption);
        command.AddOption(drawerHeightOption);

        return command;
    }    
}