namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System.IO;
    using Core.Converters;

    public static class BNodeContainerBuilder
    {
        public static byte[] Build(BNodeContainer bNodeContainer, int blockSize)
        {
            if (bNodeContainer.Nodes.Count > bNodeContainer.Capacity(blockSize))
            {
                throw new IOException($"B-tree node container block {bNodeContainer.OwnBlock} has too many nodes");
            }

            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, bNodeContainer);

            BigEndianConverter.ConvertUInt16ToBytes((ushort)bNodeContainer.Nodes.Count, blockBytes, 0xc);
            blockBytes[0xe] = (byte)(bNodeContainer.IsLeaf ? 1 : 0);
            blockBytes[0xf] = bNodeContainer.NodeSize;

            for (var i = 0; i < bNodeContainer.Nodes.Count; i++)
            {
                var offset = Constants.BNodeContainerHeaderSize + i * bNodeContainer.NodeSize;
                var node = bNodeContainer.Nodes[i];
                BigEndianConverter.ConvertUInt32ToBytes(node.Key, blockBytes, offset);
                BigEndianConverter.ConvertUInt32ToBytes(node.Data, blockBytes, offset + 4);

                if (bNodeContainer.IsLeaf && node is ExtentBNode extentBNode)
                {
                    BigEndianConverter.ConvertUInt32ToBytes(extentBNode.Prev, blockBytes, offset + 8);
                    BigEndianConverter.ConvertUInt16ToBytes(extentBNode.Blocks, blockBytes, offset + 12);
                }
            }

            return BlockHelper.UpdateChecksum(blockBytes, bNodeContainer);
        }
    }
}
