namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
    using Core.Converters;

    public static class EntryBlockBuilder
    {
        public static byte[] Build(EntryBlock entryBlock, int blockSize, bool useLnfs = false)
        {
            if (entryBlock.Type != Constants.T_HEADER)
            {
                throw new ArgumentException($"Invalid entry block type '{entryBlock.SecType}'",
                    nameof(entryBlock.Type));
            }

            switch (entryBlock.SecType)
            {
                case Constants.ST_ROOT:
                    return RootBlockBuilder.Build(entryBlock as RootBlock, blockSize);
                case Constants.ST_DIR:
                case Constants.ST_FILE:
                case Constants.ST_LDIR:
                case Constants.ST_LFILE:
                    return useLnfs
                        ? LongNameEntryBlockWriter.Build(entryBlock, blockSize) 
                        : BuildEntryBlock(entryBlock, blockSize);
                default:
                    throw new ArgumentException($"Invalid entry block secondary type '{entryBlock.SecType}'",
                        nameof(entryBlock.SecType));
            }
        }
        
        public static byte[] BuildEntryBlock(EntryBlock entryBlock, int blockSize)
        {
            if (entryBlock.SecType != Constants.ST_FILE &&
                entryBlock.SecType != Constants.ST_DIR &&
                entryBlock.SecType != Constants.ST_LFILE &&
                entryBlock.SecType != Constants.ST_LDIR)
            {
                throw new ArgumentException($"Invalid entry block secondary type '{entryBlock.SecType}'", nameof(entryBlock));
            }

            if (entryBlock.BlockBytes != null && entryBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Entry block bytes is not equal to block size '{blockSize}'", nameof(entryBlock));
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

            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Access, blockBytes, blockBytes.Length - 0xc0); // block size - 0xc0: access / protection
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.ByteSize, blockBytes, blockBytes.Length - 0xbc); // block size - 0xbc: byte size
            blockBytes.WriteStringWithLength(blockBytes.Length - 0xb8, entryBlock.Comment, Constants.MAXCMMTLEN); // block size - 0xb8: comment length (first byte) + comment max length 79 chars

            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x5c, entryBlock.Date); // block size - 0x5c: last access date
            blockBytes.WriteStringWithLength(blockBytes.Length - 0x50, entryBlock.Name, Constants.MAXNAMELEN); // block size - 0x50: name length (first byte) + name max length 30 chars

            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, blockBytes.Length - 0x30); // reserved
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.RealEntry, blockBytes, blockBytes.Length - 0x2c); // block size - 0x2c: real_entry, FFS : pointer to "real" file or directory
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.NextLink, blockBytes, blockBytes.Length - 0x28); // block size - 0x28: next_link, FFS : hardlinks chained list (first=newest)

            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.NextSameHash, blockBytes, blockBytes.Length - 0x10); // block size - 0x10: hash_chain, next entry ptr with same hash
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Parent, blockBytes, blockBytes.Length - 0x0c); // block size - 0x0c: parent directory
            BigEndianConverter.ConvertUInt32ToBytes(entryBlock.Extension, blockBytes, blockBytes.Length - 0x08); // block size - 0x08: FFS : first directory cache block
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.SecType, blockBytes, blockBytes.Length - 0x04); // block size - 0x04: secondary type, eg. ST_USERDIR (== 2) for directory

            entryBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x14);
            entryBlock.BlockBytes = blockBytes;
            
            return blockBytes;
        }
    }
}