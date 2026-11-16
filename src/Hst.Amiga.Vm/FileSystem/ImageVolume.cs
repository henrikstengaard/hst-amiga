namespace Hst.Amiga.Vm.FileSystem;

using System;

/// <summary>
/// Volume mounted from adf or partition in disk image.
/// </summary>
public class ImageVolume : IDisposable
{
    public ImageVolume(string deviceName, int partitionNumber, bool bootable, int bootPriority,
        AmigaVolumeFileSystem fileSystem)
    {
        DeviceName = deviceName;
        PartitionNumber = partitionNumber;
        Bootable = bootable;
        BootPriority = bootPriority;
        FileSystem = fileSystem;
    }

    /// <summary>
    /// Device name without colon, e.g. DF0 for adf or DH0 for partition.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Partition number starting from 1 in rigid disk block or 0 for adf.
    /// </summary>
    public int PartitionNumber { get; }

    /// <summary>
    /// Volume is bootable, adf or partition with bootable flag.
    /// </summary>
    public bool Bootable { get; }

    /// <summary>
    /// Boot priority of partition.
    /// </summary>
    public int BootPriority { get; }

    /// <summary>
    /// File system of mounted volume.
    /// </summary>
    public AmigaVolumeFileSystem FileSystem { get; }

    /// <summary>
    /// Name of Amiga file system volume, e.g. Workbench.
    /// </summary>
    public string VolumeName => FileSystem.Name;

    public void Dispose() => FileSystem.Dispose();
}
