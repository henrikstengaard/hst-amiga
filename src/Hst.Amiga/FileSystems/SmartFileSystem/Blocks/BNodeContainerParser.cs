namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System.IO;
    using Core.Converters;

    public static class BNodeContainerParser
    {
        public static BNodeContainer Parse(byte[] blockBytes)
        {
            var bNodeContainer = new BNodeContainer();
            BlockHelper.ReadHeader(blockBytes, bNodeContainer, Constants.BNodeContainerId);

            var nodeCount = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0xc);
            bNodeContainer.IsLeaf = blockBytes[0xe] != 0;
            bNodeContainer.NodeSize = blockBytes[0xf];

            if (bNodeContainer.NodeSize == 0 || nodeCount > bNodeContainer.Capacity(blockBytes.Length))
            {
                throw new IOException($"Invalid B-tree node container block {bNodeContainer.OwnBlock}");
            }

            for (var i = 0; i < nodeCount; i++)
            {
                var offset = Constants.BNodeContainerHeaderSize + i * bNodeContainer.NodeSize;
                if (bNodeContainer.IsLeaf)
                {
                    bNodeContainer.Nodes.Add(new ExtentBNode
                    {
                        Key = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset),
                        Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4),
                        Prev = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 8),
                        Blocks = BigEndianConverter.ConvertBytesToUInt16(blockBytes, offset + 12)
                    });
                }
                else
                {
                    bNodeContainer.Nodes.Add(new BNode
                    {
                        Key = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset),
                        Data = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4)
                    });
                }
            }

            return bNodeContainer;
        }
    }
}
