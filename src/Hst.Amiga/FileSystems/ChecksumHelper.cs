namespace Hst.Amiga.FileSystems
{
    using System;
    using System.Buffers.Binary;
    using Core.Converters;

    public static class ChecksumHelper
    {
        public static int CalculateChecksum(byte[] blockBytes, int checksumOffset, int length = 0)
        {
            var end = length > 0 ? length : blockBytes.Length;
            var checksum = 0;
            for (var offset = 0; offset < end; offset += 4)
            {
                // skip checksum offset
                if (offset == checksumOffset)
                {
                    continue;
                }

                checksum += BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(blockBytes, offset, 4));
            }

            checksum = -checksum;
            return checksum;
        }

        /// <summary>
        /// updates checksum for block bytes by calculating checksum and writing it at checksum offset
        /// </summary>
        /// <param name="blockBytes"></param>
        /// <param name="checksumOffset"></param>
        /// <param name="length"></param>
        public static int UpdateChecksum(byte[] blockBytes, int checksumOffset, int length = 0)
        {
            var checksum = CalculateChecksum(blockBytes, checksumOffset, length > 0 ? length : blockBytes.Length);
            BigEndianConverter.ConvertInt32ToBytes(checksum, blockBytes, checksumOffset);
            return checksum;
        }
    }
}