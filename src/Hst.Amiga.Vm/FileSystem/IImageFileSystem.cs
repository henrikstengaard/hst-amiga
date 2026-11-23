namespace Hst.Amiga.Vm.FileSystem;

using System;

/// <summary>
/// File system of a volume mounted from an image, e.g. an Amiga file system in an adf or partition or an
/// ISO 9660 file system in a CD image.
/// </summary>
public interface IImageFileSystem : IVolumeFileSystem, IDisposable
{
    /// <summary>
    /// Name of volume, e.g. Workbench.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Flush changes to volume.
    /// </summary>
    void Flush();
}
