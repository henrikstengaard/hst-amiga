namespace Hst.Amiga.FileSystems
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;

    public static class ChecksumHelper
    {
        public static async Task<int> CalculateChecksum(byte[] blockBytes, int checksumOffset)
        {
            int checksum = 0;
            var memoryStream = new MemoryStream(blockBytes);
            for (var offset = 0; offset < blockBytes.Length; offset += 4)
            {
                var value = await memoryStream.ReadBigEndianInt32();

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
        public static async Task<int> UpdateChecksum(byte[] blockBytes, int checksumOffset)
        {
            var checksum = await CalculateChecksum(blockBytes, checksumOffset);
            var checksumBytes = BigEndianConverter.ConvertInt32ToBytes(checksum);
            Array.Copy(checksumBytes, 0, blockBytes, checksumOffset, checksumBytes.Length);
            return checksum;
        }
    }
}