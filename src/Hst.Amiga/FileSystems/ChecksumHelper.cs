namespace Hst.Amiga.FileSystems
{
    using Core.Converters;

    public static class ChecksumHelper
    {
        public static int CalculateChecksum(byte[] blockBytes, int checksumOffset)
        {
            var checksum = 0;
            for (var offset = 0; offset < blockBytes.Length; offset += 4)
            {
                var value = BigEndianConverter.ConvertBytesToInt32(blockBytes, offset);

                // skip checksum offset
                if (offset == checksumOffset)
                {
                    continue;
                }

                checksum += value;
            }

            checksum = -checksum;
            return checksum;
        }

        /// <summary>
        /// updates checksum for block bytes by calculating checksum and writing it at checksum offset
        /// </summary>
        /// <param name="blockBytes"></param>
        /// <param name="checksumOffset"></param>
        public static int UpdateChecksum(byte[] blockBytes, int checksumOffset)
        {
            var checksum = CalculateChecksum(blockBytes, checksumOffset);
            BigEndianConverter.ConvertInt32ToBytes(checksum, blockBytes, checksumOffset);
            return checksum;
        }
    }
}