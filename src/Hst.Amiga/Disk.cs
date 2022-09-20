﻿namespace Hst.Amiga
{
    using System;
    using System.IO;
    using System.Linq;
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

        public static async Task FindUsedSectors(Stream stream, int blockSize, Func<long, byte[], Task> handler)
        {
            if (blockSize % 512 != 0)
            {
                throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
            }
            
            var blockBytes = new byte[blockSize];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(blockBytes, 0, blockBytes.Length);

                // skip unused sector where all bytes are zero
                if (blockBytes.All(x => x == 0))
                {
                    continue;
                }
                
                await handler(stream.Position, blockBytes);
            } while (bytesRead == blockSize);
        }
    }
}