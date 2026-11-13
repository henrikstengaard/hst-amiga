namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of restoring displaced boot and root blocks to volume start.
    /// </summary>
    public class RootBlockRestoreResult
    {
        /// <summary>
        /// Valid root block is located at volume start, no restore needed.
        /// </summary>
        public bool RootBlockValid { get; set; }

        /// <summary>
        /// Displaced boot and root blocks were found.
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// Displaced boot and root blocks were restored to volume start.
        /// </summary>
        public bool Restored { get; set; }

        /// <summary>
        /// Sector displaced boot block was found at.
        /// </summary>
        public long SourceSector { get; set; } = -1;

        /// <summary>
        /// Number of sectors boot and root blocks are displaced from volume start.
        /// </summary>
        public long DisplacedSectors { get; set; }

        /// <summary>
        /// Number of sectors copied from displaced boot and root blocks to volume start.
        /// </summary>
        public int SectorsCopied { get; set; }

        public IList<string> Messages { get; } = new List<string>();
    }
}
