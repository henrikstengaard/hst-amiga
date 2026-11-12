namespace Hst.Amiga.Extensions
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DataTypes.Hunks;
    using RigidDiskBlocks;
    using VersionStrings;

    public static class RigidDiskBlockExtensions
    {
        public static RigidDiskBlock CreateRigidDiskBlock(this long size) => RigidDiskBlock.Create(size);

        public static RigidDiskBlock AddFileSystem(this RigidDiskBlock rigidDiskBlock,
            string dosType, byte[] fileSystemBytes, bool overwrite = false)
        {
            return AddFileSystem(rigidDiskBlock, DosTypeHelper.FormatDosType(dosType), string.Empty, fileSystemBytes,
                overwrite);
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

            var fileVersion = ReadFileSystemVersion(fileSystemBytes);

            var fileSystemHeaderBlock = BlockHelper.CreateFileSystemHeaderBlock(dosType, fileVersion.Version,
                fileVersion.Revision, fileSystemName, fileSystemBytes);

            rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks
                .Where(x => !x.DosType.SequenceEqual(dosType)).Concat(new[]
                    { fileSystemHeaderBlock });

            return rigidDiskBlock;
        }

        /// <summary>
        /// Read file system version from resident structure (romtag) and "$VER:" version string:
        /// 1. Id string or "$VER:" version string with version matching resident structure version (rt_Version),
        ///    id string preferred.
        /// 2. Id string, then "$VER:" version string.
        /// 3. Resident structure version (rt_Version) and revision 0, if resident structure version is set.
        /// 4. Version 1 and revision 0.
        /// </summary>
        private static AmigaVersion ReadFileSystemVersion(byte[] fileSystemBytes)
        {
            var romTag = RomTagReader.Read(new MemoryStream(fileSystemBytes, false)).GetAwaiter().GetResult();
            var idStringVersion = VersionStringReader.Parse(romTag?.IdString);
            var versionStringVersion = VersionStringReader.Parse(VersionStringReader.ReadVersionString(fileSystemBytes));

            // resident structure version 0 is treated as not set
            var romTagVersion = romTag?.Version ?? 0;

            if (romTagVersion > 0)
            {
                if (idStringVersion != null && idStringVersion.Version == romTagVersion)
                {
                    return idStringVersion;
                }

                if (versionStringVersion != null && versionStringVersion.Version == romTagVersion)
                {
                    return versionStringVersion;
                }
            }

            return idStringVersion ?? versionStringVersion ?? new AmigaVersion
            {
                Version = romTagVersion > 0 ? romTagVersion : 1,
                Revision = 0
            };
        }

        public static RigidDiskBlock AddPartition(this RigidDiskBlock rigidDiskBlock,
            string driveName, long size = 0, int fileSystemBlockSize = 512, bool bootable = false)
        {
            var firstFileSystemHeaderBlock = rigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault();

            if (firstFileSystemHeaderBlock == null)
            {
                throw new Exception("No file system header blocks");
            }

            return AddPartition(rigidDiskBlock, firstFileSystemHeaderBlock.DosType, driveName, size,
                fileSystemBlockSize, bootable);
        }

        public static RigidDiskBlock AddPartition(this RigidDiskBlock rigidDiskBlock, byte[] dosType, string driveName,
            long size = 0,
            int fileSystemBlockSize = 512,
            bool bootable = false)
        {
            var partitionBlock =
                PartitionBlock.Create(rigidDiskBlock, dosType, driveName, size, fileSystemBlockSize, bootable);
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            return rigidDiskBlock;
        }

        public static async Task<RigidDiskBlock> WriteToFile(this RigidDiskBlock rigidDiskBlock, string path)
        {
            // create file
            using (var stream = File.Open(path, FileMode.Create))
            {
                await WriteToStream(rigidDiskBlock, stream);

                return rigidDiskBlock;
            }
        }

        public static async Task<RigidDiskBlock> WriteToStream(this RigidDiskBlock rigidDiskBlock, Stream stream)
        {
            // set length to preallocate
            stream.SetLength(rigidDiskBlock.DiskSize);

            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return rigidDiskBlock;
        }
    }
}