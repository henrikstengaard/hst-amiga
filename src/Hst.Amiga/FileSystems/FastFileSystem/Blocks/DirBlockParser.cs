namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Amiga.Extensions;
    using Core.Converters;

    public static class DirBlockParser
    {
        public static DirBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_HEADER)
            {
                throw new IOException("Invalid dir block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x8);
            var indexSize = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0xc); // hashtable & data blocks
            var firstData = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid dir block checksum");
            }
            
            var index = new List<int>();
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var access = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x140);
            var comment = blockBytes.ReadStringWithLength(0x148);

            var date = DateHelper.ReadDate(blockBytes, 0x1a4);
            var name = blockBytes.ReadStringWithLength(0x1b0);

            var realEntry = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1d4);
            var nextLink = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1d8);

            var nextSameHash = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0);
            var parent = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f4);
            var extension = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f8);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0 + (SizeOf.Long * 3));

            if (secType != Constants.ST_DIR)
            {
                throw new IOException($"Invalid dir block sec type '{type}'");
            }
            
            return new DirBlock
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                FirstData = firstData,
                Checksum = checksum,
                IndexSize = indexSize,
                Index = index.ToArray(),
                Access = access,
                Comment = comment,
                Date = date,
                Name = name,
                RealEntry = realEntry,
                NextLink = nextLink,
                NextSameHash = nextSameHash,
                Parent = parent,
                Extension = extension
            };
        }
    }
}