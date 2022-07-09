namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class FileExtBlockWriter
    {
        public static async Task<byte[]> BuildBlock(FileExtBlock fileExtBlock, int blockSize)
        {
            fileExtBlock.IndexSize = 0;
            fileExtBlock.FirstData = 0;
            
            var blockStream =
                new MemoryStream(
                    fileExtBlock.BlockBytes == null || fileExtBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : fileExtBlock.BlockBytes);

            await blockStream.WriteBigEndianInt32(fileExtBlock.Type);
            await blockStream.WriteBigEndianInt32(fileExtBlock.HeaderKey);
            await blockStream.WriteBigEndianInt32(fileExtBlock.HighSeq);
            await blockStream.WriteBigEndianInt32(fileExtBlock.IndexSize);
            await blockStream.WriteBigEndianInt32(fileExtBlock.FirstData);
            await blockStream.WriteBigEndianUInt32(0); // checksum

            for (var i = 0; i < Constants.MAX_DATABLK; i++)
            {
                await blockStream.WriteBigEndianInt32(fileExtBlock.Index[i]);
            }
            
            for (var i = 0; i < 45; i++)
            {
                await blockStream.WriteBigEndianInt32(0);
            }

            await blockStream.WriteBigEndianInt32(fileExtBlock.Info);
            await blockStream.WriteBigEndianInt32(fileExtBlock.NextSameHash);
            await blockStream.WriteBigEndianInt32(fileExtBlock.Parent);
            await blockStream.WriteBigEndianInt32(fileExtBlock.Extension);
            await blockStream.WriteBigEndianInt32(fileExtBlock.SecType);
            
            var blockBytes = blockStream.ToArray();

            fileExtBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileExtBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}