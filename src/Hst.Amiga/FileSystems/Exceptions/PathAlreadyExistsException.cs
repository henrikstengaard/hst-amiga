namespace Hst.Amiga.FileSystems.Exceptions
{
    public class PathAlreadyExistsException : FileSystemException
    {
        public PathAlreadyExistsException(string message) : base(message)
        {
        }
    }
}