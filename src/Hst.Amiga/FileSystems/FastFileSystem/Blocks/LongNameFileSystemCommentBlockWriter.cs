namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Amiga.Extensions;
    using Core.Converters;

    /// <summary>
    /// Writer for long name file system comment block (LNFS) present in DOS\6 and DOS\7
    /// </summary>
    public static class LongNameFileSystemCommentBlockWriter
    {
        public static byte[] Build(LongNameFileSystemCommentBlock longNameFileSystemCommentBlock, int blockSize)
        {
            if (longNameFileSystemCommentBlock.Type != Constants.TYPE_COMMENT)
            {
                throw new ArgumentException($"Invalid long name file system comment block type '{longNameFileSystemCommentBlock.Type}'",
                    nameof(longNameFileSystemCommentBlock));
            }
            
            var blockBytes = new byte[blockSize];
            if (longNameFileSystemCommentBlock.BlockBytes != null)
            {
                Array.Copy(longNameFileSystemCommentBlock.BlockBytes, 0, blockBytes, 0, blockSize);
            }
            
            BigEndianConverter.ConvertInt32ToBytes(longNameFileSystemCommentBlock.Type, blockBytes, 0x0);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemCommentBlock.OwnKey, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(longNameFileSystemCommentBlock.HeaderKey, blockBytes, 0x8);
            
            // long * 2: spare 1 / not used, must be set to zero
            BigEndianConverter.ConvertUInt32ToBytes(0U, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(0U, blockBytes, 0x10);
            
            blockBytes.WriteStringWithLength(0x18, longNameFileSystemCommentBlock.Comment, Constants.MAXCMMTLEN);
            
            longNameFileSystemCommentBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 0x14);
            longNameFileSystemCommentBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}