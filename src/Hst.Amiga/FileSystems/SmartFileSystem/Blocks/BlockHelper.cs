namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class BlockHelper
    {
        /// <summary>
        /// Read block header: id, checksum and own block.
        /// </summary>
        public static void ReadHeader(byte[] blockBytes, Block block)
        {
            block.Id = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0);
            block.Checksum = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4);
            block.OwnBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8);
        }

        /// <summary>
        /// Read block header and verify id.
        /// </summary>
        public static void ReadHeader(byte[] blockBytes, Block block, uint expectedId)
        {
            ReadHeader(blockBytes, block);
            if (block.Id != expectedId)
            {
                throw new IOException(
                    $"Invalid block id '{FormatId(block.Id)}', expected block id '{FormatId(expectedId)}'");
            }
        }

        /// <summary>
        /// Write block header and update checksum.
        /// </summary>
        public static void WriteHeader(byte[] blockBytes, Block block)
        {
            BigEndianConverter.ConvertUInt32ToBytes(block.Id, blockBytes, 0);
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 4);
            BigEndianConverter.ConvertUInt32ToBytes(block.OwnBlock, blockBytes, 8);
        }

        /// <summary>
        /// Update checksum of block bytes and block.
        /// </summary>
        public static byte[] UpdateChecksum(byte[] blockBytes, Block block)
        {
            block.Checksum = ChecksumHelper.UpdateChecksum(blockBytes);
            return blockBytes;
        }

        public static void ValidateBlockSize(int blockSize)
        {
            if (blockSize < 512 || blockSize > 32768 || (blockSize & (blockSize - 1)) != 0)
            {
                throw new ArgumentException($"Invalid block size {blockSize}, must be a power of 2 between 512 and 32768",
                    nameof(blockSize));
            }
        }

        /// <summary>
        /// Read null terminated string
        /// </summary>
        /// <returns>String and number of bytes read including null terminator.</returns>
        public static string ReadNullTerminatedString(byte[] bytes, int offset, out int bytesRead)
        {
            var length = 0;
            while (offset + length < bytes.Length && bytes[offset + length] != 0)
            {
                length++;
            }

            bytesRead = length + 1;
            return length == 0 ? string.Empty : AmigaTextHelper.Iso88591.GetString(bytes, offset, length);
        }

        public static string FormatId(uint id)
        {
            var chars = new char[4];
            for (var i = 0; i < 4; i++)
            {
                var c = (byte)(id >> (24 - i * 8));
                chars[i] = c >= 32 && c < 127 ? (char)c : '\\';
            }

            var text = new string(chars);
            return (id & 0xff) < 32 ? $"{text.Substring(0, 3)}\\{id & 0xff}" : text;
        }
    }
}
