namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class DirCacheBlockParser
    {
        public static DirCacheBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            if (type != Constants.T_DIRC)
            {
                throw new IOException("Invalid dir cache block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var recordsNb = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            var nextDirC = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid dir cache block checksum");
            }

            var maxRecordsSize = blockBytes.Length - (SizeOf.ULong * 4) - (SizeOf.Long * 2);
            var records = new byte[maxRecordsSize];
            Array.Copy(blockBytes, 0x18, records, 0, maxRecordsSize);
            
            return new DirCacheBlock(blockBytes.Length)
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                Parent = parent,
                RecordsNb = recordsNb,
                NextDirC = nextDirC,
                Checksum = checksum,
                Records = records
            };
        }
    }
}