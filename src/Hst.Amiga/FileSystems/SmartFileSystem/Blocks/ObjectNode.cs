namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Object node (struct fsObjectNode) stored in leaf node container.
    /// </summary>
    public class ObjectNode
    {
        /// <summary>
        /// Object container block containing object, 0 if node is free and 0xffffffff if node is reserved.
        /// </summary>
        public uint Data { get; set; }

        /// <summary>
        /// Next object node in hash chain, 0 if last.
        /// </summary>
        public uint Next { get; set; }

        /// <summary>
        /// 16-bit hash of object name.
        /// </summary>
        public ushort Hash16 { get; set; }
    }
}
