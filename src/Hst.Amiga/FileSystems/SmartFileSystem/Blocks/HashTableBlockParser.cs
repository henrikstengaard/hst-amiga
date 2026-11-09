namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class HashTableBlockParser
    {
        public static HashTableBlock Parse(byte[] blockBytes)
        {
            var hashTableBlock = new HashTableBlock(blockBytes.Length);
            BlockHelper.ReadHeader(blockBytes, hashTableBlock, Constants.HashTableId);

            hashTableBlock.Parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            for (var i = 0; i < hashTableBlock.HashEntries.Length; i++)
            {
                hashTableBlock.HashEntries[i] =
                    BigEndianConverter.ConvertBytesToUInt32(blockBytes, Constants.HashTableHeaderSize + i * 4);
            }

            return hashTableBlock;
        }
    }
}
