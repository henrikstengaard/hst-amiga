using System;

namespace Hst.Amiga.RigidDiskBlocks.Exceptions
{
    public class RigidDiskBlockException : Exception
    {
        public RigidDiskBlockException(string message)
            : base(message)
        {
        }
        
        public RigidDiskBlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}