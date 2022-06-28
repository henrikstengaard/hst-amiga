namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;

    public static class OfsDataBlockWriter
    {
        public static async Task<byte[]> BuildBlock(OfsDataBlock ofsDataBlock, uint blockSize)
        {
            var blockStream =
                new MemoryStream(
                    ofsDataBlock.BlockBytes == null || ofsDataBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : ofsDataBlock.BlockBytes);

            await blockStream.WriteBigEndianInt32(ofsDataBlock.Type); // 0x000
            await blockStream.WriteBigEndianInt32(ofsDataBlock.HeaderKey); // 0x004
            await blockStream.WriteBigEndianInt32(ofsDataBlock.SeqNum); // 0x008
            await blockStream.WriteBigEndianInt32(ofsDataBlock.DataSize); // 0x0c
            await blockStream.WriteBigEndianInt32(ofsDataBlock.NextData); // 0x10
            await blockStream.WriteBigEndianUInt32(0); // 0x014: checksum
            await blockStream.WriteBytes(ofsDataBlock.Data); // 0x018 : data
            
            var blockBytes = blockStream.ToArray();
            var newSum = Raw.AdfNormalSum(blockBytes, 20, blockBytes.Length);
            // swLong(buf+20, newSum);
            var checksumBytes = BigEndianConverter.ConvertUInt32ToBytes(newSum);
            Array.Copy(checksumBytes, 0, blockBytes, 20, checksumBytes.Length);

            ofsDataBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}