namespace Hst.Amiga.FileSystems.Salvage
{
    /// <summary>
    /// Type of UAE metadata written, when salvaging to a directory.
    /// </summary>
    public enum UaeMetadata
    {
        /// <summary>
        /// UAEFSDB, one _UAEFSDB.___ file per directory (WinUAE, FS-UAE, Amiberry).
        /// </summary>
        UaeFsDb,

        /// <summary>
        /// UAE metafiles, one .uaem file per directory and file (FS-UAE).
        /// </summary>
        UaeMetafile
    }
}
