﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;
    using Extensions;

    public static class DelDirBlockWriter
    {
        public static async Task<byte[]> BuildBlock(deldirblock deldirblock)
        {
            var blockStream = deldirblock.BlockBytes == null || deldirblock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(deldirblock.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(deldirblock.id); // 0
            await blockStream.WriteBigEndianUInt16(deldirblock.not_used_1); // 2
            await blockStream.WriteBigEndianUInt32(deldirblock.datestamp); // 4
            await blockStream.WriteBigEndianUInt32(deldirblock.seqnr); // 8

            // not_used_2[2] + not_used_3
            await blockStream.WriteBigEndianUInt16(0); // 12
            await blockStream.WriteBigEndianUInt16(0); // 14
            await blockStream.WriteBigEndianUInt16(0); // 16
            
            await blockStream.WriteBigEndianUInt16(deldirblock.uid); // 18
            await blockStream.WriteBigEndianUInt16(deldirblock.gid); // 20
            await blockStream.WriteBigEndianUInt32(deldirblock.protection); // 22
            await DateHelper.WriteDate(blockStream, deldirblock.CreationDate); // 26
            
            foreach (var entry in deldirblock.entries)
            {
                await blockStream.WriteBigEndianUInt32(entry.anodenr); // 32...
                await blockStream.WriteBigEndianUInt32(entry.fsize); // 36...
                await DateHelper.WriteDate(blockStream, entry.CreationDate); // 40... 
                await blockStream.WriteString(entry.filename, 16); // 46 ...
                await blockStream.WriteBigEndianUInt16(entry.fsizex); //
            }
            
            var blockBytes = blockStream.ToArray();
            deldirblock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}