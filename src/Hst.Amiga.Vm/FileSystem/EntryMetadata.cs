namespace Hst.Amiga.Vm.FileSystem;

using System;

/// <summary>
/// Amiga metadata of a file or directory in a host directory.
/// </summary>
public class EntryMetadata
{
    /// <summary>
    /// Amiga protection bits as stored in file info block, where read, write, execute and delete bits are set
    /// when the operation is not allowed.
    /// </summary>
    public uint Protection { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Date stored in metadata or null to use date of host file.
    /// </summary>
    public DateTime? Date { get; set; }

    public EntryMetadata Clone() => new() { Protection = Protection, Comment = Comment, Date = Date };
}
