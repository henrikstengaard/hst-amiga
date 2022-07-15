namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class BitmapExtensionBlockBuilder
    {
        public static byte[] Build(BitmapExtensionBlock bitmapExtensionBlock, uint blockSize)
        {
            var blockBytes = new byte[blockSize];
            if (bitmapExtensionBlock.BlockBytes != null)
            {
                Array.Copy(bitmapExtensionBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            var bitmapBlocks = (blockBytes.Length - SizeOf.ULong) / SizeOf.ULong;
            for (var i = 0; i < bitmapBlocks; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(
                    i < bitmapExtensionBlock.BitmapBlockOffsets.Length
                        ? bitmapExtensionBlock.BitmapBlockOffsets[i]
                        : 0,
                    blockBytes,
                    i * SizeOf.ULong);
            }

            BigEndianConverter.ConvertUInt32ToBytes(bitmapExtensionBlock.NextBitmapExtensionBlockPointer, blockBytes,
                blockBytes.Length - SizeOf.ULong);

            return blockBytes;
        }
    }
}