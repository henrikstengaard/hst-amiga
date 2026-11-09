namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class NodeContainerParser
    {
        public static NodeContainer Parse(byte[] blockBytes)
        {
            var nodeNumber = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            var nodes = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);

            var nodeContainer = nodes == 1
                ? NodeContainer.CreateLeaf(blockBytes.Length, nodeNumber)
                : NodeContainer.CreateIndex(blockBytes.Length, nodeNumber, nodes);
            BlockHelper.ReadHeader(blockBytes, nodeContainer, Constants.NodeContainerId);

            if (nodeContainer.IsLeaf)
            {
                for (var i = 0; i < nodeContainer.ObjectNodes.Length; i++)
                {
                    var offset = Constants.NodeContainerHeaderSize + i * Constants.ObjectNodeSize;
                    var objectNode = nodeContainer.ObjectNodes[i];
                    objectNode.Data = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset);
                    objectNode.Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4);
                    objectNode.Hash16 = BigEndianConverter.ConvertBytesToUInt16(blockBytes, offset + 8);
                }
            }
            else
            {
                for (var i = 0; i < nodeContainer.Pointers.Length; i++)
                {
                    nodeContainer.Pointers[i] =
                        BigEndianConverter.ConvertBytesToUInt32(blockBytes, Constants.NodeContainerHeaderSize + i * 4);
                }
            }

            return nodeContainer;
        }
    }
}
