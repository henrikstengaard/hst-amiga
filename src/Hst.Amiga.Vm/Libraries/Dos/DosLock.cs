namespace Hst.Amiga.Vm.Libraries.Dos;

using System.Collections.Generic;
using FileSystem;

/// <summary>
/// Dos lock on file or directory mapped to host path.
/// </summary>
internal sealed class DosLock
{
    public DosLock(uint address, AmigaPath path, bool isDirectory, int access)
    {
        Address = address;
        Path = path;
        IsDirectory = isDirectory;
        Access = access;
    }

    /// <summary>
    /// Address of FileLock structure.
    /// </summary>
    public uint Address { get; }

    public AmigaPath Path { get; }
    public bool IsDirectory { get; }
    public int Access { get; }

    /// <summary>
    /// Directory entries examined by ExNext().
    /// </summary>
    public IList<string> Entries { get; set; }
}
