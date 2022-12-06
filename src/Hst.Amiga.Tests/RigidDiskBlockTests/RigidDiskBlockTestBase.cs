namespace Hst.Amiga.Tests.RigidDiskBlockTests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public abstract class RigidDiskBlockTestBase
    {
        protected const int FileSystemVersion = 19;
        protected const int FileSystemRevision = 2;
        protected readonly byte[] Pds3DosType = new byte[] { 0x50, 0x44, 0x53, 0x3 };
        protected readonly byte[] Dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };
        protected readonly byte[] FastFileSystemBytes = Encoding.ASCII.GetBytes(
            "$VER: FastFileSystem 1.0 (12/12/22) "); // dummy fast file system used for testing
        
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
            return BlockHelper.CreateFileSystemHeaderBlock(Pds3DosType, FileSystemVersion, FileSystemRevision,
                "pfs3aio", await File.ReadAllBytesAsync(Path.Combine("TestData", "RigidDiskBlocks", "pfs3aio")));
        }
    }
}