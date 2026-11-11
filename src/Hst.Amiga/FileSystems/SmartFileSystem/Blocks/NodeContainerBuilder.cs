namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class NodeContainerBuilder
    {
        public static byte[] Build(NodeContainer nodeContainer, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, nodeContainer);

            BigEndianConverter.ConvertUInt32ToBytes(nodeContainer.NodeNumber, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(nodeContainer.Nodes, blockBytes, 0x10);

            if (nodeContainer.IsLeaf)
            {
                var capacity = NodeContainer.LeafCapacity(blockSize);
                for (var i = 0; i < capacity && i < nodeContainer.ObjectNodes.Length; i++)
                {
                    var offset = Constants.NodeContainerHeaderSize + i * Constants.ObjectNodeSize;
                    var objectNode = nodeContainer.ObjectNodes[i];
                    BigEndianConverter.ConvertUInt32ToBytes(objectNode.Data, blockBytes, offset);
                    BigEndianConverter.ConvertUInt32ToBytes(objectNode.Next, blockBytes, offset + 4);
                    BigEndianConverter.ConvertUInt16ToBytes(objectNode.Hash16, blockBytes, offset + 8);
                }
            }
            else
            {
                var capacity = NodeContainer.IndexCapacity(blockSize);
                for (var i = 0; i < capacity && i < nodeContainer.Pointers.Length; i++)
                {
                    BigEndianConverter.ConvertUInt32ToBytes(nodeContainer.Pointers[i], blockBytes,
                        Constants.NodeContainerHeaderSize + i * 4);
                }
            }

            return BlockHelper.UpdateChecksum(blockBytes, nodeContainer);
        }
    }
}
