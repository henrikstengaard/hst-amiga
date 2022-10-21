namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;
    using Extensions;

    public static class RootBlockWriter
    {
        public static async Task<byte[]> BuildBlock(RootBlock rootBlock)
        {
            var blockStream = rootBlock.BlockBytes == null || rootBlock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(rootBlock.BlockBytes);
            
            await blockStream.WriteBigEndianInt32(rootBlock.DiskType); // 0
            await blockStream.WriteBigEndianUInt32((uint)rootBlock.Options); // 4
            await blockStream.WriteBigEndianUInt32(rootBlock.Datestamp); // 8
            await DateHelper.WriteDate(blockStream, rootBlock.CreationDate); // 12
            await blockStream.WriteBigEndianUInt16(rootBlock.Protection); // 18
            blockStream.WriteByte((byte)(rootBlock.DiskName.Length > 31 ? 31 : rootBlock.DiskName.Length)); // 20
            await blockStream.WriteString(rootBlock.DiskName, 31); // 21
            await blockStream.WriteBigEndianUInt32(rootBlock.LastReserved); // 52
            await blockStream.WriteBigEndianUInt32(rootBlock.FirstReserved); // 56
            await blockStream.WriteBigEndianUInt32(rootBlock.ReservedFree); // 60
            await blockStream.WriteBigEndianUInt16(rootBlock.ReservedBlksize); // 64
            await blockStream.WriteBigEndianUInt16(rootBlock.RblkCluster); // 66
            await blockStream.WriteBigEndianUInt32(rootBlock.BlocksFree); // 68
            await blockStream.WriteBigEndianUInt32(rootBlock.AlwaysFree); // 72
            await blockStream.WriteBigEndianUInt32(rootBlock.RovingPtr); // 76
            await blockStream.WriteBigEndianUInt32(rootBlock.DelDir); // 80
            await blockStream.WriteBigEndianUInt32(rootBlock.DiskSize); // 84
            await blockStream.WriteBigEndianUInt32(rootBlock.Extension); // 88
            await blockStream.WriteBigEndianUInt32(0); // not used, 92

            foreach (var t in rootBlock.idx.union)
            {
                await blockStream.WriteBigEndianUInt32(t);
            }

            if (rootBlock.ReservedBitmapBlock != null)
            {
                await blockStream.WriteBytes(await BitmapBlockWriter.BuildBlock(rootBlock.ReservedBitmapBlock));
            }
            
            var blockBytes = blockStream.ToArray();
            return blockBytes;            
        }
    }
}