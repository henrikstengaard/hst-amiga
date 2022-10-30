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
            var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file ext block checksum");
            }

            var indexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
            
            var index = new List<uint>();
            for (var i = 0; i < indexSize; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var info = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x14);
            var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x10);
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x0c);
            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x08);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x04);
            
            if (secType != Constants.ST_FILE)
            {
                throw new IOException($"Invalid secondary type '{secType}'");
            }

            return new FileExtBlock(blockBytes.Length)
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