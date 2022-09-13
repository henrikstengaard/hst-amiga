namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class IndexBlockWriter
    {
        public static async Task<byte[]> BuildBlock(indexblock indexBlock)
        {
            var blockStream = indexBlock.BlockBytes == null || indexBlock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(indexBlock.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(indexBlock.id);
            await blockStream.WriteBigEndianUInt16(indexBlock.not_used_1);
            await blockStream.WriteBigEndianUInt32(indexBlock.datestamp);
            await blockStream.WriteBigEndianUInt32(indexBlock.seqnr);

            foreach (var t in indexBlock.index)
            {
                await blockStream.WriteBigEndianInt32(t);
            }
                
            var blockBytes = blockStream.ToArray();
            indexBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}