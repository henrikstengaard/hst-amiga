namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using Core.Converters;

    public static class BitmapExtensionBlockParser
    {
        public static BitmapExtensionBlock Parse(byte[] blockBytes)
        {
            var bitmapBlockOffsets = new List<uint>();
            var bitmapBlocks = (blockBytes.Length - SizeOf.ULong) / SizeOf.ULong;
            for (var i = 0; i < bitmapBlocks; i++)
            {
                var bitmapBlockOffset = BigEndianConverter.ConvertBytesToUInt32(blockBytes, i * SizeOf.ULong);
                if (bitmapBlockOffset == 0)
                {
                    break;
                }

                bitmapBlockOffsets.Add(bitmapBlockOffset);
            }

            // read next bitmap extension block pointer
            var nextBitmapExtensionBlockPointer = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - SizeOf.ULong);

            return new BitmapExtensionBlock
            {
                BlockBytes = blockBytes,
                BitmapBlockOffsets = bitmapBlockOffsets.ToArray(),
                NextBitmapExtensionBlockPointer = nextBitmapExtensionBlockPointer
            };
        }
    }
}