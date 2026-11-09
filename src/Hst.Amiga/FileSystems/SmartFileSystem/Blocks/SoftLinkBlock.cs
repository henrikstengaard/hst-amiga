namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Soft link block (struct fsSoftLink) containing path of soft link.
    /// </summary>
    public class SoftLinkBlock : Block
    {
        /// <summary>
        /// Object node of soft link object.
        /// </summary>
        public uint Parent { get; set; }

        public uint Next { get; set; }
        public uint Previous { get; set; }

        /// <summary>
        /// Path soft link points to.
        /// </summary>
        public string Path { get; set; }

        public SoftLinkBlock() : base(Constants.SoftLinkId)
        {
            Path = string.Empty;
        }
    }
}
