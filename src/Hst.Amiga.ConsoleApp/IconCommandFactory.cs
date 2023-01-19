namespace Hst.Amiga.ConsoleApp;

using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

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

        var command = new Command("info", "Display info about icon file.");
        command.AddAlias("i");
        command.SetHandler(CommandHandler.IconInfo, pathArgument);
        command.AddArgument(pathArgument);

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
    
    public static async Task IconInfo(string path)
    {
        await Execute(new IconInfoCommand(GetLogger<IconInfoCommand>(), path));
    }

    public static async Task IconCreate(string path, IconType type)
    {
        await Execute(new IconCreateCommand(GetLogger<IconCreateCommand>(), path, type));
    }
}