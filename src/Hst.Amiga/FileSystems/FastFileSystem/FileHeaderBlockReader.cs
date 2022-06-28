namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;

    public static class FileHeaderBlockReader
    {
        public static async Task<FileHeaderBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var type = await blockStream.ReadBigEndianInt32();
            var headerKey = await blockStream.ReadBigEndianInt32();
            var highSeq = await blockStream.ReadBigEndianInt32();
            var dataSize = await blockStream.ReadBigEndianInt32();
            var firstData = await blockStream.ReadBigEndianInt32();
            var checksum = await blockStream.ReadBigEndianInt32();

            var hashTable = new List<int>();
            for (var i = 0; i < Constants.MAX_DATABLK; i++)
            {
                hashTable.Add(await blockStream.ReadBigEndianInt32());
            }

            // r1 og r2
            for (var i = 0; i < 2; i++)
            {
                await blockStream.ReadBigEndianInt32();
            }
            
            var access = await blockStream.ReadBigEndianInt32();
            var byteSize = await blockStream.ReadBigEndianInt32();
            var comment = await blockStream.ReadString();

            blockStream.Seek(0x1a4, SeekOrigin.Begin);
            var date = await DateHelper.ReadDate(blockStream);
            var name = await blockStream.ReadString();

            blockStream.Seek(0x1d4, SeekOrigin.Begin);
            var realEntry = await blockStream.ReadBigEndianInt32();
            var nextLink = await blockStream.ReadBigEndianInt32();

            blockStream.Seek(0x1f0, SeekOrigin.Begin);
            var nextSameHash = await blockStream.ReadBigEndianInt32();
            var parent = await blockStream.ReadBigEndianInt32();
            var extension = await blockStream.ReadBigEndianInt32();
            var secType = await blockStream.ReadBigEndianInt32();

            return new FileHeaderBlock
            {
                BlockBytes = blockBytes,
                Type = type,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                DataSize = dataSize,
                FirstData = firstData,
                Checksum = checksum,
                HashTable = hashTable.ToArray(),
                Access = access,
                ByteSize = byteSize,
                Comment = comment,
                Date = date,
                Name = name,
                RealEntry = realEntry,
                NextLink = nextLink,
                NextSameHash = nextSameHash,
                Parent = parent,
                Extension = extension,
                SecType = secType
            };
        }
    }
}