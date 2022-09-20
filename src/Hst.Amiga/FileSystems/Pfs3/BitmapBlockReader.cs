﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class BitmapBlockReader
    {
        public static async Task<BitmapBlock> Parse(byte[] blockBytes, globaldata g)
        {
            var blockStream = new MemoryStream(blockBytes);

            var id = await blockStream.ReadBigEndianUInt16();
            
            if (id == 0)
            {
                return null;
            }
            
            var not_used = await blockStream.ReadBigEndianUInt16();
            var datestamp = await blockStream.ReadBigEndianUInt32();
            var seqnr = await blockStream.ReadBigEndianUInt32();
            
            var bitmap = new List<uint>();
            var bitmapCount = (blockBytes.Length - SizeOf.UWORD * 2 - SizeOf.ULONG * 2) / SizeOf.ULONG;
            for (var i = 0; i < bitmapCount; i++)
            {
                bitmap.Add(await blockStream.ReadBigEndianUInt32());
            }
            
            return new BitmapBlock(g)
            {
                id = id,
                not_used_1 = not_used,
                datestamp = datestamp,
                seqnr = seqnr,
                bitmap = bitmap.ToArray()
            };
        }
    }
}