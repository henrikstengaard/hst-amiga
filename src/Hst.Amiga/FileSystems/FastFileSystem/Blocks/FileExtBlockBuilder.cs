namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class FileExtBlockBuilder
    {
        public static byte[] Build(FileExtBlock fileExtBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            if (fileExtBlock.BlockBytes != null)
            {
                Array.Copy(fileExtBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            fileExtBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.Checksum, blockBytes, 0x14);

            for (var i = 0; i < fileExtBlock.IndexSize; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.Info, blockBytes, blockBytes.Length - 0x14);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.NextSameHash, blockBytes, blockBytes.Length - 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.Parent, blockBytes, blockBytes.Length - 0x0c);
            BigEndianConverter.ConvertUInt32ToBytes(fileExtBlock.Extension, blockBytes, blockBytes.Length - 0x08);
            BigEndianConverter.ConvertInt32ToBytes(fileExtBlock.SecType, blockBytes, blockBytes.Length - 0x04);
            
            fileExtBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileExtBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}