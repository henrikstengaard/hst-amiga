using System;

namespace Hst.Amiga.RigidDiskBlocks.Exceptions
{
    public class FileSystemHeaderBlockException : RigidDiskBlockException
    {
        public FileSystemHeaderBlockException(string message)
            : base(message)
        {
        }
        
        public FileSystemHeaderBlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}