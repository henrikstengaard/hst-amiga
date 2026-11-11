namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;

    /// <summary>
    /// Root info (struct fsRootInfo) stored in the last 36 bytes of the root object container block.
    /// </summary>
    public class RootInfo
    {
        /// <summary>
        /// Number of blocks used by deleted files in recycled directory.
        /// </summary>
        public uint DeletedBlocks { get; set; }

        /// <summary>
        /// Number of deleted files in recycled directory.
        /// </summary>
        public uint DeletedFiles { get; set; }

        /// <summary>
        /// Number of free blocks, must match number of set bits in bitmap.
        /// </summary>
        public uint FreeBlocks { get; set; }

        public DateTime DateCreated { get; set; }
        public uint LastAllocatedBlock { get; set; }
        public uint LastAllocatedAdminSpace { get; set; }
        public uint LastAllocatedExtentNode { get; set; }
        public uint LastAllocatedObjectNode { get; set; }
        public uint RovingPointer { get; set; }
    }
}
