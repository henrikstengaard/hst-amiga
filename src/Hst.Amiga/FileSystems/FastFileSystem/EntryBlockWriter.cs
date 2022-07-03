namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using Core.Converters;
    using Extensions;

    public static class EntryBlockWriter
    {
        public static byte[] BuildBlock(EntryBlock entryBlock, uint blockSize)
        {
            if (!(entryBlock.SecType == Constants.ST_ROOT || entryBlock.SecType == Constants.ST_DIR ||
                  entryBlock.SecType == Constants.ST_FILE))
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
            
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.SecType == Constants.ST_ROOT || entryBlock is RootBlock ? 0 : entryBlock.HeaderKey, blockBytes, 0x4); // header key
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.HighSeq, blockBytes, 0x8); // high seq
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.IndexSize, blockBytes, 0xc); // ht_size
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.FirstData, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Checksum, blockBytes, 0x14);
            
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(entryBlock.Index[i], blockBytes, 0x18 + (i * Amiga.SizeOf.Long));
            }
            
            if (entryBlock.SecType != Constants.ST_ROOT)
            {
                BigEndianConverter.ConvertInt32ToBytes(entryBlock.Access, blockBytes, 0x140);
                BigEndianConverter.ConvertInt32ToBytes(entryBlock.ByteSize, blockBytes, 0x144);
                blockBytes.WriteStringWithLength(0x148, entryBlock.Comment, Constants.MAXCMMTLEN + 1);

                DateHelper.WriteDate(blockBytes, 0x1a4, entryBlock.Date);
                blockBytes.WriteStringWithLength(0x1b0, entryBlock.Name, Constants.MAXNAMELEN + 1);

                BigEndianConverter.ConvertInt32ToBytes(entryBlock.RealEntry, blockBytes, 0x1d4);
                BigEndianConverter.ConvertInt32ToBytes(entryBlock.NextLink, blockBytes, 0x1d8);
            }

            BigEndianConverter.ConvertInt32ToBytes(entryBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.Extension, blockBytes, 0x1f8); // FFS: first directory cache block, 0 otherwise
            BigEndianConverter.ConvertInt32ToBytes(entryBlock.SecType, blockBytes, 0x1fc); // block secondary type = ST_ROOT (value 1)
            
            entryBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            entryBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}