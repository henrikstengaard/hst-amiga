namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Core.Converters;

    public static class FileExtBlockParser
    {
        public static FileExtBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_LIST)
            {
                throw new IOException("Invalid file ext block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var indexSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc); // hashtable & data blocks
            var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file ext block checksum");
            }
            
            var index = new List<uint>();
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var info = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1ec);
            var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f0);
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f4);
            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f8);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1fc);
            
            if (secType != Constants.ST_FILE)
            {
                throw new IOException($"Invalid secondary type '{secType}'");
            }

            return new FileExtBlock
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                IndexSize = indexSize,
                FirstData = firstData,
                Checksum = checksum,
                Index = index.ToArray(),
                Info = info,
                NextSameHash = nextSameHash,
                Parent = parent,
                Extension = extension,
            };
        }
    }
}