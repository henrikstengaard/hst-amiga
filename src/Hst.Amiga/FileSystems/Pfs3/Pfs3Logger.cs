namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public class Pfs3Logger
    {
        public ILogger<Pfs3Logger> Logger { get; private set; }

        private Pfs3Logger()
        {
            Logger = null;
        }

        private static readonly Lazy<Pfs3Logger> SingletonInstance =
            new Lazy<Pfs3Logger>(() => new Pfs3Logger(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Pfs3Logger Instance => SingletonInstance.Value;
        
        public void RegisterLogger(ILogger<Pfs3Logger> logger)
        {
            Logger = logger;
        }

        public void Debug(string message)
        {
            Logger?.LogDebug(message);
        }

        public void Information(string message)
        {
            Logger?.LogInformation(message);
        }

        public void Warning(string message)
        {
            Logger?.LogWarning(message);
        }

        public void Error(string message)
        {
            Logger?.LogError(message);
        }

        public void Error(Exception exception, string message)
        {
            Logger?.LogError(exception, message);
        }
    }
}