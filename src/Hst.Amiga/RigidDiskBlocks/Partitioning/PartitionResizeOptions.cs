namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    /// <summary>
    /// Options for resizing a partition and the file system within it. New cylinders must be within the cylinders
    /// available before and after the partition.
    /// </summary>
    public class PartitionResizeOptions
    {
        /// <summary>
        /// Optional new low cylinder of partition to expand or shrink partition at start. If not set, low cylinder is
        /// unchanged.
        /// </summary>
        public uint? NewLowCyl { get; set; }

        /// <summary>
        /// New high cylinder of partition.
        /// </summary>
        public uint NewHighCyl { get; set; }

        /// <summary>
        /// Optional new number of cylinders for rigid disk block to expand or shrink rigid disk block within the size
        /// of the disk. Must be large enough for all partitions after the resize.
        /// </summary>
        public uint? RigidDiskBlockCylinders { get; set; }

        /// <summary>
        /// Validate and plan the resize without writing any changes.
        /// </summary>
        public bool DryRun { get; set; }
    }
}
