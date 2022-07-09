namespace Hst.Amiga
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public static class Disk
    {
        public static async Task<byte[]> ReadBlock(Stream stream, int blockSize)
        {
            if (blockSize % 512 != 0)
            {
                throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
            }
            
            var blockBytes = new byte[blockSize];
            var bytesRead = await stream.ReadAsync(blockBytes, 0, blockBytes.Length);

            if (bytesRead != blockBytes.Length)
            {
                throw new IOException($"Read block bytes only returned {bytesRead} bytes, but expected {blockSize} bytes");
            }
            
            return blockBytes;
        }

        public static async Task WriteBlock(Stream stream, byte[] blockBytes)
        {
            if (blockBytes.Length % 512 != 0)
            {
                throw new ArgumentException("Block bytes must be dividable by 512", nameof(blockBytes));
            }

            await stream.WriteAsync(blockBytes, 0, blockBytes.Length);
        }
    }
}