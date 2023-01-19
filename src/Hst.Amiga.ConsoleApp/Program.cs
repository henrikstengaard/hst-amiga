namespace Hst.Amiga.ConsoleApp;

using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

public static class Program
{
    private static readonly LoggerConfiguration LoggerConfig = new LoggerConfiguration()
        .MinimumLevel.ControlledBy(AppState.Instance.LoggingLevelSwitch)
        .WriteTo.Console();
    
    static async Task<int> Main(string[] args)
    {
        Log.Logger = LoggerConfig
            .CreateLogger();
        
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var rootCommand = CommandFactory.CreateRootCommand();
        var parser = new CommandLineBuilder(rootCommand).AddMiddleware(async (context, next) =>
        {
            AppState.Instance.LoggingLevelSwitch.MinimumLevel = LogEventLevel.Information;

            var appState = AppState.Instance;
            var app =
                $"Hst Imager v{appState.Version.Major}.{appState.Version.Minor}.{appState.Version.Build} ({appState.BuildDate})";
            var author = "Henrik Noerfjand Stengaard";

            Log.Logger.Information(app);
            Log.Logger.Information(author);
            Log.Logger.Information($"[CMD] {string.Join(" ", args)}");

            await next(context);
        }).UseDefaults().Build();
        return await parser.InvokeAsync(args);
    }
}