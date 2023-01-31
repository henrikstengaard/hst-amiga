namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class BitmapBlockWriter
    {
        public static byte[] BuildBlock(BitmapBlock bitmapBlock, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (bitmapBlock.BlockBytes != null)
            {
                Array.Copy(bitmapBlock.BlockBytes, 0, blockBytes, 0, g.RootBlock.ReservedBlksize);
            }

            BigEndianConverter.ConvertUInt16ToBytes(bitmapBlock.id, blockBytes, 0);
            BigEndianConverter.ConvertUInt16ToBytes(bitmapBlock.not_used_1, blockBytes, 2);
            BigEndianConverter.ConvertUInt32ToBytes(bitmapBlock.datestamp, blockBytes, 4);
            BigEndianConverter.ConvertUInt32ToBytes(bitmapBlock.seqnr, blockBytes, 8);

            var offset = 0xc;
            for (var i = 0; i < Math.Min(bitmapBlock.bitmap.Length, (int)g.glob_allocdata.longsperbmb); i++)
            {
                BigEndianConverter.ConvertUInt32ToBytes(bitmapBlock.bitmap[i], blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }
            
            bitmapBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}