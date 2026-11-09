namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Extent B-tree node (struct fsExtentBNode) in leaf B-tree node containers describing a range of
    /// continuous data blocks used by a file.
    /// </summary>
    public class ExtentBNode : BNode
    {
        /// <summary>
        /// Key of next extent (first block), 0 if last extent of file.
        /// </summary>
        public uint Next
        {
            get => Data;
            set => Data = value;
        }

        /// <summary>
        /// Key of previous extent. For first extent of a file, this is the object node of file with bit 31 set.
        /// </summary>
        public uint Prev { get; set; }

        /// <summary>
        /// Number of blocks in extent.
        /// </summary>
        public ushort Blocks { get; set; }
    }
}
