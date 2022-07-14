namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Hst.Amiga.Extensions;
    using Hst.Core.Converters;

    public static class RootBlockWriter
    {
        public static byte[] BuildBlock(RootBlock rootBlock, int blockSize)
        {
            if (rootBlock.SecType != Constants.ST_ROOT)
            {
                throw new ArgumentException($"Invalid root block secondary type '{rootBlock.SecType}'", nameof(rootBlock));
            }
            
            if (rootBlock.BlockBytes != null && rootBlock.BlockBytes.Length != blockSize)
            {
                throw new ArgumentException($"Root block bytes is not equal to block size '{blockSize}'", nameof(rootBlock));
            }

            var blockBytes = new byte[blockSize];
            if (rootBlock.BlockBytes != null)
            {
                Array.Copy(rootBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }

            BigEndianConverter.ConvertInt32ToBytes(rootBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x4); // header key
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x8); // high seq
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.IndexSize, blockBytes, 0xc); // ht_size
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x10); // reserved
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x14); // checksum

            for (var i = 0; i < Constants.HT_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(rootBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }

            BigEndianConverter.ConvertInt32ToBytes(rootBlock.BitmapFlags, blockBytes, 0x138); // bm_flag

            for (var i = 0; i < Constants.BM_SIZE; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(i < rootBlock.BitmapBlockOffsets.Length ? rootBlock.BitmapBlockOffsets[i] : 0,
                    blockBytes, 0x13c + (i * SizeOf.Long));
            }

            // write first bitmap extension block pointer
            BigEndianConverter.ConvertUInt32ToBytes(
                rootBlock.BitmapExtensionBlocksOffset == 0 ? 0 : rootBlock.BitmapExtensionBlocksOffset, blockBytes,
                0x1a0); // bm_flag

            DateHelper.WriteDate(blockBytes, 0x1a4, rootBlock.RootAlterationDate);
            blockBytes.WriteStringWithLength(0x1b0, rootBlock.DiskName, Constants.MAXNAMELEN);
            DateHelper.WriteDate(blockBytes, 0x1d8, rootBlock.DiskAlterationDate);
            DateHelper.WriteDate(blockBytes, 0x1e4, rootBlock.FileSystemCreationDate);
            
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.NextSameHash, blockBytes, 0x1f0);
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.Parent, blockBytes, 0x1f4);
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.Extension, blockBytes, 0x1f8); // FFS: first directory cache block, 0 otherwise
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.SecType, blockBytes, 0x1fc); // block secondary type = ST_ROOT (value 1)
            
            rootBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            rootBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}