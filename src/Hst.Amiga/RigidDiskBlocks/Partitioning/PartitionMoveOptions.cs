namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    /// <summary>
    /// Options for moving a partition to a new location within the rigid disk block.
    /// </summary>
    public class PartitionMoveOptions
    {
        /// <summary>
        /// New low cylinder of partition. Partition keeps its size, so new high cylinder is new low cylinder plus
        /// number of cylinders minus one. New location can overlap the current location.
        /// </summary>
        public uint NewLowCyl { get; set; }

        /// <summary>
        /// Optional new number of cylinders for rigid disk block to expand or shrink rigid disk block within the size
        /// of the disk. Must be large enough for all partitions after the move.
        /// </summary>
        public uint? RigidDiskBlockCylinders { get; set; }

        /// <summary>
        /// Validate and plan the move without writing any changes.
        /// </summary>
        public bool DryRun { get; set; }
    }
}
