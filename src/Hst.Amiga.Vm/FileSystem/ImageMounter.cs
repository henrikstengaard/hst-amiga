namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using RigidDiskBlocks;

/// <summary>
/// Mounts volumes from adf and disk image files with rigid disk block.
/// </summary>
public static class ImageMounter
{
    /// <summary>
    /// Boot priority of floppy disk drive DF0:.
    /// </summary>
    public const int FloppyBootPriority = 5;

    /// <summary>
    /// Is stream an adf image with size of a double or high density floppy disk.
    /// </summary>
    public static bool IsAdf(Stream stream) =>
        stream.Length == FloppyDiskConstants.DoubleDensity.Size ||
        stream.Length == FloppyDiskConstants.HighDensity.Size;

    /// <summary>
    /// Number of sectors from start of disk image searched for rigid disk block.
    /// </summary>
    public const int RigidDiskBlockLocationLimit = 16;

    /// <summary>
    /// Is stream an adf image or a disk image with rigid disk block in one of the first 16 sectors.
    /// </summary>
    public static async Task<bool> IsImage(Stream stream) => IsAdf(stream) || await HasRigidDiskBlock(stream);

    /// <summary>
    /// Has stream a rigid disk block identifier in one of the first 16 sectors. Only the identifier is checked, so
    /// a rigid disk block with invalid checksum is detected too.
    /// </summary>
    public static async Task<bool> HasRigidDiskBlock(Stream stream)
    {
        const int sectorSize = 512;
        var position = stream.Position;
        try
        {
            var sector = new byte[sectorSize];
            for (var i = 0; i < RigidDiskBlockLocationLimit && (long)(i + 1) * sectorSize <= stream.Length; i++)
            {
                stream.Position = (long)i * sectorSize;
                var read = 0;
                while (read < 4)
                {
                    var bytesRead = await stream.ReadAsync(sector, read, sectorSize - read);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    read += bytesRead;
                }

                if (read >= 4 && BitConverter.ToUInt32(sector, 0) == BlockIdentifiers.RigidDiskBlock)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>
    /// Find boot partition in rigid disk block, which is the bootable partition with highest boot priority or
    /// otherwise the first partition. Partitions with no mount flag set or unsupported file systems are skipped.
    /// </summary>
    /// <returns>Partition number starting from 1 and partition block.</returns>
    /// <exception cref="IOException">No partition with supported file system found.</exception>
    public static (int Number, PartitionBlock PartitionBlock) FindBootPartition(
        IEnumerable<PartitionBlock> partitionBlocks)
    {
        var partitions = partitionBlocks
            .Select((partitionBlock, index) => (Number: index + 1, PartitionBlock: partitionBlock))
            .Where(x => !x.PartitionBlock.NoMount && FileSystemVolumeFactory.IsSupported(x.PartitionBlock))
            .ToList();
        if (partitions.Count == 0)
        {
            throw new IOException("No partition with PFS3, FFS or SFS file system found in rigid disk block");
        }

        return partitions.Where(x => x.PartitionBlock.Bootable)
            .OrderByDescending(x => x.PartitionBlock.BootPriority)
            .DefaultIfEmpty(partitions[0])
            .First();
    }

    /// <summary>
    /// Select boot volume, which is the bootable volume with highest boot priority or otherwise the first volume.
    /// </summary>
    /// <returns>Boot volume or null, if there are no volumes.</returns>
    public static ImageVolume SelectBootVolume(IEnumerable<ImageVolume> volumes)
    {
        var list = volumes.ToList();
        return list.Where(x => x.Bootable).OrderByDescending(x => x.BootPriority).FirstOrDefault() ??
               list.FirstOrDefault();
    }

    /// <summary>
    /// Mount volumes in adf or disk image with rigid disk block.
    /// </summary>
    /// <param name="stream">Stream with adf or disk image.</param>
    /// <param name="writable">Volumes are writable, otherwise they are write protected.</param>
    /// <param name="floppyDeviceName">Device name of adf, e.g. DF0.</param>
    /// <param name="log">Action receiving warnings for partitions, which can't be mounted.</param>
    /// <param name="partitionFilter">Filter partitions to mount by partition number starting from 1 and partition
    /// block. Default mounts partitions, which don't have the no mount flag set.</param>
    /// <returns>Mounted volumes, which must be disposed.</returns>
    /// <exception cref="IOException">Image doesn't contain an adf or rigid disk block.</exception>
    public static async Task<IList<ImageVolume>> Mount(Stream stream, bool writable, string floppyDeviceName = "DF0",
        Action<string> log = null, Func<int, PartitionBlock, bool> partitionFilter = null)
    {
        stream.Position = 0;
        if (IsAdf(stream))
        {
            var adfVolume = await FastFileSystemVolume.MountAdf(stream);
            return new List<ImageVolume>
            {
                new(floppyDeviceName, 0, true, FloppyBootPriority,
                    new AmigaVolumeFileSystem(adfVolume, !writable, log))
            };
        }

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        if (rigidDiskBlock == null)
        {
            throw new IOException("Image is not an adf and doesn't contain a rigid disk block");
        }

        var volumes = new List<ImageVolume>();
        var partitionNumber = 0;
        foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks)
        {
            partitionNumber++;
            if (partitionFilter == null ? partitionBlock.NoMount : !partitionFilter(partitionNumber, partitionBlock))
            {
                continue;
            }

            if (!FileSystemVolumeFactory.IsSupported(partitionBlock))
            {
                log?.Invoke(
                    $"Warning: Partition {partitionNumber} '{partitionBlock.DriveName}' file system {partitionBlock.DosTypeFormatted} is not supported, only PFS3, FFS and SFS are supported");
                continue;
            }

            try
            {
                var volume = await FileSystemVolumeFactory.MountPartition(stream, partitionBlock);
                volumes.Add(new ImageVolume(partitionBlock.DriveName, partitionNumber, partitionBlock.Bootable,
                    partitionBlock.BootPriority, new AmigaVolumeFileSystem(volume, !writable, log)));
            }
            catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException
                                          or Hst.Amiga.FileSystems.Exceptions.FileSystemException)
            {
                log?.Invoke(
                    $"Warning: Unable to mount partition {partitionNumber} '{partitionBlock.DriveName}': {e.Message}");
            }
        }

        return volumes;
    }
}
