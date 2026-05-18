namespace Hst.Amiga.FileSystems.Pfs3
{
    /// <summary>
    /// Cached allocation node.
    /// </summary>
    public class canode
    {
        /// <summary>
        /// Number of blocks in a cluster.
        /// </summary>
        public uint clustersize;	// number of blocks in a cluster
        
        /// <summary>
        /// Block number
        /// </summary>
        public uint blocknr;		// the block number
        
        /// <summary>
        /// Next anode number. 0 means end of file (eof).
        /// </summary>
        public uint next;			// next anode (anodenummer), 0 = eof

        /// <summary>
        /// Anode number (unique per dir entry?)
        /// </summary>
        public uint nr;			// the anodenr
    }
}