namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.IO;

/// <summary>
/// File system of a volume, e.g. a host directory or an Amiga file system in an adf or disk image file. Paths
/// are resolved Amiga paths within the volume.
/// </summary>
public interface IVolumeFileSystem
{
    /// <summary>
    /// Host directory of volume root or null, if volume isn't a host directory.
    /// </summary>
    string HostRoot { get; }

    /// <summary>
    /// Volume is write protected.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Is name a valid name for a file or directory.
    /// </summary>
    bool IsValidName(string amigaName);

    /// <summary>
    /// Get root path of volume.
    /// </summary>
    AmigaPath GetRoot(string volume);

    /// <summary>
    /// Get entry in directory by Amiga name ignoring case. Path for a new entry is returned, if entry doesn't exist.
    /// </summary>
    AmigaPath GetChild(AmigaPath directory, string amigaName);

    /// <summary>
    /// List Amiga names of entries in directory.
    /// </summary>
    string[] ListNames(AmigaPath directory);

    /// <summary>
    /// Get information about file or directory.
    /// </summary>
    /// <returns>Information or null, if path doesn't exist.</returns>
    VolumeEntryInfo GetInfo(AmigaPath path);

    /// <summary>
    /// Open file.
    /// </summary>
    /// <exception cref="IOException">File can't be opened.</exception>
    Stream OpenFile(AmigaPath path, VolumeFileMode mode);

    /// <summary>
    /// Create directory.
    /// </summary>
    void CreateDirectory(AmigaPath path);

    /// <summary>
    /// Delete file or empty directory.
    /// </summary>
    void Delete(AmigaPath path);

    /// <summary>
    /// Rename or move file or directory within volume.
    /// </summary>
    void Rename(AmigaPath oldPath, AmigaPath newPath);

    /// <summary>
    /// Set fib protection bits of file or directory.
    /// </summary>
    void SetProtection(AmigaPath path, uint protection);

    /// <summary>
    /// Set comment of file or directory.
    /// </summary>
    void SetComment(AmigaPath path, string comment);

    /// <summary>
    /// Set date of file or directory.
    /// </summary>
    void SetDate(AmigaPath path, DateTime date);

    /// <summary>
    /// Get size and free space of volume in bytes.
    /// </summary>
    (long Size, long Free) GetSpace(AmigaPath path);
}
