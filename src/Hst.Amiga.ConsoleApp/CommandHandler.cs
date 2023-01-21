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

    public static async Task IconCreate(string path, IconType type)
    {
        await Execute(new IconCreateCommand(GetLogger<IconCreateCommand>(), path, type));
    }
}