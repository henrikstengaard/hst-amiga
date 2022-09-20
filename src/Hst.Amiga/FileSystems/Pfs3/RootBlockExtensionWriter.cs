﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class RootBlockExtensionWriter
    {
        public static async Task<byte[]> BuildBlock(rootblockextension rootblockextension)
        {
            var blockStream = rootblockextension.BlockBytes == null || rootblockextension.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(rootblockextension.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(rootblockextension.id); // 0
            await blockStream.WriteBigEndianUInt16(rootblockextension.not_used_1); // 2
            await blockStream.WriteBigEndianUInt32(rootblockextension.ext_options); // 4
            await blockStream.WriteBigEndianUInt32(rootblockextension.datestamp); // 8
            await blockStream.WriteBigEndianUInt32(rootblockextension.pfs2version); // 12
            await DateHelper.WriteDate(blockStream, rootblockextension.RootDate); // 16
            await DateHelper.WriteDate(blockStream, rootblockextension.VolumeDate); // 22
            
            await blockStream.WriteBigEndianUInt32(rootblockextension.tobedone.operation_id); // 28
            await blockStream.WriteBigEndianUInt32(rootblockextension.tobedone.argument1); // 32
            await blockStream.WriteBigEndianUInt32(rootblockextension.tobedone.argument2); // 36
            await blockStream.WriteBigEndianUInt32(rootblockextension.tobedone.argument3); // 40

            await blockStream.WriteBigEndianUInt32(rootblockextension.reserved_roving); // 44
            await blockStream.WriteBigEndianUInt16(rootblockextension.rovingbit); // 48
            await blockStream.WriteBigEndianUInt16(rootblockextension.curranseqnr); // 50
            await blockStream.WriteBigEndianUInt16(rootblockextension.deldirroving); // 52
            await blockStream.WriteBigEndianUInt16(rootblockextension.deldirsize); // 54
            await blockStream.WriteBigEndianUInt16(rootblockextension.fnsize); // 56

            // not_used_2[3]
            await blockStream.WriteBigEndianUInt16(0); // 58
            await blockStream.WriteBigEndianUInt16(0); // 60
            await blockStream.WriteBigEndianUInt16(0); // 62
            
            foreach (var superindex in rootblockextension.superindex)
            {
                await blockStream.WriteBigEndianUInt32(superindex); // 64
            }
            
            await blockStream.WriteBigEndianUInt16(rootblockextension.dd_uid); // 128 = (64 + (16 * 4))
            await blockStream.WriteBigEndianUInt16(rootblockextension.dd_gid); // 130
            await blockStream.WriteBigEndianUInt32(rootblockextension.dd_protection); // 132
            await DateHelper.WriteDate(blockStream, rootblockextension.dd_creationdate); // 136
            
            // not_used_3
            await blockStream.WriteBigEndianUInt16(0); // 142
            
            foreach (var deldir in rootblockextension.deldir)
            {
                await blockStream.WriteBigEndianUInt32(deldir); // 144 ...
            }
            
            var blockBytes = blockStream.ToArray();
            rootblockextension.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}