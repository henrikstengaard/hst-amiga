namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;

public static class IconCommandFactory
{
    public static Command CreateIconCommand()
    {
        var command = new Command("icon", "Icon.");

        command.AddCommand(CreateIconInfo());
        command.AddCommand(CreateIconCreate());
        command.AddCommand(CreateIconImage());
        command.AddCommand(CreateIconUpdate());
        command.AddCommand(CreateIconToolTypes());

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

        var typeArgument = new Argument<IconType>(
            name: "Type",
            description: "Type of icon to create.");
        
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

        var imageTypeOption = new Option<ImageType>(
            new[] { "--image-type", "-it" },
            description: "Type of icon image to create.");
        
        var image1PathOption = new Option<string>(
            new[] { "--image1-path", "-i1" },
            description: "Path to icon image 1.");

        var image2PathOption = new Option<string>(
            new[] { "--image2-path", "-i2" },
            description: "Path to icon image 2.");
        
        var command = new Command("create", "Create icon file.");
        
        command.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var type = context.ParseResult.GetValueForArgument(typeArgument);
            var x = context.ParseResult.GetValueForOption(xOption);
            var y = context.ParseResult.GetValueForOption(yOption);
            var stackSize = context.ParseResult.GetValueForOption(stackSizeOption);
            var drawerX = context.ParseResult.GetValueForOption(drawerXOption);
            var drawerY = context.ParseResult.GetValueForOption(drawerYOption);
            var drawerWidth = context.ParseResult.GetValueForOption(drawerWidthOption);
            var drawerHeight = context.ParseResult.GetValueForOption(drawerHeightOption);
            var imageType = context.ParseResult.GetValueForOption(imageTypeOption);
            var image1Path = context.ParseResult.GetValueForOption(image1PathOption);
            var image2Path = context.ParseResult.GetValueForOption(image2PathOption);

            await CommandHandler.IconCreate(path, type, x, y, stackSize, drawerX, drawerY, drawerWidth, drawerHeight,
                imageType, image1Path, image2Path);
        });
        
        command.AddArgument(pathArgument);
        command.AddArgument(typeArgument);
        command.AddOption(xOption);
        command.AddOption(yOption);
        command.AddOption(stackSizeOption);
        command.AddOption(drawerXOption);
        command.AddOption(drawerYOption);
        command.AddOption(drawerWidthOption);
        command.AddOption(drawerHeightOption);
        command.AddOption(imageTypeOption);
        command.AddOption(image1PathOption);
        command.AddOption(image2PathOption);
        
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

    private static Command CreateIconImage()
    {
        var command = new Command("image", "Icon image.");

        command.AddCommand(CreateIconImageImport());
        command.AddCommand(CreateIconImageExport());

        return command;
    }
    
    private static Command CreateIconImageImport()
    {
        var iconPathArgument = new Argument<string>(
            name: "Path",
            description: "Path to icon file.");

        var imageTypeArgument = new Argument<ImageType>(
            name: "Type",
            description: "Type of icon image to import.");
        
        var image1PathOption = new Option<string>(
            new[] { "--image1-path", "-i1" },
            description: "Path to icon image 1.");

        var image2PathOption = new Option<string>(
            new[] { "--image2-path", "-i2" },
            description: "Path to icon image 2.");
        
        var command = new Command("import", "Import icon image.");
        command.SetHandler(CommandHandler.IconImageImport, iconPathArgument, imageTypeArgument, image1PathOption, image2PathOption);
        
        command.AddArgument(iconPathArgument);
        command.AddArgument(imageTypeArgument);
        command.AddOption(image1PathOption);
        command.AddOption(image2PathOption);
        
        return command;
    }
    
    private static Command CreateIconImageExport()
    {
        var iconPathArgument = new Argument<string>(
            name: "Path",
            description: "Path to icon file.");

        var imageTypeArgument = new Argument<ImageType>(
            name: "Type",
            description: "Type of icon image to export.");
        
        var image1PathOption = new Option<string>(
            new[] { "--image1-path", "-i1" },
            description: "Path to icon image 1.");

        var image2PathOption = new Option<string>(
            new[] { "--image2-path", "-i2" },
            description: "Path to icon image 2.");

        var jsonPalettePathOption = new Option<string>(
            new[] { "--palette-path", "-p" },
            description: "Path to JSON palette for exporting planar images.");
        
        var command = new Command("export", "Export icon image.");
        command.SetHandler(CommandHandler.IconImageExport, iconPathArgument, imageTypeArgument, image1PathOption, image2PathOption, jsonPalettePathOption);
        
        command.AddArgument(iconPathArgument);
        command.AddArgument(imageTypeArgument);
        command.AddOption(image1PathOption);
        command.AddOption(image2PathOption);
        command.AddOption(jsonPalettePathOption);
        
        return command;
    }
    
    private static Command CreateIconToolTypes()
    {
        var command = new Command("tooltypes", "Icon tool types.");

        command.AddCommand(CreateIconToolTypesExport());
        command.AddCommand(CreateIconToolTypesImport());

        return command;
    }

    private static Command CreateIconToolTypesExport()
    {
        var iconPathArgument = new Argument<string>(
            name: "IconPath",
            description: "Path to icon file.");

        var toolTypesPathArgument = new Argument<string>(
            name: "ToolTypesPath",
            description: "Path to tool types file for exporting.");
        
        var excludeNewIconOption = new Option<bool>(
            new[] { "--exclude-newicon", "-xn" },
            description: "Exclude new icon from tool types.");
        
        var command = new Command("export", "Export icon tool types.");
        command.SetHandler(CommandHandler.IconToolTypesExport, iconPathArgument, toolTypesPathArgument, excludeNewIconOption);
        
        command.AddArgument(iconPathArgument);
        command.AddArgument(toolTypesPathArgument);
        command.AddOption(excludeNewIconOption);
        
        return command;
    }

    private static Command CreateIconToolTypesImport()
    {
        var iconPathArgument = new Argument<string>(
            name: "IconPath",
            description: "Path to icon file.");

        var toolTypesPathArgument = new Argument<string>(
            name: "ToolTypesPath",
            description: "Path to tool types file for importing.");

        var preserveNewIconOption = new Option<bool>(
            new[] { "--preserve-newicon", "-pn" },
            description: "Preserve new icon in icon tool types.");
        
        var command = new Command("import", "Import icon tool types.");
        command.SetHandler(CommandHandler.IconToolTypesImport, iconPathArgument, toolTypesPathArgument, preserveNewIconOption);
        
        command.AddArgument(iconPathArgument);
        command.AddArgument(toolTypesPathArgument);
        command.AddOption(preserveNewIconOption);
        
        return command;
    }
}