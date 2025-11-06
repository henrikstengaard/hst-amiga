using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Hst.Amiga.Roms;

namespace Hst.Amiga.ConsoleApp;

using System;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Serilog;

public static class CommandHandler
{
    private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
        .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
        .BuildServiceProvider();
    
    private static ILogger<T> GetLogger<T>()
    {
        var loggerFactory = ServiceProvider.GetService<ILoggerFactory>();
        return loggerFactory.CreateLogger<T>();
    }
    
    private static async Task Execute(CommandBase command)
    {
        command.DebugMessage += (_, progressMessage) => { Log.Logger.Debug(progressMessage); };
        command.InformationMessage += (_, progressMessage) => { Log.Logger.Information(progressMessage); };

        var cancellationTokenSource = new CancellationTokenSource();
        Result result = null;
        try
        {
            result = await command.Execute(cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Log.Logger.Error(e, $"Failed to execute command '{command.GetType()}'");
            Environment.Exit(1);
        }

        if (result.IsFaulted)
        {
            Log.Logger.Error($"{result.Error}");
            Environment.Exit(1);
        }

        Log.Logger.Information("Done");
    }
    
    public static async Task IconInfo(string path, bool all)
    {
        await Execute(new IconInfoCommand(GetLogger<IconInfoCommand>(), path, all));
    }

    public static async Task IconCreate(string path, IconType type, int? x, int? y, int? stackSize, int? drawerX,
        int? drawerY, int? drawerWidth, int? drawerHeight, DrawerFlags? drawerFlags, DrawerViewModes? drawerViewModes,
        ImageType imageType, string image1Path, string image2Path)
    {
        await Execute(new IconCreateCommand(GetLogger<IconCreateCommand>(), path, type, x, y, stackSize, drawerX,
            drawerY, drawerWidth, drawerHeight, drawerFlags, drawerViewModes, imageType, image1Path, image2Path));
    }
    
    public static async Task IconUpdate(string path, int? type, int? x, int? y, int? stackSize, int? drawerX,
        int? drawerY, int? drawerWidth, int? drawerHeight, DrawerFlags? drawerFlags, DrawerViewModes? drawerViewModes)
    {
        await Execute(new IconUpdateCommand(GetLogger<IconUpdateCommand>(), path, type, x, y, stackSize, drawerX, drawerY,
            drawerWidth, drawerHeight, drawerFlags, drawerViewModes));
    }
    
    public static async Task IconImageExport(string iconPath, ImageType imageType, string image1Path, string image2Path, string jsonPalettePath)
    {
        await Execute(new IconImageExport(GetLogger<IconImageExport>(), iconPath, imageType, image1Path, image2Path, jsonPalettePath));
    }

    public static async Task IconImageConvert(string iconPath, ImageType srcType, ImageType destType, string jsonPalettePath)
    {
        await Execute(new IconImageConvert(GetLogger<IconImageConvert>(), iconPath, srcType, destType, jsonPalettePath));
    }

    public static async Task IconImageDelete(string iconPath, ImageType? imageType)
    {
        await Execute(new IconImageDelete(GetLogger<IconImageDelete>(), iconPath, imageType));
    }
    
    public static async Task IconImageImport(string iconPath, ImageType imageType, string image1Path, string image2Path)
    {
        await Execute(new IconImageImport(GetLogger<IconImageImport>(), iconPath, imageType, image1Path, image2Path));
    }

    public static async Task IconToolTypesExport(string iconPath, string toolTypesPath, bool excludeNewIcon)
    {
        await Execute(new IconToolTypesExport(GetLogger<IconToolTypesExport>(), iconPath, toolTypesPath, excludeNewIcon));
    }
    
    public static async Task IconToolTypesImport(string iconPath, string toolTypesPath, bool preserveNewIcon)
    {
        await Execute(new IconToolTypesImport(GetLogger<IconToolTypesImport>(), iconPath, toolTypesPath, preserveNewIcon));
    }

    public static async Task EpromBuildA500(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild16BitCommand("a500", kickstartRomPath, EpromBuilder.RomIcNameA500,
            epromType, size));
    }

    public static async Task EpromBuildA600(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild16BitCommand("a600", kickstartRomPath, EpromBuilder.RomIcNameA600,
            epromType, size));
    }

    public static async Task EpromBuildA2000(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild16BitCommand("a2000", kickstartRomPath, EpromBuilder.RomIcNameA2000,
            epromType, size));
    }

    public static async Task EpromBuildA1200(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild32BitCommand("a1200", kickstartRomPath, EpromBuilder.HiRomIcNameA1200,
            EpromBuilder.LoRomIcNameA1200, epromType, size));
    }
    
    public static async Task EpromBuildA3000(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild32BitCommand("a3000", kickstartRomPath, EpromBuilder.HiRomIcNameA3000,
            EpromBuilder.LoRomIcNameA3000, epromType, size));
    }

    public static async Task EpromBuildA4000(string kickstartRomPath, EpromType? epromType, int? size)
    {
        await Execute(new EpromBuild32BitCommand("a4000", kickstartRomPath, EpromBuilder.HiRomIcNameA4000,
            EpromBuilder.LoRomIcNameA4000, epromType, size));
    }
    
    public static async Task EpromFill(string kickstartRomPath, EpromType? epromType, int? size, bool? zeroFill)
    {
        await Execute(new EpromFillCommand(kickstartRomPath, epromType, size, zeroFill));
    }

    public static async Task EpromByteSwap(string kickstartRomPath)
    {
        await Execute(new EpromByteSwapCommand(kickstartRomPath));
    }
    
    public static async Task Script(string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        var scriptLines = lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().StartsWith("#"))
            .Select(x => CommandLineStringSplitter.Instance.Split(x)).ToList();

        var rootCommand = CommandFactory.CreateRootCommand();
        foreach (var scriptLine in scriptLines)
        {
            var args = scriptLine.ToArray();

            Log.Logger.Information($"[CMD] {string.Join(" ", args)}");

            if (await rootCommand.InvokeAsync(args) != 0)
            {
                Environment.Exit(1);
            }
        }
    }
}