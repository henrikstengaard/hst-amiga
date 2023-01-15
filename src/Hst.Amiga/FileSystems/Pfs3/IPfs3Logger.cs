namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;

    public interface IPfs3Logger
    {
        void Debug(string message);
        void Information(string message);
        void Warning(string message);
        void Error(string message);
        void Error(Exception exception, string message);
    }
}