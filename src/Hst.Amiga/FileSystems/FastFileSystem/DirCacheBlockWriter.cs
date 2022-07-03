namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class DirCacheBlockWriter
    {
        public static async Task<byte[]> BuildBlock(DirCacheBlock dirCacheBlock, uint blockSize)
        {
            var blockStream =
                new MemoryStream(
                    dirCacheBlock.BlockBytes == null || dirCacheBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : dirCacheBlock.BlockBytes);

            await blockStream.WriteBigEndianInt32(dirCacheBlock.Type);
            await blockStream.WriteBigEndianInt32(dirCacheBlock.HeaderKey);
            await blockStream.WriteBigEndianInt32(dirCacheBlock.Parent);
            await blockStream.WriteBigEndianInt32(dirCacheBlock.RecordsNb);
            await blockStream.WriteBigEndianInt32(dirCacheBlock.NextDirC);
            await blockStream.WriteBigEndianUInt32(0); // checksum

            await blockStream.WriteBytes(dirCacheBlock.Records);
            
            var blockBytes = blockStream.ToArray();

            dirCacheBlock.CheckSum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dirCacheBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}