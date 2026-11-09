namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Node container block (struct fsNodeContainer) part of object node tree.
    /// A leaf node container (Nodes = 1) contains object nodes, otherwise it's an index node container
    /// with pointers to child node containers.
    /// </summary>
    public class NodeContainer : Block
    {
        /// <summary>
        /// Node number of first node covered by this node container.
        /// </summary>
        public uint NodeNumber { get; set; }

        /// <summary>
        /// Number of nodes covered by each entry, 1 for a leaf node container.
        /// </summary>
        public uint Nodes { get; set; }

        /// <summary>
        /// Object nodes, only used for leaf node container.
        /// </summary>
        public ObjectNode[] ObjectNodes { get; set; }

        /// <summary>
        /// Pointers to child node containers, only used for index node container.
        /// Each pointer is block shifted left by (log2(block size) - 5) with bit 0 set when child is full.
        /// </summary>
        public uint[] Pointers { get; set; }

        public bool IsLeaf => Nodes == 1;

        public NodeContainer() : base(Constants.NodeContainerId)
        {
            ObjectNodes = System.Array.Empty<ObjectNode>();
            Pointers = System.Array.Empty<uint>();
        }

        public static int LeafCapacity(int blockSize) =>
            (blockSize - Constants.NodeContainerHeaderSize) / Constants.ObjectNodeSize;

        public static int IndexCapacity(int blockSize) => (blockSize - Constants.NodeContainerHeaderSize) / 4;

        public static NodeContainer CreateLeaf(int blockSize, uint nodeNumber)
        {
            var nodeContainer = new NodeContainer
            {
                NodeNumber = nodeNumber,
                Nodes = 1,
                ObjectNodes = new ObjectNode[LeafCapacity(blockSize)]
            };
            for (var i = 0; i < nodeContainer.ObjectNodes.Length; i++)
            {
                nodeContainer.ObjectNodes[i] = new ObjectNode();
            }

            return nodeContainer;
        }

        public static NodeContainer CreateIndex(int blockSize, uint nodeNumber, uint nodes)
        {
            return new NodeContainer
            {
                NodeNumber = nodeNumber,
                Nodes = nodes,
                Pointers = new uint[IndexCapacity(blockSize)]
            };
        }
    }
}
