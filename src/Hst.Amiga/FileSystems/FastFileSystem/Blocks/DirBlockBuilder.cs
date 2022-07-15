namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
    using Core.Converters;

    public static class DirBlockBuilder
    {
        public static byte[] Build(DirBlock dirBlock, int blockSize)
        {
            if (dirBlock.SecType != Constants.ST_DIR)
            {
                throw new ArgumentException($"Invalid dir block secondary type '{dirBlock.SecType}'", nameof(dirBlock));
            }

            if (dirBlock.BlockBytes != null && dirBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Dir block bytes is not equal to block size '{blockSize}'", nameof(dirBlock));
            }

            var blockBytes = new byte[blockSize];
            if (dirBlock.BlockBytes != null)
            {
                Array.Copy(dirBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(dirBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Access, blockBytes, 0x140);
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x144); // reserved / byte size
            blockBytes.WriteStringWithLength(0x148, dirBlock.Comment, Constants.MAXCMMTLEN + 1);

            DateHelper.WriteDate(blockBytes, 0x1a4, dirBlock.Date);
            blockBytes.WriteStringWithLength(0x1b0, dirBlock.Name, Constants.MAXNAMELEN + 1);

            BigEndianConverter.ConvertInt32ToBytes(dirBlock.RealEntry, blockBytes, 0x1d4);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.NextLink, blockBytes, 0x1d8);

            BigEndianConverter.ConvertInt32ToBytes(dirBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Extension, blockBytes, 0x1f8);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.SecType, blockBytes, 0x1fc);
            
            dirBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}