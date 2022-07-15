namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
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
            
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Access, blockBytes, 0x140);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.ByteSize, blockBytes, 0x144);
            blockBytes.WriteStringWithLength(0x148, fileHeaderBlock.Comment, Constants.MAXCMMTLEN + 1);

            DateHelper.WriteDate(blockBytes, 0x1a4, fileHeaderBlock.Date);
            blockBytes.WriteStringWithLength(0x1b0, fileHeaderBlock.Name, Constants.MAXNAMELEN + 1);

            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.RealEntry, blockBytes, 0x1d4);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.NextLink, blockBytes, 0x1d8);

            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Extension, blockBytes, 0x1f8);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.SecType, blockBytes, 0x1fc);
            
            fileHeaderBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileHeaderBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}