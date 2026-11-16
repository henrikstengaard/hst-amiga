namespace Hst.Amiga.Vm.FileSystem;

/// <summary>
/// UAE metadata used to store Amiga names, protection bits, comments and dates for files in host directories.
/// </summary>
public enum UaeMetadataMode
{
    /// <summary>
    /// No UAE metadata. Host names are used as Amiga names, metadata files are shown as regular files and
    /// protection bits and comments are kept in memory.
    /// </summary>
    None,

    /// <summary>
    /// Detect UAE metadata per directory. Directories with a _UAEFSDB.___ file use UAEFSDB, directories with
    /// .uaem files use UAE metafiles. New metadata is written as UAEFSDB in directories without metadata.
    /// </summary>
    Auto,

    /// <summary>
    /// UAEFSDB with one _UAEFSDB.___ file per directory used by WinUAE.
    /// </summary>
    UaeFsDb,

    /// <summary>
    /// UAE metafiles with one .uaem file per file used by FS-UAE.
    /// </summary>
    UaeMetafile
}
