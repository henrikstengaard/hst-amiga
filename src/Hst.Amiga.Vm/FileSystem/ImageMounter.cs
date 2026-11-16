namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
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
