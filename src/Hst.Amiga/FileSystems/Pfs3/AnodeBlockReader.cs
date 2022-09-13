namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class AnodeBlockReader
    {
        public static async Task<anodeblock> Parse(byte[] blockBytes, globaldata g)
        {
            var blockStream = new MemoryStream(blockBytes);

            var id = await blockStream.ReadBigEndianUInt16();
            var not_used = await blockStream.ReadBigEndianUInt16();
            var datestamp = await blockStream.ReadBigEndianUInt32();
            var seqnr = await blockStream.ReadBigEndianUInt32();
            var notUsed2 = await blockStream.ReadBigEndianUInt32();

            if (id == 0)
            {
                return null;
            }

            var nodes = new List<anode>();
            var nodesCount = (g.RootBlock.ReservedBlksize - SizeOf.UWORD * 2 - SizeOf.ULONG * 3) / (SizeOf.ULONG * 3);
            for (var i = 0; i < nodesCount; i++)
            {
                var clustersize = await blockStream.ReadBigEndianUInt32();
                var blocknr = await blockStream.ReadBigEndianUInt32();
                var next = await blockStream.ReadBigEndianUInt32();
                
                nodes.Add(new anode
                {
                    clustersize = clustersize,
                    blocknr = blocknr,
                    next = next
                });
            }
            
            return new anodeblock(g)
            {
                id = id,
                not_used_1 = not_used,
                datestamp = datestamp,
                seqnr = seqnr,
                nodes = nodes.ToArray()
            };
        }
    }
}