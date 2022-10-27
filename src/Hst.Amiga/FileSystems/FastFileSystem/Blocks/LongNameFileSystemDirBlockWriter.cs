namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
    using Core.Converters;

    /// <summary>
    /// Writer for long name file system dir block (LNFS) present in DOS\6 and DOS\7
    /// </summary>
    public static class LongNameFileSystemDirBlockWriter
    {
        public static byte[] Build(DirBlock longNameFileSystemDirBlock, int blockSize)
        {
            if (longNameFileSystemDirBlock.SecType != Constants.ST_DIR)
            {
                throw new ArgumentException($"Invalid long name file system dir block secondary type '{longNameFileSystemDirBlock.SecType}'", nameof(longNameFileSystemDirBlock));
            }

            if (longNameFileSystemDirBlock.BlockBytes != null && longNameFileSystemDirBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Long name file system dir block bytes is not equal to block size '{blockSize}'", nameof(longNameFileSystemDirBlock));
            }

            var blockBytes = new byte[blockSize];
            if (longNameFileSystemDirBlock.BlockBytes != null)
            {
                Array.Copy(longNameFileSystemDirBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(longNameFileSystemDirBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(longNameFileSystemDirBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.Access, blockBytes, 0x140);
            
            blockBytes.WriteStringWithLength(0x148, longNameFileSystemDirBlock.Name, Constants.LNFSMAXNAMELEN);
            
            if (!string.IsNullOrEmpty(longNameFileSystemDirBlock.Comment))
            {
                var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - longNameFileSystemDirBlock.Name.Length + 1;
                if (nameAndCommendSpaceLeft < longNameFileSystemDirBlock.Comment.Length + 1)
                {
                    throw new ArgumentException($"Long name file system dir block does not have space left for comment. Put comment in comment block and update comment block reference.", nameof(longNameFileSystemDirBlock));
                }
                
                blockBytes.WriteStringWithLength(0x148 + longNameFileSystemDirBlock.Name.Length + 1, longNameFileSystemDirBlock.Comment, Constants.LNFSMAXNAMELEN);
            }
            
            /* Number of the block which holds the associated comment string */
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.CommentBlock, blockBytes, 0x1b8); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            DateHelper.WriteDate(blockBytes, 0x1c4, longNameFileSystemDirBlock.Date);

            // long * 2: spare 2 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.RealEntry, blockBytes, 0x1d4);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.NextLink, blockBytes, 0x1d8);

            // long * 6: spare 6 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemDirBlock.Extension, blockBytes, 0x1f8);
            BigEndianConverter.ConvertInt32ToBytes(longNameFileSystemDirBlock.SecType, blockBytes, 0x1fc);
            
            longNameFileSystemDirBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            longNameFileSystemDirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}