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
        public static byte[] Build(DirBlock dirBlock, int blockSize)
        {
            if (dirBlock.SecType != Constants.ST_DIR)
            {
                throw new ArgumentException($"Invalid long name file system dir block secondary type '{dirBlock.SecType}'", nameof(dirBlock));
            }

            if (dirBlock.BlockBytes != null && dirBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Long name file system dir block bytes is not equal to block size '{blockSize}'", nameof(dirBlock));
            }

            var blockBytes = new byte[blockSize];
            if (dirBlock.BlockBytes != null)
            {
                Array.Copy(dirBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            dirBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
            
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
            
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.Access, blockBytes, blockBytes.Length - 0xc0);
            BigEndianConverter.ConvertUInt32ToBytes(0U, blockBytes, blockBytes.Length - 0xbc);
            
            blockBytes.WriteStringWithLength(blockBytes.Length - 0xb8, dirBlock.Name, Constants.LNFSMAXNAMELEN);
            
            if (!string.IsNullOrEmpty(dirBlock.Comment))
            {
                var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - dirBlock.Name.Length + 1;
                if (nameAndCommendSpaceLeft < dirBlock.Comment.Length + 1)
                {
                    throw new ArgumentException($"Long name file system dir block does not have space left for comment. Put comment in comment block and update comment block reference.", nameof(dirBlock));
                }
                
                blockBytes.WriteStringWithLength(blockBytes.Length - 0xb8 + dirBlock.Name.Length + 1, dirBlock.Comment, Constants.LNFSMAXNAMELEN);
            }
            
            /* Number of the block which holds the associated comment string */
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.CommentBlock, blockBytes, blockBytes.Length - 0x48); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x3c, dirBlock.Date);

            // long * 2: spare 2 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.RealEntry, blockBytes, blockBytes.Length - 0x2c);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.NextLink, blockBytes, blockBytes.Length - 0x28);

            // long * 6: spare 6 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.NextSameHash, blockBytes, blockBytes.Length - 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.Parent, blockBytes, blockBytes.Length - 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.Extension, blockBytes, blockBytes.Length - 0x8);
            BigEndianConverter.ConvertInt32ToBytes(dirBlock.SecType, blockBytes, blockBytes.Length - 0x4);
            
            dirBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            dirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}