namespace Hst.Amiga.FileSystems.SmartFileSystem.Mover
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigidDiskBlocks.Partitioning;
    using Resizer;

    /// <summary>
    /// Move partition with Smart File System to a new location within the rigid disk block. The new location can
    /// overlap the current location or be free cylinders before or after the partition. First and last byte of
    /// partition stored in root blocks are updated.
    /// </summary>
    public static class SmartFileSystemMover
    {
        /// <summary>
        /// Move partition to new low cylinder. File system is checked before and after the move.
        /// </summary>
        /// <param name="stream">Stream with disk containing rigid disk block.</param>
        /// <param name="partitionNumber">Partition number starting from 1.</param>
        /// <param name="options">Move options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task<PartitionOperationResult> Move(Stream stream, int partitionNumber,
            PartitionMoveOptions options, CancellationToken cancellationToken = default) =>
            PartitionOperation.Move(stream, SmartFileSystemPartition.Instance, partitionNumber, options,
                cancellationToken);
    }
}
