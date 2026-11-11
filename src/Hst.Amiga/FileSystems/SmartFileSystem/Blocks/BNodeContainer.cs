namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System.Collections.Generic;

    /// <summary>
    /// B-tree node container block (struct fsBNodeContainer) part of extent B-tree.
    /// Leaf containers contains extent B-tree nodes, internal containers contains B-tree nodes pointing
    /// to child containers. Nodes are sorted by key.
    /// </summary>
    public class BNodeContainer : Block
    {
        public bool IsLeaf { get; set; }

        /// <summary>
        /// Size of each node in bytes, 14 for leaf and 8 for internal containers.
        /// </summary>
        public byte NodeSize { get; set; }

        public List<BNode> Nodes { get; set; }

        public BNodeContainer() : base(Constants.BNodeContainerId)
        {
            Nodes = new List<BNode>();
        }

        public static BNodeContainer CreateLeaf() => new BNodeContainer
        {
            IsLeaf = true,
            NodeSize = Constants.ExtentBNodeSize
        };

        public static BNodeContainer CreateInternal() => new BNodeContainer
        {
            IsLeaf = false,
            NodeSize = Constants.BNodeSize
        };

        /// <summary>
        /// Max number of nodes in container.
        /// </summary>
        public int Capacity(int blockSize) => (blockSize - Constants.BNodeContainerHeaderSize) / NodeSize;
    }
}
