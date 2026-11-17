namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.Linq;

    /// <summary>
    /// Helpers for partition locations and cylinders available within a rigid disk block.
    /// </summary>
    public static class PartitionGeometry
    {
        /// <summary>
        /// Size of a cylinder in bytes for rigid disk block.
        /// </summary>
        public static long GetCylinderSize(RigidDiskBlock rigidDiskBlock) =>
            (long)rigidDiskBlock.CylBlocks * rigidDiskBlock.BlockSize;

        /// <summary>
        /// Size of a cylinder in bytes for partition.
        /// </summary>
        public static long GetCylinderSize(PartitionBlock partitionBlock) =>
            (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * partitionBlock.BlockSize;

        /// <summary>
        /// Byte offset of first byte of partition.
        /// </summary>
        public static long GetStartOffset(PartitionBlock partitionBlock) =>
            partitionBlock.LowCyl * GetCylinderSize(partitionBlock);

        /// <summary>
        /// Byte offset of first byte after partition.
        /// </summary>
        public static long GetEndOffset(PartitionBlock partitionBlock) =>
            (partitionBlock.HighCyl + 1L) * GetCylinderSize(partitionBlock);

        /// <summary>
        /// Size of partition in bytes.
        /// </summary>
        public static long GetSize(PartitionBlock partitionBlock) =>
            (partitionBlock.HighCyl - partitionBlock.LowCyl + 1L) * GetCylinderSize(partitionBlock);

        /// <summary>
        /// Set low and high cylinder of partition and update partition size.
        /// </summary>
        public static void SetCylinders(PartitionBlock partitionBlock, uint lowCyl, uint highCyl)
        {
            partitionBlock.LowCyl = lowCyl;
            partitionBlock.HighCyl = highCyl;
            partitionBlock.PartitionSize = GetSize(partitionBlock);
        }

        /// <summary>
        /// Range of partitionable cylinders in rigid disk block expressed in cylinders of partition.
        /// </summary>
        public static CylinderRange GetRigidDiskBlockCylinders(RigidDiskBlock rigidDiskBlock,
            PartitionBlock partitionBlock)
        {
            var rdbCylinderSize = GetCylinderSize(rigidDiskBlock);
            var startOffset = rigidDiskBlock.LoCylinder * rdbCylinderSize;
            var endOffset = (rigidDiskBlock.HiCylinder + 1L) * rdbCylinderSize;

            return ToCylinderRange(startOffset, endOffset, GetCylinderSize(partitionBlock));
        }

        /// <summary>
        /// Range of cylinders available for partition: cylinders used by partition and free cylinders before and
        /// after partition until previous and next partition or start and end of partitionable area of rigid disk
        /// block. Cylinders are expressed in cylinders of partition.
        /// </summary>
        public static CylinderRange GetAvailableCylinders(RigidDiskBlock rigidDiskBlock,
            PartitionBlock partitionBlock)
        {
            var rdbCylinderSize = GetCylinderSize(rigidDiskBlock);
            var start = GetStartOffset(partitionBlock);

            var others = rigidDiskBlock.PartitionBlocks.Where(x => !ReferenceEquals(x, partitionBlock)).ToList();

            var availableStart = rigidDiskBlock.LoCylinder * rdbCylinderSize;
            foreach (var other in others.Where(x => GetStartOffset(x) < start))
            {
                availableStart = Math.Max(availableStart, GetEndOffset(other));
            }

            var availableEnd = (rigidDiskBlock.HiCylinder + 1L) * rdbCylinderSize;
            foreach (var other in others.Where(x => GetStartOffset(x) >= start))
            {
                availableEnd = Math.Min(availableEnd, GetStartOffset(other));
            }

            return ToCylinderRange(availableStart, availableEnd, GetCylinderSize(partitionBlock));
        }

        /// <summary>
        /// Partition other than given partition overlapping cylinders, null if no partition overlaps.
        /// </summary>
        public static PartitionBlock FindOverlappingPartition(RigidDiskBlock rigidDiskBlock,
            PartitionBlock partitionBlock, uint lowCyl, uint highCyl)
        {
            var cylinderSize = GetCylinderSize(partitionBlock);
            var start = lowCyl * cylinderSize;
            var end = (highCyl + 1L) * cylinderSize;

            return rigidDiskBlock.PartitionBlocks.FirstOrDefault(x =>
                !ReferenceEquals(x, partitionBlock) && GetStartOffset(x) < end && GetEndOffset(x) > start);
        }

        private static CylinderRange ToCylinderRange(long startOffset, long endOffset, long cylinderSize)
        {
            var lowCyl = (startOffset + cylinderSize - 1) / cylinderSize;
            var highCyl = endOffset / cylinderSize - 1;

            return new CylinderRange((uint)Math.Max(0, lowCyl), (uint)Math.Max(0, highCyl));
        }
    }
}
