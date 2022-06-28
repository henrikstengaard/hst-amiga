namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;

    public static class FileExtBlockWriter
    {
        public static async Task<byte[]> BuildBlock(FileExtBlock fileExtBlock, uint blockSize)
        {
            var blockStream =
                new MemoryStream(
                    fileExtBlock.BlockBytes == null || fileExtBlock.BlockBytes.Length == 0
                        ? new byte[blockSize]
                        : fileExtBlock.BlockBytes);

            await blockStream.WriteBigEndianInt32(fileExtBlock.type);
            await blockStream.WriteBigEndianInt32(fileExtBlock.headerKey);
            await blockStream.WriteBigEndianInt32(fileExtBlock.highSeq);
            await blockStream.WriteBigEndianInt32(fileExtBlock.dataSize);
            await blockStream.WriteBigEndianInt32(fileExtBlock.firstData);
            await blockStream.WriteBigEndianUInt32(0); // checksum

            for (var i = 0; i < Constants.MAX_DATABLK; i++)
            {
                await blockStream.WriteBigEndianInt32(fileExtBlock.dataBlocks[i]);
            }
            
            for (var i = 0; i < 45; i++)
            {
                await blockStream.WriteBigEndianInt32(0);
            }

            await blockStream.WriteBigEndianInt32(fileExtBlock.info);
            await blockStream.WriteBigEndianInt32(fileExtBlock.nextSameHash);
            await blockStream.WriteBigEndianInt32(fileExtBlock.parent);
            await blockStream.WriteBigEndianInt32(fileExtBlock.extension);
            await blockStream.WriteBigEndianInt32(fileExtBlock.secType);
            
            var blockBytes = blockStream.ToArray();
            var newSum = Raw.AdfNormalSum(blockBytes, 20, blockBytes.Length);
            // swLong(buf+20, newSum);
            var checksumBytes = BigEndianConverter.ConvertUInt32ToBytes(newSum);
            Array.Copy(checksumBytes, 0, blockBytes, 20, checksumBytes.Length);

            fileExtBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}