namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class DirBlockWriter
    {
        public static async Task<byte[]> BuildBlock(dirblock dirblock)
        {
            var blockStream = dirblock.BlockBytes == null || dirblock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(dirblock.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(dirblock.id);
            await blockStream.WriteBigEndianUInt16(dirblock.not_used_1);
            await blockStream.WriteBigEndianUInt32(dirblock.datestamp);
            
            // not_used_2
            for (var i = 0; i < 2; i++)
            {
                await blockStream.WriteBigEndianUInt16(0);
            }
            
            await blockStream.WriteBigEndianUInt32(dirblock.anodenr);
            await blockStream.WriteBigEndianUInt32(dirblock.parent);
            await blockStream.WriteBytes(dirblock.entries);
                
            var blockBytes = blockStream.ToArray();
            dirblock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}