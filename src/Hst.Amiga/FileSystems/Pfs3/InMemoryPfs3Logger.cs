namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;

    public class InMemoryPfs3Logger : ILogger<Pfs3Logger>
    {
        public readonly IList<string> Messages;

        public InMemoryPfs3Logger()
        {
            Messages = new List<string>();
        }
    
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Messages.Add($"[{eventId.Id,2}: {logLevel,-12}] {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }    
}

