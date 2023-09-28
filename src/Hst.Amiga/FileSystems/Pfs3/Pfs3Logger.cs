namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Threading;

    public sealed class Pfs3Logger : IPfs3Logger
    {
        private IPfs3Logger Logger { get; set; }

        private Pfs3Logger()
        {
            Logger = null;
        }

        private static readonly Lazy<Pfs3Logger> SingletonInstance =
            new Lazy<Pfs3Logger>(() => new Pfs3Logger(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Pfs3Logger Instance => SingletonInstance.Value;
        
        public void RegisterLogger(IPfs3Logger logger)
        {
            Logger = logger;
        }

        public void Debug(string message)
        {
            Logger?.Debug(message);
        }

        public void Information(string message)
        {
            Logger?.Information(message);
        }

        public void Warning(string message)
        {
            Logger?.Warning(message);
        }

        public void Error(string message)
        {
            Logger?.Error(message);
        }

        public void Error(Exception exception, string message)
        {
            Logger?.Error(exception, message);
        }
    }
}