namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;

    public class InMemoryPfs3Logger : IPfs3Logger
    {
        public readonly IList<string> Messages;

        public InMemoryPfs3Logger()
        {
            Messages = new List<string>();
        }
    
        public void Debug(string message)
        {
            Messages.Add($"[DGB] {message}");
        }

        public void Information(string message)
        {
            Messages.Add($"[INF] {message}");
        }

        public void Warning(string message)
        {
            Messages.Add($"[WRN] {message}");
        }

        public void Error(string message)
        {
            Messages.Add($"[ERR] {message}");
        }

        public void Error(Exception exception, string message)
        {
            Messages.Add($"[ERR] {message}: {exception}");
        }
    }    
}