namespace Hst.Amiga.FileSystems.Exceptions
{
    public class PathNotFoundException : FileSystemException
    {
        public PathNotFoundException(string message) : base(message)
        {
        }
    }
}