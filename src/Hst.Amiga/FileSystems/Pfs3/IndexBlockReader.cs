namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class IndexBlockReader
    {
        public static async Task<indexblock> Parse(byte[] blockBytes, globaldata g)
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

            var index = new List<int>();
            var indexCount = (blockBytes.Length - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) / Amiga.SizeOf.Long;
            for (var i = 0; i < indexCount; i++)
            {
                index.Add(await blockStream.ReadBigEndianInt32());
            }
            
            return new indexblock(g)
            {
                id = id,
                not_used_1 = not_used,
                datestamp = datestamp,
                seqnr = seqnr,
                index = index.ToArray()
            };
        }
    }
}