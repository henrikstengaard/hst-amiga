namespace Hst.Amiga.Vm.FileSystem;

using System;

/// <summary>
/// Information about file or directory in volume.
/// </summary>
public class VolumeEntryInfo
{
    public bool IsDirectory { get; set; }

    /// <summary>
    /// Size of file in bytes.
    /// </summary>
    public long Size { get; set; }

    public DateTime Date { get; set; }

    /// <summary>
    /// Fib protection bits, where bits for read, write, execute and delete are set, when not allowed.
    /// </summary>
    public uint Protection { get; set; }

    public string Comment { get; set; } = string.Empty;
}
