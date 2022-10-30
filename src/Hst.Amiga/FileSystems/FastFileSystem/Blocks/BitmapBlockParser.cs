namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Core.Converters;

    public static class BitmapBlockParser
    {
        public static BitmapBlock Parse(byte[] blockBytes)
        {
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes);

            if (calculatedChecksum != checksum)
            {
                throw new IOException("Invalid bitmap block checksum");
            }

            var map = new List<uint>();
            var entries = (blockBytes.Length - SizeOf.ULong) / SizeOf.ULong;
            for (var i = 0; i < entries; i++)
            {
                var entry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4 + i * SizeOf.ULong);
                map.Add(entry);
            }

            return new BitmapBlock(blockBytes.Length)
            {
                Checksum = checksum,
                BlockBytes = blockBytes,
                Map = map.ToArray()
            };
        }
    }
}