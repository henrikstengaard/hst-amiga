﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public static class Pfs3Helper
    {
        public static async Task<globaldata> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            return await Mount(stream, partitionBlock.Sectors, partitionBlock.BlocksPerTrack,
                partitionBlock.Surfaces, partitionBlock.LowCyl, partitionBlock.HighCyl, partitionBlock.NumBuffer,
                partitionBlock.FileSystemBlockSize);
        }

        public static async Task<globaldata> Mount(Stream stream, uint sectors, uint blocksPerTrack, uint surfaces, uint lowCyl,
            uint highCyl, uint numBuffer, uint fileSystemBlockSize)
        {
            var g = Init.CreateGlobalData(sectors, blocksPerTrack, surfaces, lowCyl, highCyl, numBuffer,
                fileSystemBlockSize);
            g.stream = stream;

            Init.Initialize(g);
            
            var rootBlock = await Volume.GetCurrentRoot(g);
            
            await Volume.DiskInsertSequence(rootBlock, g);

            return g;
        }

        public static async Task Unmount(globaldata g)
        {
            await Update.UpdateDisk(g);
        }
    }
}