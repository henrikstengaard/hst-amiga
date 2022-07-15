namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class FileExtBlockBuilder
    {
        public static byte[] Build(FileExtBlock fileExtBlock, int blockSize)
        {
            fileExtBlock.IndexSize = 0;
            fileExtBlock.FirstData = 0;
            
            var blockBytes = new byte[blockSize];
            if (fileExtBlock.BlockBytes != null)
            {
                Array.Copy(fileExtBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Checksum, blockBytes, 0x14);

            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            for (var i = 0; i < 45; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x138 + (i * SizeOf.Long));
            }

            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Info, blockBytes, 0x1ec);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Extension, blockBytes, 0x1f8);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.SecType, blockBytes, 0x1fc);
            
            fileExtBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileExtBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}