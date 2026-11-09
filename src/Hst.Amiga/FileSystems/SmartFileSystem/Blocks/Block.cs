namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Base block with block header (struct fsBlockHeader) used by all smart file system blocks
    /// except data blocks.
    /// </summary>
    public abstract class Block
    {
        /// <summary>
        /// Id identifying the type of block, e.g. "OBJC".
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// Checksum of block.
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// Block number of this block relative to start of partition.
        /// </summary>
        public uint OwnBlock { get; set; }

        protected Block(uint id)
        {
            Id = id;
        }
    }
}
