namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class DirCacheBlockReader
    {
        public static async Task<DirCacheBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var type = await blockStream.ReadBigEndianInt32();
            var headerKey = await blockStream.ReadBigEndianInt32();
            var parent = await blockStream.ReadBigEndianInt32();
            var recordsNb = await blockStream.ReadBigEndianInt32();
            var nextDirC = await blockStream.ReadBigEndianInt32();
            var checksum = await blockStream.ReadBigEndianInt32();
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid dir cache block checksum");
            }
            
            var records = await blockStream.ReadBytes(488);
            
            return new DirCacheBlock
            {
                BlockBytes = blockBytes,
                Type = type,
                HeaderKey = headerKey,
                Parent = parent,
                RecordsNb = recordsNb,
                NextDirC = nextDirC,
                CheckSum = checksum,
                Records = records
            };
        }
    }
}