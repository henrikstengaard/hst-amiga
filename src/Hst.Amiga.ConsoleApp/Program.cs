namespace Hst.Amiga.ConsoleApp;

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

public static class Program
{
    static async Task<int> Main(string[] args)
    {
        // vm commands write program output to standard output, so logging is written to standard error
        var isVmCommand = args.Length > 0 && string.Equals(args[0], "vm", StringComparison.OrdinalIgnoreCase);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(AppState.Instance.LoggingLevelSwitch);
        Log.Logger = (isVmCommand
                ? loggerConfig.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
                : loggerConfig.WriteTo.Console())
            .CreateLogger();

        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var rootCommand = CommandFactory.CreateRootCommand();
        var parseResult = rootCommand.Parse(args);

        var verbose = parseResult.GetValue(CommandFactory.VerboseOption);

        AppState.Instance.LoggingLevelSwitch.MinimumLevel =
            verbose ? LogEventLevel.Debug : LogEventLevel.Information;

        if (!isVmCommand || verbose)
        {
            var appState = AppState.Instance;
            var app =
                $"Hst Amiga v{appState.Version.Major}.{appState.Version.Minor}.{appState.Version.Build} ({appState.BuildDate})";
            var author = "Henrik Noerfjand Stengaard";

            Log.Logger.Information(app);
            Log.Logger.Information(author);
            Log.Logger.Information($"[CMD] {string.Join(" ", args)}");
        }

        return await parseResult.InvokeAsync();
    }
}
