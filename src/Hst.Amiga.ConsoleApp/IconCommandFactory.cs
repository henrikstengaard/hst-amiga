namespace Hst.Amiga.ConsoleApp;

using System.CommandLine;
using Models;

public static class IconCommandFactory
{
    public static Command CreateIconCommand()
    {
        var command = new Command("icon", "Icon.");

        command.Subcommands.Add(CreateIconInfo());
        command.Subcommands.Add(CreateIconCreate());
        command.Subcommands.Add(CreateIconImage());
        command.Subcommands.Add(CreateIconUpdate());
        command.Subcommands.Add(CreateIconToolTypes());
        command.Subcommands.Add(CreateIconFix());

        return command;
    }

    private static Command CreateIconInfo()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var allOption = new Option<bool>("--all", "-a")
        {
            Description = "Display all information.",
            DefaultValueFactory = _ => false
        };

        var command = new Command("info", "Display info about icon file.");
        command.SetAction((parseResult, _) => CommandHandler.IconInfo(parseResult.GetValue(pathArgument),
            parseResult.GetValue(allOption)));
        command.Arguments.Add(pathArgument);
        command.Options.Add(allOption);

        return command;
    }

    private static Command CreateIconCreate()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var typeArgument = new Argument<IconType>("Type")
        {
            Description = "Type of icon to create."
        };

        var xOption = new Option<int?>("--current-x", "-x")
        {
            Description = "Update x position of icon (0 = auto)."
        };

        var yOption = new Option<int?>("--current-y", "-y")
        {
            Description = "Update y position of icon (0 = auto)."
        };

        var stackSizeOption = new Option<int?>("--stack-size", "-s")
        {
            Description = "Update stack size of icon."
        };

        var drawerXOption = new Option<int?>("--drawer-x", "-dx")
        {
            Description = "Update drawer x position of icon."
        };

        var drawerYOption = new Option<int?>("--drawer-y", "-dy")
        {
            Description = "Update drawer y position of icon."
        };

        var drawerWidthOption = new Option<int?>("--drawer-width", "-dw")
        {
            Description = "Update drawer width of icon."
        };

        var drawerHeightOption = new Option<int?>("--drawer-height", "-dh")
        {
            Description = "Update drawer height of icon."
        };

        var drawerFlagsOption = new Option<DrawerFlags?>("--flags", "-f")
        {
            Description = "Drawer flags.",
            DefaultValueFactory = _ => DrawerFlags.IconsOnly | DrawerFlags.AllFiles
        };

        var drawerViewModesOption = new Option<DrawerViewModes?>("--view-modes", "-v")
        {
            Description = "Drawer view modes.",
            DefaultValueFactory = _ => DrawerViewModes.Os1X
        };

        var imageTypeOption = new Option<ImageType>("--image-type", "-it")
        {
            Description = "Type of icon image to create."
        };

        var image1PathOption = new Option<string>("--image1-path", "-i1")
        {
            Description = "Path to icon image 1."
        };

        var image2PathOption = new Option<string>("--image2-path", "-i2")
        {
            Description = "Path to icon image 2."
        };

        var command = new Command("create", "Create icon file.");

        command.SetAction(async (parseResult, _) =>
        {
            var path = parseResult.GetValue(pathArgument);
            var type = parseResult.GetValue(typeArgument);
            var x = parseResult.GetValue(xOption);
            var y = parseResult.GetValue(yOption);
            var stackSize = parseResult.GetValue(stackSizeOption);
            var drawerX = parseResult.GetValue(drawerXOption);
            var drawerY = parseResult.GetValue(drawerYOption);
            var drawerWidth = parseResult.GetValue(drawerWidthOption);
            var drawerHeight = parseResult.GetValue(drawerHeightOption);
            var drawerFlags = parseResult.GetValue(drawerFlagsOption);
            var drawerViewModes = parseResult.GetValue(drawerViewModesOption);
            var imageType = parseResult.GetValue(imageTypeOption);
            var image1Path = parseResult.GetValue(image1PathOption);
            var image2Path = parseResult.GetValue(image2PathOption);

            await CommandHandler.IconCreate(path, type, x, y, stackSize, drawerX, drawerY, drawerWidth, drawerHeight,
                drawerFlags, drawerViewModes, imageType, image1Path, image2Path);
        });

        command.Arguments.Add(pathArgument);
        command.Arguments.Add(typeArgument);
        command.Options.Add(xOption);
        command.Options.Add(yOption);
        command.Options.Add(stackSizeOption);
        command.Options.Add(drawerXOption);
        command.Options.Add(drawerYOption);
        command.Options.Add(drawerWidthOption);
        command.Options.Add(drawerHeightOption);
        command.Options.Add(drawerFlagsOption);
        command.Options.Add(drawerViewModesOption);
        command.Options.Add(imageTypeOption);
        command.Options.Add(image1PathOption);
        command.Options.Add(image2PathOption);

        return command;
    }

    private static Command CreateIconUpdate()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var typeOption = new Option<int?>("--type", "-t")
        {
            Description = "Update icon type."
        };

        var xOption = new Option<int?>("--current-x", "-x")
        {
            Description = "Update x position of icon (0 = auto)."
        };

        var yOption = new Option<int?>("--current-y", "-y")
        {
            Description = "Update y position of icon (0 = auto)."
        };

        var stackSizeOption = new Option<int?>("--stack-size", "-s")
        {
            Description = "Update stack size of icon."
        };

        var drawerXOption = new Option<int?>("--drawer-x", "-dx")
        {
            Description = "Update drawer x position of icon."
        };

        var drawerYOption = new Option<int?>("--drawer-y", "-dy")
        {
            Description = "Update drawer y position of icon."
        };

        var drawerWidthOption = new Option<int?>("--drawer-width", "-dw")
        {
            Description = "Update drawer width of icon."
        };

        var drawerHeightOption = new Option<int?>("--drawer-height", "-dh")
        {
            Description = "Update drawer height of icon."
        };

        var drawerFlagsOption = new Option<DrawerFlags?>("--flags", "-f")
        {
            Description = "Drawer flags.",
            DefaultValueFactory = _ => DrawerFlags.IconsOnly | DrawerFlags.AllFiles
        };

        var drawerViewModesOption = new Option<DrawerViewModes?>("--view-modes", "-v")
        {
            Description = "Drawer view mode.",
            DefaultValueFactory = _ => DrawerViewModes.Os1X
        };

        var command = new Command("update", "Update icon file.");

        command.SetAction(async (parseResult, _) =>
        {
            var path = parseResult.GetValue(pathArgument);
            var type = parseResult.GetValue(typeOption);
            var x = parseResult.GetValue(xOption);
            var y = parseResult.GetValue(yOption);
            var stackSize = parseResult.GetValue(stackSizeOption);
            var drawerX = parseResult.GetValue(drawerXOption);
            var drawerY = parseResult.GetValue(drawerYOption);
            var drawerWidth = parseResult.GetValue(drawerWidthOption);
            var drawerHeight = parseResult.GetValue(drawerHeightOption);
            var drawerFlags = parseResult.GetValue(drawerFlagsOption);
            var drawerViewModes = parseResult.GetValue(drawerViewModesOption);

            await CommandHandler.IconUpdate(path, type, x, y, stackSize, drawerX, drawerY, drawerWidth, drawerHeight,
                drawerFlags, drawerViewModes);
        });

        command.Arguments.Add(pathArgument);
        command.Options.Add(typeOption);
        command.Options.Add(xOption);
        command.Options.Add(yOption);
        command.Options.Add(stackSizeOption);
        command.Options.Add(drawerXOption);
        command.Options.Add(drawerYOption);
        command.Options.Add(drawerWidthOption);
        command.Options.Add(drawerHeightOption);
        command.Options.Add(drawerFlagsOption);
        command.Options.Add(drawerViewModesOption);

        return command;
    }

    private static Command CreateIconImage()
    {
        var command = new Command("image", "Icon image.");

        command.Subcommands.Add(CreateIconImageConvert());
        command.Subcommands.Add(CreateIconImageDelete());
        command.Subcommands.Add(CreateIconImageImport());
        command.Subcommands.Add(CreateIconImageExport());

        return command;
    }

    private static Command CreateIconImageConvert()
    {
        var iconPathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var srcTypeArgument = new Argument<ImageType>("SrcType")
        {
            Description = "Source type of icon image to convert from."
        };

        var destTypeArgument = new Argument<ImageType>("DestType")
        {
            Description = "Destination type of icon image to convert to."
        };

        var jsonPalettePathOption = new Option<string>("--palette-path", "-p")
        {
            Description = "Path to JSON palette for converting planar images."
        };

        var deleteIconsOption = new Option<bool>("--delete-icons", "-d")
        {
            Description = "Delete icons when converted preserving only destination type."
        };

        var command = new Command("convert", "Convert icon image.");
        command.SetAction((parseResult, _) => CommandHandler.IconImageConvert(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(srcTypeArgument),
            parseResult.GetValue(destTypeArgument), parseResult.GetValue(jsonPalettePathOption),
            parseResult.GetValue(deleteIconsOption)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(srcTypeArgument);
        command.Arguments.Add(destTypeArgument);
        command.Options.Add(jsonPalettePathOption);
        command.Options.Add(deleteIconsOption);

        return command;
    }

    private static Command CreateIconImageDelete()
    {
        var iconPathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var imageTypeArgument = new Argument<ImageType?>("ImageType")
        {
            Description = "Type of icon image to delete.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("delete", "Delete icon image.");

        command.SetAction((parseResult, _) => CommandHandler.IconImageDelete(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(imageTypeArgument)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(imageTypeArgument);

        return command;
    }

    private static Command CreateIconImageImport()
    {
        var iconPathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var imageTypeArgument = new Argument<ImageType>("Type")
        {
            Description = "Type of icon image to import."
        };

        var image1PathOption = new Option<string>("--image1-path", "-i1")
        {
            Description = "Path to icon image 1."
        };

        var image2PathOption = new Option<string>("--image2-path", "-i2")
        {
            Description = "Path to icon image 2."
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force icon image import."
        };

        var command = new Command("import", "Import icon image.");
        command.SetAction((parseResult, _) => CommandHandler.IconImageImport(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(imageTypeArgument),
            parseResult.GetValue(image1PathOption), parseResult.GetValue(image2PathOption),
            parseResult.GetValue(forceOption)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(imageTypeArgument);
        command.Options.Add(image1PathOption);
        command.Options.Add(image2PathOption);
        command.Options.Add(forceOption);

        return command;
    }

    private static Command CreateIconImageExport()
    {
        var iconPathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var imageTypeArgument = new Argument<ImageType>("Type")
        {
            Description = "Type of icon image to export."
        };

        var image1PathOption = new Option<string>("--image1-path", "-i1")
        {
            Description = "Path to icon image 1."
        };

        var image2PathOption = new Option<string>("--image2-path", "-i2")
        {
            Description = "Path to icon image 2."
        };

        var jsonPalettePathOption = new Option<string>("--palette-path", "-p")
        {
            Description = "Path to JSON palette for exporting planar images."
        };

        var command = new Command("export", "Export icon image.");
        command.SetAction((parseResult, _) => CommandHandler.IconImageExport(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(imageTypeArgument),
            parseResult.GetValue(image1PathOption), parseResult.GetValue(image2PathOption),
            parseResult.GetValue(jsonPalettePathOption)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(imageTypeArgument);
        command.Options.Add(image1PathOption);
        command.Options.Add(image2PathOption);
        command.Options.Add(jsonPalettePathOption);

        return command;
    }

    private static Command CreateIconToolTypes()
    {
        var command = new Command("tooltypes", "Icon tool types.");

        command.Subcommands.Add(CreateIconToolTypesExport());
        command.Subcommands.Add(CreateIconToolTypesImport());

        return command;
    }

    private static Command CreateIconToolTypesExport()
    {
        var iconPathArgument = new Argument<string>("IconPath")
        {
            Description = "Path to icon file."
        };

        var toolTypesPathArgument = new Argument<string>("ToolTypesPath")
        {
            Description = "Path to tool types file for exporting."
        };

        var excludeNewIconOption = new Option<bool>("--exclude-newicon", "-xn")
        {
            Description = "Exclude new icon from tool types."
        };

        var command = new Command("export", "Export icon tool types.");
        command.SetAction((parseResult, _) => CommandHandler.IconToolTypesExport(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(toolTypesPathArgument),
            parseResult.GetValue(excludeNewIconOption)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(toolTypesPathArgument);
        command.Options.Add(excludeNewIconOption);

        return command;
    }

    private static Command CreateIconToolTypesImport()
    {
        var iconPathArgument = new Argument<string>("IconPath")
        {
            Description = "Path to icon file."
        };

        var toolTypesPathArgument = new Argument<string>("ToolTypesPath")
        {
            Description = "Path to tool types file for importing."
        };

        var preserveNewIconOption = new Option<bool>("--preserve-newicon", "-pn")
        {
            Description = "Preserve new icon in icon tool types."
        };

        var command = new Command("import", "Import icon tool types.");
        command.SetAction((parseResult, _) => CommandHandler.IconToolTypesImport(
            parseResult.GetValue(iconPathArgument), parseResult.GetValue(toolTypesPathArgument),
            parseResult.GetValue(preserveNewIconOption)));

        command.Arguments.Add(iconPathArgument);
        command.Arguments.Add(toolTypesPathArgument);
        command.Options.Add(preserveNewIconOption);

        return command;
    }

    private static Command CreateIconFix()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to icon file."
        };

        var command = new Command("fix", "Fix icon file.");
        command.SetAction((parseResult, _) => CommandHandler.IconFix(parseResult.GetValue(pathArgument)));
        command.Arguments.Add(pathArgument);

        return command;
    }
}