namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using Core.Converters;

    public static class DirCacheBlockWriter
    {
        public static byte[] BuildBlock(DirCacheBlock dirCacheBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            if (dirCacheBlock.BlockBytes != null)
            {
                Array.Copy(dirCacheBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.HeaderKey, blockBytes, 0x4);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.Parent, blockBytes, 0x8);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.RecordsNb, blockBytes, 0xc);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.NextDirC, blockBytes, 0x10);
            BigEndianConverter.ConvertInt32ToBytes(dirCacheBlock.Checksum, blockBytes, 0x14);
            
            Array.Copy(dirCacheBlock.Records, 0, blockBytes, 0x18, 488);
            
            dirCacheBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x14);
            dirCacheBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}