namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;

    /// <summary>
    /// Writes rigid disk block and partition blocks back to the blocks they were read from. Unlike
    /// RigidDiskBlockWriter, block pointers and the layout of the rigid disk block area are not changed.
    /// </summary>
    internal static class RigidDiskBlockUpdater
    {
        private const int RdbLocationLimit = 16;
        private const int SectorSize = 512;

        public static async Task Write(Stream stream, RigidDiskBlock rigidDiskBlock)
        {
            var rdbSector = await FindRigidDiskBlockSector(stream);
            if (rdbSector < 0)
            {
                throw new PartitionOperationException("Rigid disk block not found");
            }

            var rigidDiskBlockBytes = await RigidDiskBlockWriter.BuildBlock(rigidDiskBlock);
            stream.Seek((long)rdbSector * SectorSize, SeekOrigin.Begin);
            await stream.WriteBytes(rigidDiskBlockBytes);

            var partitionList = rigidDiskBlock.PartitionList;

            foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks.ToList())
            {
                if (partitionList == BlockIdentifiers.EndOfBlock || partitionList == 0)
                {
                    throw new PartitionOperationException("Partition list is shorter than partition blocks");
                }

                var partitionBlockBytes = await PartitionBlockWriter.BuildBlock(partitionBlock);
                stream.Seek((long)partitionList * rigidDiskBlock.BlockSize, SeekOrigin.Begin);
                await stream.WriteBytes(partitionBlockBytes);

                partitionList = partitionBlock.NextPartitionBlock;
            }

            await stream.FlushAsync();
        }

        private static async Task<int> FindRigidDiskBlockSector(Stream stream)
        {
            var buffer = new byte[SectorSize];
            for (var sector = 0; sector < RdbLocationLimit; sector++)
            {
                stream.Seek((long)sector * SectorSize, SeekOrigin.Begin);
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read < 4)
                {
                    return -1;
                }

                if (BitConverter.ToUInt32(buffer, 0) == BlockIdentifiers.RigidDiskBlock)
                {
                    return sector;
                }
            }

            return -1;
        }
    }
}
