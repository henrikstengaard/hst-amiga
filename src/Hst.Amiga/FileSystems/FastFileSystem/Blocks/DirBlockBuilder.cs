namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
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
            
            dirBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < dirBlock.IndexSize; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(dirBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }

            EntryBlockBuilder.WriteGenericEntryBlock(dirBlock, blockBytes);
            
            dirBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}