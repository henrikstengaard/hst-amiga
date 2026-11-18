namespace Hst.Amiga.FileSystems
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using FastFileSystem;
    using Pfs3;
    using RigidDiskBlocks;
    using SmartFileSystem;

    /// <summary>
    /// Mounts file system volumes of partitions formatted with PFS3, FFS or SFS.
    /// </summary>
    public static class FileSystemVolumeFactory
    {
        /// <summary>
        /// Is file system of partition supported, PFS3, FFS or SFS.
        /// </summary>
        /// <param name="partitionBlock">Partition block of partition.</param>
        /// <returns>True, if file system of partition can be mounted.</returns>
        public static bool IsSupported(PartitionBlock partitionBlock)
        {
            var dosType = partitionBlock?.DosType ?? Array.Empty<byte>();
            if (dosType.Length != 4)
            {
                return false;
            }

            var identifier = AmigaTextHelper.GetString(dosType, 0, 3);
            return ((identifier == "PFS" || identifier == "PDS") && dosType[3] == 3) ||
                   (identifier == "DOS" && dosType[3] <= 7) ||
                   (identifier == "SFS" && dosType[3] == 0);
        }

        /// <summary>
        /// Mount partition from stream. Partition must be formatted with PFS3, FFS or SFS.
        /// </summary>
        /// <param name="stream">Stream with rigid disk block, e.g. a rigid disk block image file.</param>
        /// <param name="partitionBlock">Partition block of partition.</param>
        /// <param name="force">Mount partition, even if partition is already mounted by another volume on the same stream.</param>
        /// <returns>Mounted file system volume, which must be disposed.</returns>
        /// <exception cref="NotSupportedException">File system of partition is not supported.</exception>
        /// <exception cref="Exceptions.PartitionAlreadyMountedException">Partition is already mounted on the same stream.</exception>
        public static async Task<IFileSystemVolume> MountPartition(Stream stream, PartitionBlock partitionBlock,
            bool force = false)
        {
            var dosType = partitionBlock.DosType ?? Array.Empty<byte>();
            if (dosType.Length != 4)
            {
                throw new NotSupportedException($"Partition '{partitionBlock.DriveName}' has invalid dos type");
            }

            var identifier = AmigaTextHelper.GetString(dosType, 0, 3);
            if ((identifier == "PFS" || identifier == "PDS") && dosType[3] == 3)
            {
                return await Pfs3Volume.Mount(stream, partitionBlock, force: force);
            }

            if (identifier == "DOS" && dosType[3] <= 7)
            {
                return await FastFileSystemVolume.MountPartition(stream, partitionBlock, force: force);
            }

            if (identifier == "SFS" && dosType[3] == 0)
            {
                return await SmartFileSystemVolume.Mount(stream, partitionBlock, force);
            }

            throw new NotSupportedException(
                $"Partition '{partitionBlock.DriveName}' file system {partitionBlock.DosTypeFormatted} is not supported, only PFS3, FFS and SFS are supported");
        }
    }
}
