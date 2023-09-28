namespace Hst.Amiga.FileSystems.Exceptions
{
    /// <summary>
    /// not a file exception indicates a path was expected to be a file, but was not
    /// </summary>
    public class NotAFileException : FileSystemException
    {
        public NotAFileException(string message) : base(message)
        {
        }
    }
}