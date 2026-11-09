namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Admin space container block (struct fsAdminSpaceContainer) keeping track of admin spaces,
    /// which are areas of 32 blocks used for metadata blocks.
    /// </summary>
    public class AdminSpaceContainer : Block
    {
        public uint Next { get; set; }
        public uint Previous { get; set; }

        /// <summary>
        /// Number of blocks per admin space, always 32.
        /// </summary>
        public byte Bits { get; set; }

        public AdminSpace[] AdminSpaces { get; set; }

        public AdminSpaceContainer(int blockSize) : base(Constants.AdminSpaceContainerId)
        {
            Bits = Constants.BlocksAdmin;
            AdminSpaces = new AdminSpace[Capacity(blockSize)];
            for (var i = 0; i < AdminSpaces.Length; i++)
            {
                AdminSpaces[i] = new AdminSpace();
            }
        }

        public static int Capacity(int blockSize) =>
            (blockSize - Constants.AdminSpaceContainerHeaderSize) / Constants.AdminSpaceSize;
    }
}
