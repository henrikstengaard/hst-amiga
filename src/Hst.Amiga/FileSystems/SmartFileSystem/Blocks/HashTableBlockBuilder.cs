namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class HashTableBlockBuilder
    {
        public static byte[] Build(HashTableBlock hashTableBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, hashTableBlock);

            BigEndianConverter.ConvertUInt32ToBytes(hashTableBlock.Parent, blockBytes, 0xc);
            var entries = HashHelper.HashEntries(blockSize);
            for (var i = 0; i < entries && i < hashTableBlock.HashEntries.Length; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(hashTableBlock.HashEntries[i], blockBytes,
                    Constants.HashTableHeaderSize + i * 4);
            }

            return BlockHelper.UpdateChecksum(blockBytes, hashTableBlock);
        }
    }
}
