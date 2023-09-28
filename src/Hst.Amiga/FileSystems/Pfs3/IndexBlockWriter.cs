namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class IndexBlockWriter
    {
        public static byte[] BuildBlock(indexblock indexBlock, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (indexBlock.BlockBytes != null)
            {
                Array.Copy(indexBlock.BlockBytes, 0, blockBytes, 0,
                    Math.Min(indexBlock.BlockBytes.Length, g.RootBlock.ReservedBlksize));
            }
                
            BigEndianConverter.ConvertUInt16ToBytes(indexBlock.id, blockBytes, 0);
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0x2); // not_used 1
            BigEndianConverter.ConvertUInt32ToBytes(indexBlock.datestamp, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(indexBlock.seqnr, blockBytes, 0x8);

            var offset = 0xc;
            var indexCount = (g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) /
                             Amiga.SizeOf.Long;
            for (var i = 0; i < indexCount; i++)
            {
                BigEndianConverter.ConvertInt32ToBytes(indexBlock.index[i], blockBytes, offset);
                offset += Amiga.SizeOf.Long;
            }
                
            indexBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}