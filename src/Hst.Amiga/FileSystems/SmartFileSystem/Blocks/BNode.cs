namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// B-tree node (struct BNode) in internal (non-leaf) B-tree node containers.
    /// </summary>
    public class BNode
    {
        /// <summary>
        /// Key of node. For internal nodes, the first key in child B-tree node container.
        /// </summary>
        public uint Key { get; set; }

        /// <summary>
        /// For internal nodes, block of child B-tree node container.
        /// </summary>
        public uint Data { get; set; }

        public virtual BNode Clone() => (BNode)MemberwiseClone();
    }
}
