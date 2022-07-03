﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class BitmapBlockWriter
    {
        public static async Task<byte[]> BuildBlock(BitmapBlock bitmapBlock)
        {
            var blockStream =
                new MemoryStream(
                    bitmapBlock.BlockBytes == null || bitmapBlock.BlockBytes.Length == 0
                        ? new byte[512]
                        : bitmapBlock.BlockBytes);
            
            await blockStream.WriteBigEndianInt32(0); // checksum

            foreach (var map in bitmapBlock.BlocksFreeMap.ChunkBy(32))
            {
                var mapBytes = MapBlockHelper.ConvertBlockFreeMapToByteArray(map.ToArray());
                await blockStream.WriteBytes(mapBytes);
            }
                
            // calculate and update checksum
            var bitmapBytes = blockStream.ToArray();
            bitmapBlock.Checksum = ChecksumHelper.UpdateChecksum(bitmapBytes, 0);
            bitmapBlock.BlockBytes = bitmapBytes;

            return bitmapBytes;            
        }
    }
}