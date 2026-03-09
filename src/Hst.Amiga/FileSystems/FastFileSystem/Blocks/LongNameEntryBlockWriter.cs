using System;
using Hst.Amiga.Extensions;
using Hst.Core.Converters;

namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    /// <summary>
    /// Long name entry block writer for long name file system (LNFS) present in DOS\6 and DOS\7.
    /// </summary>
    public static class LongNameEntryBlockWriter
    {
        public static byte[] Build(EntryBlock entryBlock, int blockSize)
        {
            if (entryBlock.SecType != Constants.ST_FILE &&
                entryBlock.SecType != Constants.ST_DIR &&
                entryBlock.SecType != Constants.ST_LFILE &&
                entryBlock.SecType != Constants.ST_LDIR)
            {
                throw new ArgumentException($"Invalid long name entry block secondary type '{entryBlock.SecType}'", nameof(entryBlock));
            }

            if (entryBlock.BlockBytes != null && entryBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Long name entry block bytes is not equal to block size '{blockSize}'", nameof(entryBlock));
            }

            var blockBytes = new byte[blockSize];
            if (entryBlock.BlockBytes != null)
            {
                Array.Copy(entryBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            entryBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.HighSeq, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.IndexSize, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Checksum, blockBytes, 0x14);

            for (var i = 0; i < entryBlock.IndexSize; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }
            
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Access, blockBytes, blockBytes.Length - 0xc0);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.ByteSize, blockBytes, blockBytes.Length - 0xbc);
            
            blockBytes.WriteStringWithLength(blockBytes.Length - 0xb8, entryBlock.Name, Constants.LNFSMAXNAMELEN);
            
            if (!string.IsNullOrEmpty(entryBlock.Comment))
            {
                var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - entryBlock.Name.Length + 1;
                if (nameAndCommendSpaceLeft < entryBlock.Comment.Length + 1)
                {
                    throw new ArgumentException($"Long name file system file header block does not have space left for comment. Put comment in comment block and update comment block reference.", nameof(entryBlock));
                }
                
                blockBytes.WriteStringWithLength(blockBytes.Length - 0xb8 + entryBlock.Name.Length + 1, entryBlock.Comment, Constants.LNFSMAXNAMELEN);
            }
            
            /* Number of the block which holds the associated comment string */
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.CommentBlock, blockBytes, blockBytes.Length - 0x48); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x3c, entryBlock.Date);

            // long * 2: spare 2 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.RealEntry, blockBytes, blockBytes.Length - 0x2c);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.NextLink, blockBytes, blockBytes.Length - 0x28);

            // long * 6: spare 6 / not used, must be set to zero
            
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.NextSameHash, blockBytes, blockBytes.Length - 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Parent, blockBytes, blockBytes.Length - 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Extension, blockBytes, blockBytes.Length - 0x8);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.SecType, blockBytes, blockBytes.Length - 0x4);
            
            entryBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            entryBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}