namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class BitmapBlockBuilder
    {
        public static byte[] Build(BitmapBlock bitmapBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, bitmapBlock);

            var longs = (blockSize - Constants.BitmapHeaderSize) / 4;
            for (var i = 0; i < longs && i < bitmapBlock.Map.Length; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(bitmapBlock.Map[i], blockBytes, Constants.BitmapHeaderSize + i * 4);
            }

            return BlockHelper.UpdateChecksum(blockBytes, bitmapBlock);
        }
    }
}
