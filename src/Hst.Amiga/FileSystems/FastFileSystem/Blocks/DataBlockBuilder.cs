namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class DataBlockBuilder
    {
        public static byte[] Build(DataBlock dataBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            if (dataBlock.BlockBytes != null)
            {
                Array.Copy(dataBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(dataBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(dataBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(dataBlock.SeqNum, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(dataBlock.DataSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(dataBlock.NextData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(dataBlock.Checksum, blockBytes, 0x14);
            
            Array.Copy(dataBlock.Data, 0, blockBytes, 0x18, Math.Min(dataBlock.DataSize, 488));
            
            dataBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dataBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}