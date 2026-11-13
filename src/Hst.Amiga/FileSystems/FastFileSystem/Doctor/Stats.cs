namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    /// <summary>
    /// Statistics from doctor run.
    /// </summary>
    public class Stats
    {
        /// <summary>
        /// Number of directories found, excluding root directory.
        /// </summary>
        public int Directories { get; set; }

        /// <summary>
        /// Number of files found.
        /// </summary>
        public int Files { get; set; }

        /// <summary>
        /// Number of hard links found.
        /// </summary>
        public int HardLinks { get; set; }

        /// <summary>
        /// Number of soft links found.
        /// </summary>
        public int SoftLinks { get; set; }

        /// <summary>
        /// Number of blocks used.
        /// </summary>
        public uint UsedBlocks { get; set; }

        /// <summary>
        /// Number of blocks free.
        /// </summary>
        public uint FreeBlocks { get; set; }

        /// <summary>
        /// Number of errors found.
        /// </summary>
        public int Errors { get; set; }

        /// <summary>
        /// Number of warnings found.
        /// </summary>
        public int Warnings { get; set; }

        /// <summary>
        /// Number of errors and warnings fixed.
        /// </summary>
        public int Fixed { get; set; }
    }
}
