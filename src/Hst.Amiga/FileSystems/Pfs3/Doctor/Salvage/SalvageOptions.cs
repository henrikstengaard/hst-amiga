namespace Hst.Amiga.FileSystems.Pfs3.Doctor.Salvage
{
    /// <summary>
    /// Options for salvage.
    /// </summary>
    public class SalvageOptions
    {
        /// <summary>
        /// Default name of directory orphaned directories are salvaged to.
        /// </summary>
        public const string DefaultLostDirectoryName = "Lost+Found";

        /// <summary>
        /// Salvage orphaned directories, which are directory blocks found in reserved area that can't be reached
        /// from the root directory. Orphaned directories can be old or deleted directories.
        /// </summary>
        public bool SalvageOrphans { get; set; } = true;

        /// <summary>
        /// Name of directory in root orphaned directories are salvaged to.
        /// </summary>
        public string LostDirectoryName { get; set; } = DefaultLostDirectoryName;
    }
}
