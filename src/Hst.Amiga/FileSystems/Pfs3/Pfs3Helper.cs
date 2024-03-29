﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public static class Pfs3Helper
    {
        public static async Task<globaldata> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            return await Mount(stream, partitionBlock.Sectors, partitionBlock.BlocksPerTrack,
                partitionBlock.Surfaces, partitionBlock.LowCyl, partitionBlock.HighCyl, partitionBlock.NumBuffer,
                partitionBlock.BlockSize, partitionBlock.Mask);
        }

        public static async Task<globaldata> Mount(Stream stream, uint sectors, uint blocksPerTrack, uint surfaces, uint lowCyl,
            uint highCyl, uint numBuffer, uint blockSize, uint mask)
        {
            var g = Init.CreateGlobalData(sectors, blocksPerTrack, surfaces, lowCyl, highCyl, numBuffer, mask);
            g.stream = stream;

            Init.Initialize(g);
            
            var rootBlock = await Volume.GetCurrentRoot(g);
            
            await Volume.DiskInsertSequence(rootBlock, g);

            return g;
        }

        public static async Task Flush(globaldata g)
        {
            if (g.stream.CanWrite)
            {
                await Update.UpdateDisk(g);
            }
            
            Volume.FreeVolumeResources(g.currentvolume, g);
            await g.stream.FlushAsync();
        }

        public static int CalculateBitmapBlocksCount(int bitmapsCount, globaldata g)
        {
            var bitmapsPerBlock = g.blocksize / Amiga.SizeOf.ULong;
            var bitmapsPerFirstBlock = (g.blocksize - (Amiga.SizeOf.UWord * 2) - (Amiga.SizeOf.ULong * 2)) / Amiga.SizeOf.ULong;

            return bitmapsCount > bitmapsPerFirstBlock
                ? Convert.ToInt32(Math.Ceiling((double)(bitmapsCount - bitmapsPerFirstBlock) / bitmapsPerBlock) + 1)
                : 1;
        }
    }
}