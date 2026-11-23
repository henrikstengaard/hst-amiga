namespace Hst.Amiga.Vm.FileSystem;

using System;

/// <summary>
/// Volume mounted from adf, partition in disk image or CD image.
/// </summary>
public class ImageVolume : IDisposable
{
    public ImageVolume(string deviceName, int partitionNumber, bool bootable, int bootPriority,
        IImageFileSystem fileSystem, ImageVolumeType type)
    {
        DeviceName = deviceName;
        PartitionNumber = partitionNumber;
        Bootable = bootable;
        BootPriority = bootPriority;
        FileSystem = fileSystem;
        Type = type;
    }

    /// <summary>
    /// Device name without colon, e.g. DF0 for adf, DH0 for partition or CD0 for CD image.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Partition number starting from 1 in rigid disk block or 0 for adf and CD image.
    /// </summary>
    public int PartitionNumber { get; }

    /// <summary>
    /// Volume is bootable, adf, CD image or partition with bootable flag.
    /// </summary>
    public bool Bootable { get; }

    /// <summary>
    /// Boot priority of partition.
    /// </summary>
    public int BootPriority { get; }

    /// <summary>
    /// File system of mounted volume.
    /// </summary>
    public IImageFileSystem FileSystem { get; }

    /// <summary>
    /// Type of volume, floppy, partition or CD.
    /// </summary>
    public ImageVolumeType Type { get; }

    /// <summary>
    /// Name of volume, e.g. Workbench.
    /// </summary>
    public string VolumeName => FileSystem.Name;

    public void Dispose() => FileSystem.Dispose();
}
