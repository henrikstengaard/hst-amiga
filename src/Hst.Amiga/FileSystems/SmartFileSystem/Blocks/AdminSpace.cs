namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Admin space (struct fsAdminSpace) describing an area of 32 blocks used for metadata blocks.
    /// </summary>
    public class AdminSpace
    {
        /// <summary>
        /// First block of admin space, 0 if unused.
        /// </summary>
        public uint Space { get; set; }

        /// <summary>
        /// Bits for 32 blocks in admin space, bit 31 is first block. Set bits are used blocks.
        /// </summary>
        public uint Bits { get; set; }
    }
}
