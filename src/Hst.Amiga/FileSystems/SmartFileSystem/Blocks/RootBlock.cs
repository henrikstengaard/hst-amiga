namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;

    /// <summary>
    /// Root block (struct fsRootBlock) located at first and last block of partition.
    /// </summary>
    public class RootBlock : Block
    {
        public ushort Version { get; set; }
        public ushort SequenceNumber { get; set; }
        public DateTime DateCreated { get; set; }
        public byte Bits { get; set; }

        /// <summary>
        /// Absolute byte offset of start of partition on disk.
        /// </summary>
        public ulong FirstByte { get; set; }

        /// <summary>
        /// Absolute byte offset of end of partition on disk (exclusive).
        /// </summary>
        public ulong LastByte { get; set; }

        public uint TotalBlocks { get; set; }
        public uint BlockSize { get; set; }
        public uint BitmapBase { get; set; }
        public uint AdminSpaceContainer { get; set; }
        public uint RootObjectContainer { get; set; }
        public uint ExtentBNodeRoot { get; set; }
        public uint ObjectNodeRoot { get; set; }

        public bool CaseSensitive => (Bits & Constants.RootBitsCaseSensitive) != 0;
        public bool Recycled => (Bits & Constants.RootBitsRecycled) != 0;

        public RootBlock() : base(Constants.RootBlockId)
        {
            Version = Constants.StructureVersion;
        }
    }
}
