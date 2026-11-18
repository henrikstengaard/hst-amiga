namespace Hst.Amiga.FileSystems.Exceptions
{
    /// <summary>
    /// Exception thrown when mounting a partition, which overlaps a partition already mounted by another volume
    /// on the same stream.
    /// </summary>
    public class PartitionAlreadyMountedException : FileSystemException
    {
        public PartitionAlreadyMountedException(string message) : base(message)
        {
        }
    }
}
