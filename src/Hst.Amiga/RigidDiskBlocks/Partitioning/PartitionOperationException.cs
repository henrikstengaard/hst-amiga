namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;

    /// <summary>
    /// Exception thrown when a partition can't be moved or resized, e.g. not enough free space.
    /// </summary>
    public class PartitionOperationException : Exception
    {
        public PartitionOperationException(string message) : base(message)
        {
        }
    }
}
