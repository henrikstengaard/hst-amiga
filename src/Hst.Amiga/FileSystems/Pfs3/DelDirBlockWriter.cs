namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class DelDirBlockWriter
    {
        public static byte[] BuildBlock(deldirblock delDirBlock, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (delDirBlock.BlockBytes != null)
            {
                Array.Copy(delDirBlock.BlockBytes, 0, blockBytes, 0,
                    Math.Min(delDirBlock.BlockBytes.Length, g.RootBlock.ReservedBlksize));
            }
            
            BigEndianConverter.ConvertUInt16ToBytes(delDirBlock.id, blockBytes, 0);
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0x2); // not used 1
            BigEndianConverter.ConvertUInt32ToBytes(delDirBlock.datestamp, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(delDirBlock.seqnr, blockBytes, 0x8);

            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0xc); // not used 2
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0xe); // not used 2
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0x10); // not used 3
            
            BigEndianConverter.ConvertUInt16ToBytes(delDirBlock.uid, blockBytes, 0x12);
            BigEndianConverter.ConvertUInt16ToBytes(delDirBlock.gid, blockBytes, 0x14);
            BigEndianConverter.ConvertUInt32ToBytes(delDirBlock.protection, blockBytes, 0x16);
            DateHelper.WriteDate(delDirBlock.CreationDate, blockBytes, 0x1a);

            var offset = 0x20; // first del dir entry offset
            foreach (var entry in delDirBlock.entries)
            {
                DelDirEntryWriter.Write(blockBytes, offset, entry);
                offset += SizeOf.DelDirEntry.Struct;
            }
            
            delDirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}