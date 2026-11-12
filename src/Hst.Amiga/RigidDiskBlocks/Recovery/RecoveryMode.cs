namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    public enum RecoveryMode
    {
        /// <summary>
        /// Fix rigid disk block, if rigid disk block is found. Otherwise rebuild rigid disk block.
        /// </summary>
        Auto,

        /// <summary>
        /// Fix existing rigid disk block by repairing checksums, block pointers and partitions using file systems
        /// found on disk.
        /// </summary>
        Fix,

        /// <summary>
        /// Rebuild new rigid disk block from disk size and file systems found on disk.
        /// </summary>
        Rebuild
    }
}
