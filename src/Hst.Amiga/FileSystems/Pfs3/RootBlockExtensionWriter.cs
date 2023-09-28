namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class RootBlockExtensionWriter
    {
        public static byte[] BuildBlock(rootblockextension rootblockextension, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (rootblockextension.BlockBytes != null)
            {
                Array.Copy(rootblockextension.BlockBytes, 0, blockBytes, 0,
                    Math.Min(rootblockextension.BlockBytes.Length, g.RootBlock.ReservedBlksize));
            }
            
            BigEndianConverter.ConvertUInt16ToBytes(Constants.EXTENSIONID, blockBytes, 0); // 0
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.not_used_1, blockBytes, 2); // 2
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.ext_options, blockBytes, 4); // 4
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.datestamp, blockBytes, 8); // 8
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.pfs2version, blockBytes, 12); // 12
            DateHelper.WriteDate(rootblockextension.RootDate, blockBytes, 16); // 16
            DateHelper.WriteDate(rootblockextension.VolumeDate, blockBytes, 22); // 22
            
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.tobedone.operation_id, blockBytes, 28); // 28
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.tobedone.argument1, blockBytes, 32); // 32
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.tobedone.argument2, blockBytes, 36); // 36
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.tobedone.argument3, blockBytes, 40); // 40

            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.reserved_roving, blockBytes, 44); // 44
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.rovingbit, blockBytes, 48); // 48
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.curranseqnr, blockBytes, 50); // 50
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.deldirroving, blockBytes, 52); // 52
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.deldirsize, blockBytes, 54); // 54
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.fnsize, blockBytes, 56); // 56

            // not_used_2[3]
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 58); // 58
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 60); // 60
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 62); // 62

            var offset = 64;
            foreach (var superindex in rootblockextension.superindex)
            {
                BigEndianConverter.ConvertUInt32ToBytes(superindex, blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }
            
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.dd_uid, blockBytes, 128); // 128 = (64 + (16 * 4))
            BigEndianConverter.ConvertUInt16ToBytes(rootblockextension.dd_gid, blockBytes, 130); // 130
            BigEndianConverter.ConvertUInt32ToBytes(rootblockextension.dd_protection, blockBytes, 132); // 132
            DateHelper.WriteDate(rootblockextension.dd_creationdate, blockBytes, 136); // 136
            
            // not_used_3
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 142); // 142
            
            offset = 144;
            foreach (var deldir in rootblockextension.deldir)
            {
                BigEndianConverter.ConvertUInt32ToBytes(deldir, blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }
            
            rootblockextension.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}