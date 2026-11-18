namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    internal static class BitmapBlockBuilder
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

            BigEndianArrays.WriteUInt32Array(blockBytes, 0x4, bitmapBlock.Map, mapEntries);

            bitmapBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x0);
            bitmapBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}