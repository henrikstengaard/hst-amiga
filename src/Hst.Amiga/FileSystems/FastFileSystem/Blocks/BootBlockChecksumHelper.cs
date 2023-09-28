namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using Core.Converters;

    public static class BootBlockChecksumHelper
    {
        public static int CalculateChecksum(byte[] blockBytes, int checksumOffset = 4)
        {
            if (blockBytes.Length != 1024)
            {
                throw new ArgumentException("Boot block must be 1024 bytes", nameof(blockBytes));
            }
        
            long newSum = 0;
            for (var offset = 0; offset < 1024; offset += 4)
            {
                if (offset == checksumOffset)
                {
                    continue;
                }

                newSum += BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset);

                if (newSum > 0xffffffff)
                {
                    // Int32 overflow
                    newSum -= 0x100000000; // Simulating overflow
                    newSum++; // part of the boot block checksum calculation
                }
            }
            return (int)(~newSum >> 0);
        }    

        public static int UpdateChecksum(byte[] blockBytes, int checksumOffset)
        {
            var checksum = CalculateChecksum(blockBytes, checksumOffset);
            BigEndianConverter.ConvertInt32ToBytes(checksum, blockBytes, checksumOffset);
            return checksum;
        }
    }
}