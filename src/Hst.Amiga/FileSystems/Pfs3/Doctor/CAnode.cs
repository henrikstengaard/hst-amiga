namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Cached anode (canode_t in doctor.h).
    /// </summary>
    public class CAnode
    {
        /// <summary>
        /// number of blocks in cluster
        /// </summary>
        public uint ClusterSize { get; set; }

        /// <summary>
        /// the block number
        /// </summary>
        public uint BlockNr { get; set; }

        /// <summary>
        /// next anode (anode number), 0 = eof
        /// </summary>
        public uint Next { get; set; }

        /// <summary>
        /// the anode number
        /// </summary>
        public uint Nr { get; set; }
    }
}
