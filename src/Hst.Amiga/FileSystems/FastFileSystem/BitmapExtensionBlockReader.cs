namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class BitmapExtensionBlockReader
    {
        public static async Task<BitmapExtensionBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            // read bitmap block offsets
            var bitmapBlockOffsets = new List<uint>();
            var bitmapBlocks = blockBytes.Length - SizeOf.ULong / SizeOf.ULong;
            for (var i = 0; i < bitmapBlocks; i++)
            {
                var bitmapBlockOffset = await blockStream.ReadBigEndianUInt32();
                if (bitmapBlockOffset == 0)
                {
                    break;
                }

                bitmapBlockOffsets.Add(bitmapBlockOffset);
            }

            // read next bitmap block pointer
            blockStream.Seek(blockBytes.Length - 4, SeekOrigin.Begin);
            var nextBitmapExtensionBlockPointer = await blockStream.ReadBigEndianUInt32();

            return new BitmapExtensionBlock
            {
                BlockBytes = blockBytes,
                BitmapBlockOffsets = bitmapBlockOffsets,
                NextBitmapExtensionBlockPointer = nextBitmapExtensionBlockPointer
            };
        }
    }
}