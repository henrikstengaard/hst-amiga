namespace Hst.Amiga.RigidDiskBlocks
{
    public static class BlockSize
    {
        /// <summary>
        /// Rigid disk block size in number of longs (4 bytes)
        /// </summary>
        public const int RigidDiskBlock = 64;

        /// <summary>
        /// Partition block size in number of longs (4 bytes)
        /// </summary>
        public const int PartitionBlock = 64;

        /// <summary>
        /// File system header block size in number of longs (4 bytes)
        /// </summary>
        public const int FileSystemHeaderBlock = 64;
    }
}