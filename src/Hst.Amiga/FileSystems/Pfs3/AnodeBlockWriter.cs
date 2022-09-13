namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class AnodeBlockWriter
    {
        public static async Task<byte[]> BuildBlock(anodeblock anodeblock)
        {
            var blockStream = anodeblock.BlockBytes == null || anodeblock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(anodeblock.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(anodeblock.id);
            await blockStream.WriteBigEndianUInt16(0); // not_used
            await blockStream.WriteBigEndianUInt32(anodeblock.datestamp);
            await blockStream.WriteBigEndianUInt32(anodeblock.seqnr);
            await blockStream.WriteBigEndianUInt32(0); // not_used2
                
            foreach (var anode in anodeblock.nodes)
            {
                await blockStream.WriteBigEndianUInt32(anode.clustersize);
                await blockStream.WriteBigEndianUInt32(anode.blocknr);
                await blockStream.WriteBigEndianUInt32(anode.next);
            }
            
            var blockBytes = blockStream.ToArray();
            anodeblock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}