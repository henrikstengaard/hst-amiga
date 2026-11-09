namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using Core.Converters;

    /// <summary>
    /// Checksum helper for smart file system blocks.
    /// Checksum is calculated by summing all longs in block starting with value 1.
    /// The checksum stored at offset 4 is the negated sum with checksum field set to 0,
    /// which makes the sum of a valid block equal to 0.
    /// </summary>
    public static class ChecksumHelper
    {
        public const int ChecksumOffset = 4;

        /// <summary>
        /// Calculate sum of block, CALCCHECKSUM() in smart file system source.
        /// </summary>
        public static uint CalculateSum(byte[] blockBytes)
        {
            uint sum = 1;
            for (var i = 0; i + 3 < blockBytes.Length; i += 4)
            {
                unchecked
                {
                    sum += BigEndianConverter.ConvertBytesToUInt32(blockBytes, i);
                }
            }

            return sum;
        }

        /// <summary>
        /// Calculate checksum for block without changing block.
        /// </summary>
        public static uint CalculateChecksum(byte[] blockBytes)
        {
            var storedChecksum = BigEndianConverter.ConvertBytesToUInt32(blockBytes, ChecksumOffset);
            unchecked
            {
                var sum = CalculateSum(blockBytes) - storedChecksum;
                return (uint)-(int)sum;
            }
        }

        /// <summary>
        /// Update checksum in block, setchecksum() in smart file system source.
        /// </summary>
        public static uint UpdateChecksum(byte[] blockBytes)
        {
            var checksum = CalculateChecksum(blockBytes);
            BigEndianConverter.ConvertUInt32ToBytes(checksum, blockBytes, ChecksumOffset);
            return checksum;
        }

        /// <summary>
        /// Checks if checksum of block is valid.
        /// </summary>
        public static bool IsValid(byte[] blockBytes) => CalculateSum(blockBytes) == 0;
    }
}
