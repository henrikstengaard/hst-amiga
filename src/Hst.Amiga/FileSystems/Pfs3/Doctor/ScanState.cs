namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Standard scan state (ss struct in standardscan.c).
    /// </summary>
    public class ScanState
    {
        /// <summary>
        /// internal flags
        /// </summary>
        public StandardScanFlags Flags { get; set; }

        /// <summary>
        /// options
        /// </summary>
        public StandardScanFlags Opties { get; set; }

        public int Pass { get; set; }
        public bool Verbose { get; set; }
        public bool Unformat { get; set; }
        public bool Info { get; set; }
        public Stage Stage { get; set; }
    }
}
