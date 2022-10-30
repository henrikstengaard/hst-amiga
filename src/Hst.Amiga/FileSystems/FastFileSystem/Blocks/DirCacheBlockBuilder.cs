namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class DirCacheBlockBuilder
    {
        public static byte[] Build(DirCacheBlock dirCacheBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            if (dirCacheBlock.BlockBytes != null)
            {
                Array.Copy(dirCacheBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(dirCacheBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(dirCacheBlock.Parent, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(dirCacheBlock.RecordsNb, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(dirCacheBlock.NextDirC, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.Checksum, blockBytes, 0x14);
            
            var maxRecordsSize = blockBytes.Length - (SizeOf.ULong * 4) - (SizeOf.Long * 2);
            Array.Copy(dirCacheBlock.Records, 0, blockBytes, 0x18, maxRecordsSize);
            
            dirCacheBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x14);
            dirCacheBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}