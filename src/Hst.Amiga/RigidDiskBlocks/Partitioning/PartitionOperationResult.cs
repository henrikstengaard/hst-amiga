namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of moving or resizing a partition.
    /// </summary>
    public class PartitionOperationResult
    {
        /// <summary>
        /// Operation completed (or would complete for dry run) without errors.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Errors, which prevented the operation or were found by checking the file system after the operation.
        /// </summary>
        public IList<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Messages describing the steps done.
        /// </summary>
        public IList<string> Messages { get; set; } = new List<string>();

        /// <summary>
        /// Rigid disk block after the operation.
        /// </summary>
        public RigidDiskBlock RigidDiskBlock { get; set; }

        /// <summary>
        /// Partition block after the operation.
        /// </summary>
        public PartitionBlock PartitionBlock { get; set; }
    }
}
