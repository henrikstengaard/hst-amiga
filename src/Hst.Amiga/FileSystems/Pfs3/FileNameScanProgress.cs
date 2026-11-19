namespace Hst.Amiga.FileSystems.Pfs3
{
    /// <summary>
    /// Progress of scanning entry names on a volume, reported once per directory scanned.
    /// Total number of entries is not known in advance.
    /// </summary>
    public class FileNameScanProgress
    {
        /// <summary>
        /// Number of directories scanned so far, including the root directory.
        /// </summary>
        public int DirectoriesScanned { get; }

        /// <summary>
        /// Number of entries checked so far.
        /// </summary>
        public int EntriesScanned { get; }

        /// <summary>
        /// Path of the directory most recently scanned, "/" for the root directory.
        /// </summary>
        public string CurrentPath { get; }

        public FileNameScanProgress(int directoriesScanned, int entriesScanned, string currentPath)
        {
            DirectoriesScanned = directoriesScanned;
            EntriesScanned = entriesScanned;
            CurrentPath = currentPath;
        }
    }
}
