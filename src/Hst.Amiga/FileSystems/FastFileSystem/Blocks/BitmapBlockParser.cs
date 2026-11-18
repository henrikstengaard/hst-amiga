namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Core.Converters;

    internal static class BitmapBlockParser
    {
        public static BitmapBlock Parse(byte[] blockBytes)
        {
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes);

            if (calculatedChecksum != checksum)
            {
                throw new IOException("Invalid bitmap block checksum");
            }

            var entries = (blockBytes.Length - SizeOf.ULong) / SizeOf.ULong;
            var map = BigEndianArrays.ReadUInt32Array(blockBytes, 0x4, entries);

            return new BitmapBlock(blockBytes.Length)
            {
                Checksum = checksum,
                BlockBytes = blockBytes,
                Map = map
            };
        }
    }
}