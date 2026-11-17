namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// File system specific part of moving and resizing a partition.
    /// </summary>
    internal interface IPartitionFileSystem
    {
        /// <summary>
        /// Name of file system used in messages.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// File system supports dos type.
        /// </summary>
        bool IsSupported(byte[] dosType);

        /// <summary>
        /// Check file system in partition. Returns errors found, empty if file system has no errors.
        /// </summary>
        Task<IList<string>> Check(Stream stream, PartitionBlock partitionBlock, CancellationToken cancellationToken);

        /// <summary>
        /// Update file system after partition data is moved to the location in partition block, e.g. absolute
        /// positions stored in file system blocks.
        /// </summary>
        Task AfterMove(Stream stream, PartitionBlock partitionBlock, CancellationToken cancellationToken);

        /// <summary>
        /// Resize file system in partition to new number of cylinders. Partition block has current location and size
        /// of file system, partition keeps low cylinder. Blocks after the current end of partition up to the new size
        /// are available for file system, when expanding. Dry run plans the resize and throws
        /// PartitionOperationException, if resize is not possible, but doesn't write any changes.
        /// </summary>
        Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders, bool dryRun,
            IList<string> messages, CancellationToken cancellationToken);
    }
}
