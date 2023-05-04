namespace Hst.Amiga.ConsoleApp;

using System;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        int? drawerY, int? drawerWidth, int? drawerHeight, ImageType imageType, string image1Path, string image2Path)
    {
        await Execute(new IconCreateCommand(GetLogger<IconCreateCommand>(), path, type, x, y, stackSize, drawerX,
            drawerY, drawerWidth, drawerHeight, imageType, image1Path, image2Path));
    }
    
    public static async Task IconUpdate(string path, int? type, int? x, int? y, int? stackSize, int? drawerX,
        int? drawerY, int? drawerWidth, int? drawerHeight)
    {
        await Execute(new IconUpdateCommand(GetLogger<IconUpdateCommand>(), path, type, x, y, stackSize, drawerX, drawerY,
            drawerWidth, drawerHeight));
    }
    
    public static async Task IconImageExport(string iconPath, ImageType imageType, string image1Path, string image2Path, string jsonPalettePath)
    {
        await Execute(new IconImageExport(GetLogger<IconImageExport>(), iconPath, imageType, image1Path, image2Path, jsonPalettePath));
    }

    public static async Task IconImageConvert(string iconPath, ImageType srcType, ImageType destType, string jsonPalettePath)
    {
        await Execute(new IconImageConvert(GetLogger<IconImageConvert>(), iconPath, srcType, destType, jsonPalettePath));
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
}