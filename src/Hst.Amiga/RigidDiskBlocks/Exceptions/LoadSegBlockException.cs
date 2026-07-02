using System;

namespace Hst.Amiga.RigidDiskBlocks.Exceptions
{
    public class LoadSegBlockException : RigidDiskBlockException
    {
        public LoadSegBlockException(string message)
            : base(message)
        {
        }
        
        public LoadSegBlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}