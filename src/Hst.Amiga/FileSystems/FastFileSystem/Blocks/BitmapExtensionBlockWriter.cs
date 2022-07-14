namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class BitmapExtensionBlockWriter
    {
        public static async Task<byte[]> BuildBlock(BitmapExtensionBlock bitmapExtensionBlock, uint blockSize)
        {
            var blockStream =
                new MemoryStream(
                    bitmapExtensionBlock.BlockBytes == null || bitmapExtensionBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : bitmapExtensionBlock.BlockBytes);

            // write block free
            foreach (var bitmapBlock in bitmapExtensionBlock.BitmapBlocks)
            {
                await blockStream.WriteBigEndianUInt32(bitmapBlock.Offset);
            }

            // write next bitmap block pointer
            blockStream.Seek(blockSize - 4, SeekOrigin.Begin);
            await blockStream.WriteBigEndianUInt32(bitmapExtensionBlock.NextBitmapExtensionBlockPointer);

            // update block bytes 
            var blockBytes = blockStream.ToArray();
            bitmapExtensionBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}