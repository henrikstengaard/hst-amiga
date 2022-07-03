﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class OfsDataBlockReader
    {
        public static async Task<OfsDataBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var type = await blockStream.ReadBigEndianInt32();
            var headerKey = await blockStream.ReadBigEndianInt32();
            var seqNum = await blockStream.ReadBigEndianInt32();
            var dataSize = await blockStream.ReadBigEndianInt32();
            var nextData = await blockStream.ReadBigEndianInt32();
            var checksum = await blockStream.ReadBigEndianInt32();

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);

            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid ofs data block checksum");
            }
            
            var data = await blockStream.ReadBytes(488);
            
            return new OfsDataBlock
            {
                BlockBytes = blockBytes,
                Type = type,
                HeaderKey = headerKey,
                SeqNum = seqNum,
                DataSize = dataSize,
                NextData = nextData,
                CheckSum = checksum,
                Data = data
            };
        }
    }
}