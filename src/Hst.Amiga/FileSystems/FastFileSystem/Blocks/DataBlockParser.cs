namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class DataBlockParser
    {
        public static DataBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_DATA)
            {
                throw new IOException($"Invalid data block type '{type}'");
            }

            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var seqNum = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var dataSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc); // hashtable & data blocks
            var nextData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            if (dataSize > 488)
            {
                throw new IOException($"Invalid data block data size '{dataSize}'");
            }
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid data block checksum");
            }

            var data = new byte[488];
            Array.Copy(blockBytes, 0x18, data, 0, dataSize);
            
            return new DataBlock
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                SeqNum = seqNum,
                DataSize = dataSize,
                NextData = nextData,
                Checksum = checksum,
                Data = data
            };
        }
    }
}