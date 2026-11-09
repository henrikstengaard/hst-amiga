namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class BitmapBlockParser
    {
        public static BitmapBlock Parse(byte[] blockBytes)
        {
            var bitmapBlock = new BitmapBlock(blockBytes.Length);
            BlockHelper.ReadHeader(blockBytes, bitmapBlock, Constants.BitmapId);

            for (var i = 0; i < bitmapBlock.Map.Length; i++)
            {
                bitmapBlock.Map[i] = BigEndianConverter.ConvertBytesToUInt32(blockBytes, Constants.BitmapHeaderSize + i * 4);
            }

            return bitmapBlock;
        }
    }
}
