namespace Hst.Amiga.Extensions
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using RigidDiskBlocks;
    using VersionStrings;

    public static class RigidDiskBlockExtensions
    {
        public static RigidDiskBlock CreateRigidDiskBlock(this long size) => RigidDiskBlock.Create(size);

        public static RigidDiskBlock AddFileSystem(this RigidDiskBlock rigidDiskBlock,
            string dosType, byte[] fileSystemBytes, bool overwrite = false)
        {
            return AddFileSystem(rigidDiskBlock, DosTypeHelper.FormatDosType(dosType), string.Empty, fileSystemBytes, overwrite);
        }

        public static RigidDiskBlock AddFileSystem(this RigidDiskBlock rigidDiskBlock,
            byte[] dosType, byte[] fileSystemBytes, bool overwrite = false)
        {
            return AddFileSystem(rigidDiskBlock, dosType, string.Empty, fileSystemBytes, overwrite);
        }
        
        public static RigidDiskBlock AddFileSystem(this RigidDiskBlock rigidDiskBlock,
            byte[] dosType, string fileSystemName, byte[] fileSystemBytes, bool overwrite = false)
        {
            if (dosType.Length != 4)
            {
                throw new ArgumentException($"DOS Type must consist of 4 bytes", nameof(dosType));
            }
            
            if (!overwrite && rigidDiskBlock.FileSystemHeaderBlocks.Any(x => x.DosType.SequenceEqual(dosType)))
            {
                throw new ArgumentException($"DOS Type '{dosType.FormatDosType()}' already exists", nameof(dosType));
            }
            
            var version = VersionStringReader.Read(fileSystemBytes);
            var fileVersion = VersionStringReader.Parse(version);

            var fileSystemHeaderBlock = BlockHelper.CreateFileSystemHeaderBlock(dosType, fileVersion.Version,
                fileVersion.Revision, fileSystemName, fileSystemBytes);

            rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.Where(x => !x.DosType.SequenceEqual(dosType)).Concat(new[]
                { fileSystemHeaderBlock });

            return rigidDiskBlock;
        }

        public static RigidDiskBlock AddPartition(this RigidDiskBlock rigidDiskBlock,
            string driveName, long size = 0, bool bootable = false)
        {
            var firstFileSystemHeaderBlock = rigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault();

            if (firstFileSystemHeaderBlock == null)
            {
                throw new Exception("No file system header blocks");
            }

            return AddPartition(rigidDiskBlock, firstFileSystemHeaderBlock.DosType, driveName, size, bootable);
        }

        public static RigidDiskBlock AddPartition(this RigidDiskBlock rigidDiskBlock, byte[] dosType, string driveName,
            long size = 0,
            bool bootable = false)
        {
            var partitionBlock = PartitionBlock.Create(rigidDiskBlock, dosType, driveName, size, bootable);
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            return rigidDiskBlock;
        }

        public static async Task<RigidDiskBlock> WriteToFile(this RigidDiskBlock rigidDiskBlock, string path)
        {
            // create file
            using (var stream = File.Open(path, FileMode.Create))
            {
                // set length to preallocate
                stream.SetLength(rigidDiskBlock.DiskSize);

                await WriteToStream(rigidDiskBlock, stream);

                return rigidDiskBlock;
            }
        }

        public static async Task<RigidDiskBlock> WriteToStream(this RigidDiskBlock rigidDiskBlock, Stream stream)
        {
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return rigidDiskBlock;
        }
    }
}