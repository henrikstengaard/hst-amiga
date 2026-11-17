namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Expand or shrink rigid disk block within the size of the disk. Rigid disk block can be expanded to the
    /// number of cylinders the disk holds and shrunk to the last cylinder used by partitions.
    /// </summary>
    public static class RigidDiskBlockResizer
    {
        /// <summary>
        /// Maximum number of cylinders for rigid disk block on disk with given size.
        /// </summary>
        public static uint GetMaxCylinders(RigidDiskBlock rigidDiskBlock, long diskSize) =>
            (uint)Math.Min(diskSize / PartitionGeometry.GetCylinderSize(rigidDiskBlock), uint.MaxValue);

        /// <summary>
        /// Minimum number of cylinders for rigid disk block to contain rigid disk block area and all partitions.
        /// </summary>
        public static uint GetMinCylinders(RigidDiskBlock rigidDiskBlock)
        {
            var cylinderSize = PartitionGeometry.GetCylinderSize(rigidDiskBlock);
            var endOffset = rigidDiskBlock.PartitionBlocks.Select(PartitionGeometry.GetEndOffset)
                .DefaultIfEmpty(0).Max();

            return (uint)Math.Max(rigidDiskBlock.LoCylinder + 1L, (endOffset + cylinderSize - 1) / cylinderSize);
        }

        /// <summary>
        /// Validate new number of cylinders for rigid disk block with current partitions. Returns list of errors,
        /// empty if valid.
        /// </summary>
        public static IList<string> Validate(RigidDiskBlock rigidDiskBlock, long diskSize, uint cylinders)
        {
            if (rigidDiskBlock == null) throw new ArgumentNullException(nameof(rigidDiskBlock));

            var errors = new List<string>();

            var maxCylinders = GetMaxCylinders(rigidDiskBlock, diskSize);
            if (cylinders > maxCylinders)
            {
                errors.Add(
                    $"Rigid disk block cylinders {cylinders} is larger than disk size {diskSize} bytes, which holds {maxCylinders} cylinders");
            }

            var minCylinders = GetMinCylinders(rigidDiskBlock);
            if (cylinders < minCylinders)
            {
                errors.Add(
                    $"Rigid disk block cylinders {cylinders} is smaller than {minCylinders} cylinders used by rigid disk block and partitions");
            }

            return errors;
        }

        /// <summary>
        /// Set number of cylinders for rigid disk block and update high cylinder, disk size, parking zone, reduced
        /// write and write precompensation cylinders. Doesn't validate or write rigid disk block.
        /// </summary>
        public static void Apply(RigidDiskBlock rigidDiskBlock, uint cylinders)
        {
            if (rigidDiskBlock == null) throw new ArgumentNullException(nameof(rigidDiskBlock));
            if (cylinders == 0) throw new ArgumentOutOfRangeException(nameof(cylinders));

            var oldCylinders = rigidDiskBlock.Cylinders;

            rigidDiskBlock.Cylinders = cylinders;
            rigidDiskBlock.HiCylinder = cylinders - 1;
            rigidDiskBlock.DiskSize = cylinders * PartitionGeometry.GetCylinderSize(rigidDiskBlock);
            rigidDiskBlock.ParkingZone = MoveEndCylinder(rigidDiskBlock.ParkingZone, oldCylinders, cylinders);
            rigidDiskBlock.ReducedWrite = MoveEndCylinder(rigidDiskBlock.ReducedWrite, oldCylinders, cylinders);
            rigidDiskBlock.WritePreComp = MoveEndCylinder(rigidDiskBlock.WritePreComp, oldCylinders, cylinders);
        }

        /// <summary>
        /// Resize rigid disk block on disk to given number of cylinders. Partitions are not changed.
        /// </summary>
        public static async Task<PartitionOperationResult> Resize(Stream stream, uint cylinders, bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var result = new PartitionOperationResult();

            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
            if (rigidDiskBlock == null)
            {
                result.Errors.Add("Rigid disk block not found");
                return result;
            }

            result.RigidDiskBlock = rigidDiskBlock;

            foreach (var error in Validate(rigidDiskBlock, stream.Length, cylinders))
            {
                result.Errors.Add(error);
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            result.Messages.Add($"Resize rigid disk block from {rigidDiskBlock.Cylinders} to {cylinders} cylinders");
            Apply(rigidDiskBlock, cylinders);

            if (!dryRun)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RigidDiskBlockUpdater.Write(stream, rigidDiskBlock);
            }

            result.Success = true;
            return result;
        }

        /// <summary>
        /// Move cylinder placed at end of disk (last cylinder or number of cylinders) to new end of disk, e.g.
        /// parking zone set to last cylinder. Other cylinders are kept, but limited to new number of cylinders.
        /// </summary>
        private static uint MoveEndCylinder(uint cylinder, uint oldCylinders, uint newCylinders)
        {
            if (oldCylinders == 0 || cylinder + 1 < oldCylinders)
            {
                return Math.Min(cylinder, newCylinders);
            }

            return cylinder >= oldCylinders ? newCylinders : newCylinders - 1;
        }
    }
}
