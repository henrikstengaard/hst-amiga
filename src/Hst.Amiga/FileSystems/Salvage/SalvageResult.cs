namespace Hst.Amiga.FileSystems.Salvage
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of salvage.
    /// </summary>
    public class SalvageResult
    {
        /// <summary>
        /// Valid root block was found.
        /// </summary>
        public bool RootBlockFound { get; set; }

        /// <summary>
        /// Number of directories salvaged.
        /// </summary>
        public int Directories { get; set; }

        /// <summary>
        /// Number of files salvaged, including files only partially salvaged.
        /// </summary>
        public int Files { get; set; }

        /// <summary>
        /// Number of files only partially salvaged, because their data couldn't be fully read.
        /// </summary>
        public int PartialFiles { get; set; }

        /// <summary>
        /// Number of bytes of file data salvaged.
        /// </summary>
        public long Bytes { get; set; }

        /// <summary>
        /// Number of orphaned directories salvaged to lost directory.
        /// </summary>
        public int OrphanedDirectories { get; set; }

        /// <summary>
        /// Number of entries skipped, e.g. soft links and hard links to directories.
        /// </summary>
        public int Skipped { get; set; }

        /// <summary>
        /// Number of entries that failed to be created in target.
        /// </summary>
        public int Errors { get; set; }

        /// <summary>
        /// Messages about issues found during salvage.
        /// </summary>
        public IList<string> Messages { get; } = new List<string>();
    }
}
