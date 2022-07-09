namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class DataBlockWriter
    {
        public static async Task<byte[]> BuildBlock(DataBlock dataBlock, int blockSize)
        {
            var blockStream =
                new MemoryStream(
                    dataBlock.BlockBytes == null || dataBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : dataBlock.BlockBytes);

            await blockStream.WriteBigEndianInt32(dataBlock.Type); // 0x000
            await blockStream.WriteBigEndianInt32(dataBlock.HeaderKey); // 0x004
            await blockStream.WriteBigEndianInt32(dataBlock.SeqNum); // 0x008
            await blockStream.WriteBigEndianInt32(dataBlock.DataSize); // 0x0c
            await blockStream.WriteBigEndianInt32(dataBlock.NextData); // 0x10
            await blockStream.WriteBigEndianUInt32(0); // 0x014: checksum
            await blockStream.WriteBytes(dataBlock.Data); // 0x018 : data
            
            var blockBytes = blockStream.ToArray();

            dataBlock.CheckSum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dataBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}