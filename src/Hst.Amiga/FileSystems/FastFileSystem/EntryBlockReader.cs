namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Core.Converters;
    using Amiga.Extensions;

    public static class EntryBlockReader
    {
        public static EntryBlock Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            var headerKey = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x8);
            var indexSize = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0xc); // hashtable & data blocks
            var firstData = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid entry block checksum");
            }
            
            var index = new List<int>();
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var access = 0;
            var byteSize = 0;
            var comment = string.Empty;
            var date = DateTime.MinValue;
            var name = string.Empty;
            var realEntry = 0;
            var nextLink = 0;

            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0 + (SizeOf.Long * 3));

            if (type != Constants.ST_ROOT)
            {
                access = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x140);
                byteSize = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x144);
                comment = blockBytes.ReadStringWithLength(0x148);

                date = DateHelper.ReadDate(blockBytes, 0x1a4);
                name = blockBytes.ReadStringWithLength(0x1b0);

                blockStream.Seek(0x1d4, SeekOrigin.Begin);
                realEntry = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1d4);
                nextLink = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1d8);
            }

            var nextSameHash = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0);
            var parent = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f4);
            var extension = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f8);

            return new EntryBlock
            {
                BlockBytes = blockBytes,
                Type = type,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                FirstData = firstData,
                Checksum = checksum,
                IndexSize = indexSize,
                Index = index.ToArray(),
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