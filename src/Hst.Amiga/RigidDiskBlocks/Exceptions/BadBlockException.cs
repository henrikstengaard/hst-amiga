using System;

namespace Hst.Amiga.RigidDiskBlocks.Exceptions
{
    public class BadBlockException : RigidDiskBlockException
    {
        public BadBlockException(string message)
            : base(message)
        {
        }
        
        public BadBlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}