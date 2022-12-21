namespace Hst.Amiga.FileSystems.Exceptions
{
    public class DirectoryNotEmptyException : FileSystemException
    {
        public DirectoryNotEmptyException(string message) : base(message)
        {
        }
    }
}