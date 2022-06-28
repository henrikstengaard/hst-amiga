namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public abstract class RigidDiskBlockTestBase
    {
        protected readonly byte[] Pds3DosType = new byte[] { 0x50, 0x44, 0x53, 0x3 };
        
        protected RigidDiskBlock CreateRigidDiskBlock(long size)
        {
            var rigidDiskBlock = new RigidDiskBlock();
            
            var blocksPerCylinder = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
            var cylinders = (uint)Math.Floor((double)size / (blocksPerCylinder * rigidDiskBlock.BlockSize));

            rigidDiskBlock.DiskSize = size;
            rigidDiskBlock.Cylinders = cylinders;
            rigidDiskBlock.ParkingZone = cylinders;
            rigidDiskBlock.ReducedWrite = cylinders;
            rigidDiskBlock.WritePreComp = cylinders;

            return rigidDiskBlock;
        }
        
        protected PartitionBlock CreatePartitionBlock(RigidDiskBlock rigidDiskBlock, uint startCylinder, long size,
            byte[] dosType, string driveName, uint reserved = 2, bool bootable = false)
        {
            var blocksPerCylinder = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
            var cylinders = (uint)Math.Floor((double)size / (blocksPerCylinder * rigidDiskBlock.BlockSize));

            return new PartitionBlock
            {
                DosType = dosType,
                DriveName = driveName,
                Flags = bootable ? (uint)PartitionBlock.PartitionFlagsEnum.Bootable : 0,
                Reserved = reserved,
                LowCyl = startCylinder + reserved,
                HighCyl = startCylinder + cylinders
            };
        }

        protected async Task<FileSystemHeaderBlock> CreateFileSystemHeaderBlock()
        {
            return BlockHelper.CreateFileSystemHeaderBlock(Pds3DosType, 19, 2,
                    await File.ReadAllBytesAsync(@"TestData\pfs3aio"));
        }
    }
}