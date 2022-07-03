﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class FileExtBlockReader
    {
        public static async Task<FileExtBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var type = await blockStream.ReadBigEndianInt32();
            var headerKey = await blockStream.ReadBigEndianInt32();
            var highSeq = await blockStream.ReadBigEndianInt32();
            var dataSize = await blockStream.ReadBigEndianInt32();
            var firstData = await blockStream.ReadBigEndianInt32();
            var checksum = await blockStream.ReadBigEndianInt32();

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file system header block checksum");
            }
            
            var dataBlocks = new List<int>();
            for (var i = 0; i < Constants.MAX_DATABLK; i++)
            {
                dataBlocks.Add(await blockStream.ReadBigEndianInt32());
            }

            for (var i = 0; i < 45; i++)
            {
                await blockStream.ReadBigEndianInt32();
            }
            
            var info = await blockStream.ReadBigEndianInt32();
            var nextSameHash = await blockStream.ReadBigEndianInt32();
            var parent = await blockStream.ReadBigEndianInt32();
            var extension = await blockStream.ReadBigEndianInt32();
            var secType = await blockStream.ReadBigEndianInt32();
            
            return new FileExtBlock
            {
                BlockBytes = blockBytes,
                type = type,
                headerKey = headerKey,
                highSeq = highSeq,
                dataSize = dataSize,
                firstData = firstData,
                checkSum = checksum,
                dataBlocks = dataBlocks.ToArray(),
                info = info,
                nextSameHash = nextSameHash,
                parent = parent,
                extension = extension,
                secType = secType
            };
        }
    }
}