namespace Hst.Amiga.Vm.FileSystem;

/// <summary>
/// Mode for opening file in volume.
/// </summary>
public enum VolumeFileMode
{
    /// <summary>
    /// Open existing file for reading and writing, if volume and file is writable.
    /// </summary>
    OldFile,

    /// <summary>
    /// Create new empty file for reading and writing, replacing existing file.
    /// </summary>
    NewFile,

    /// <summary>
    /// Open existing file for reading and writing or create new file, if it doesn't exist.
    /// </summary>
    ReadWrite
}
