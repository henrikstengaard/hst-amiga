namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class FileHeaderBlockBuilder
    {
        public static byte[] Build(FileHeaderBlock fileHeaderBlock, int blockSize)
        {
            if (fileHeaderBlock.SecType != Constants.ST_FILE)
            {
                throw new ArgumentException($"Invalid file header block secondary type '{fileHeaderBlock.SecType}'", nameof(fileHeaderBlock));
            }

            if (fileHeaderBlock.BlockBytes != null && fileHeaderBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Dir block bytes is not equal to block size '{blockSize}'", nameof(fileHeaderBlock));
            }

            var blockBytes = new byte[blockSize];
            if (fileHeaderBlock.BlockBytes != null)
            {
                Array.Copy(fileHeaderBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            fileHeaderBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < fileHeaderBlock.IndexSize; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            EntryBlockBuilder.WriteGenericEntryBlock(fileHeaderBlock, blockBytes);
            
            fileHeaderBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileHeaderBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}