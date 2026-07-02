using System;

namespace Hst.Amiga.RigidDiskBlocks.Exceptions
{
    public class PartitionBlockException : RigidDiskBlockException
    {
        public PartitionBlockException(string message)
            : base(message)
        {
        }
        
        public PartitionBlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}