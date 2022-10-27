namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
    using Core.Converters;

    /// <summary>
    /// Writer for long name file system file header block (LNFS) present in DOS\6 and DOS\7
    /// </summary>
    public static class LongNameFileSystemFileHeaderBlockWriter
    {
        public static byte[] Build(FileHeaderBlock fileHeaderBlock, int blockSize)
        {
            if (fileHeaderBlock.SecType != Constants.ST_FILE)
            {
                throw new ArgumentException($"Invalid long name file system file header block secondary type '{fileHeaderBlock.SecType}'", nameof(fileHeaderBlock));
            }

            if (fileHeaderBlock.BlockBytes != null && fileHeaderBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Long name file system file header block bytes is not equal to block size '{blockSize}'", nameof(fileHeaderBlock));
            }

            var blockBytes = new byte[blockSize];
            if (fileHeaderBlock.BlockBytes != null)
            {
                Array.Copy(fileHeaderBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.Access, blockBytes, 0x140);
            
            blockBytes.WriteStringWithLength(0x148, fileHeaderBlock.Name, Constants.LNFSMAXNAMELEN);
            
            if (!string.IsNullOrEmpty(fileHeaderBlock.Comment))
            {
                var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - fileHeaderBlock.Name.Length + 1;
                if (nameAndCommendSpaceLeft < fileHeaderBlock.Comment.Length + 1)
                {
                    throw new ArgumentException($"Long name file system file header block does not have space left for comment. Put comment in comment block and update comment block reference.", nameof(fileHeaderBlock));
                }
                
                blockBytes.WriteStringWithLength(0x148 + fileHeaderBlock.Name.Length + 1, fileHeaderBlock.Comment, Constants.LNFSMAXNAMELEN);
            }
            
            /* Number of the block which holds the associated comment string */
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.CommentBlock, blockBytes, 0x1b8); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            DateHelper.WriteDate(blockBytes, 0x1c4, fileHeaderBlock.Date);

            // long * 2: spare 2 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.RealEntry, blockBytes, 0x1d4);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.NextLink, blockBytes, 0x1d8);

            // long * 6: spare 6 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertUInt32ToBytes(fileHeaderBlock.Extension, blockBytes, 0x1f8);
            BigEndianConverter.ConvertInt32ToBytes(fileHeaderBlock.SecType, blockBytes, 0x1fc);
            
            fileHeaderBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            fileHeaderBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}