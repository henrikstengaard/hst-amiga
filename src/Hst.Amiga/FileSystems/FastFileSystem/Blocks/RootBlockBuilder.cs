namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Hst.Amiga.Extensions;
    using Core.Converters;

    public static class RootBlockBuilder
    {
        public static byte[] Build(RootBlock rootBlock, int blockSize)
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

            rootBlock.IndexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 0x4); // header key
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 0x8); // high seq
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.IndexSize, blockBytes, 0xc); // ht_size
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 0x10); // reserved
            BigEndianConverter.ConvertInt32ToBytes(0, blockBytes, 0x14); // checksum

            for (var i = 0; i < rootBlock.IndexSize; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(rootBlock.Index[i], blockBytes, 0x18 + (i * SizeOf.Long));
            }

            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.BitmapFlags, blockBytes, blockBytes.Length - 0xc8); // bm_flag

            for (var i = 0; i < Constants.BM_SIZE; i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(i < rootBlock.BitmapBlockOffsets.Length ? rootBlock.BitmapBlockOffsets[i] : 0,
                    blockBytes, blockBytes.Length - 0xc4 + (i * SizeOf.ULong));
            }

            // write first bitmap extension block pointer
            BigEndianConverter.ConvertUInt32ToBytes(
                rootBlock.BitmapExtensionBlocksOffset == 0 ? 0 : rootBlock.BitmapExtensionBlocksOffset, blockBytes,
                blockBytes.Length - 0x60); // bm_flag

            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x5c, rootBlock.RootAlterationDate);
            blockBytes.WriteStringWithLength(blockBytes.Length - 0x50, rootBlock.DiskName, Constants.MAXNAMELEN);
            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x28, rootBlock.DiskAlterationDate);
            DateHelper.WriteDate(blockBytes, blockBytes.Length - 0x1c, rootBlock.FileSystemCreationDate);
            
            BigEndianConverter.ConvertUInt32ToBytes(0U, blockBytes, blockBytes.Length - 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(0U, blockBytes, blockBytes.Length - 0x0c);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.Extension, blockBytes, blockBytes.Length - 0x08); // FFS: first directory cache block, 0 otherwise
            BigEndianConverter.ConvertInt32ToBytes(rootBlock.SecType, blockBytes, blockBytes.Length - 0x04); // block secondary type = ST_ROOT (value 1)
            
            rootBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 20);
            rootBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}