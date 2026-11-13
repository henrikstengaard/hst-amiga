namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    /// <summary>
    /// Issue found by doctor.
    /// </summary>
    public class Issue
    {
        /// <summary>
        /// Type of issue.
        /// </summary>
        public IssueType Type { get; set; }

        /// <summary>
        /// Block issue was found in.
        /// </summary>
        public uint Block { get; set; }

        /// <summary>
        /// Path of entry issue was found in, empty if issue is not related to an entry.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Description of issue.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Issue is a warning, which doesn't make the volume inconsistent, e.g. blocks marked used in bitmap,
        /// that are not used by any entry.
        /// </summary>
        public bool Warning { get; set; }

        /// <summary>
        /// Issue was fixed.
        /// </summary>
        public bool Fixed { get; set; }

        public override string ToString()
        {
            var prefix = Fixed ? "FIXED" : Warning ? "WARNING" : "ERROR";
            var path = string.IsNullOrEmpty(Path) ? string.Empty : $" '{Path}'";
            return $"{prefix}: {Message} (block {Block}{path})";
        }
    }
}
