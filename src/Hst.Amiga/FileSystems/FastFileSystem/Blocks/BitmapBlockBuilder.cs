namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class BitmapBlockBuilder
    {
        public static byte[] Build(BitmapBlock bitmapBlock, int blockSize)
        {
            var mapEntries = (blockSize - SizeOf.ULong) / SizeOf.Long;

            var blockBytes = new byte[blockSize];
            if (bitmapBlock.BlockBytes != null)
            {
                Array.Copy(bitmapBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            BigEndianConverter.ConvertInt32ToBytes(bitmapBlock.Checksum, blockBytes, 0x0);

            for (var i = 0; i < mapEntries; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(i < bitmapBlock.Map.Length ? bitmapBlock.Map[i] : 0,
                    blockBytes, 0x4 + i * SizeOf.ULong);
            }

            bitmapBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x0);
            bitmapBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}