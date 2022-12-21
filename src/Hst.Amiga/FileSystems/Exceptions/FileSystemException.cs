namespace Hst.Amiga.FileSystems.Exceptions
{
    using System;

    public class FileSystemException : Exception
    {
        public FileSystemException(string message) : base(message)
        {
        }
        
        public FileSystemException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}