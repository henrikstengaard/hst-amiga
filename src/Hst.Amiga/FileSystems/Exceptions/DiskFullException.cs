namespace Hst.Amiga.FileSystems.Exceptions
{
    public class DiskFullException : FileSystemException
    {
        public DiskFullException(string message) : base(message)
        {
        }
    }
}