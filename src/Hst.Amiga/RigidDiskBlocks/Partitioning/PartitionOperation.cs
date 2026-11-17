namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;

    /// <summary>
    /// Common flow for moving and resizing a partition: read rigid disk block, validate new location, check file
    /// system, move or resize file system, write rigid disk block and check file system again.
    /// </summary>
    internal static class PartitionOperation
    {
        public static async Task<PartitionOperationResult> Move(Stream stream, IPartitionFileSystem fileSystem,
            int partitionNumber, PartitionMoveOptions options, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var result = new PartitionOperationResult();

            try
            {
                var rigidDiskBlock = await ReadRigidDiskBlock(stream);
                var partitionBlock = GetPartitionBlock(rigidDiskBlock, partitionNumber, fileSystem);
                result.RigidDiskBlock = rigidDiskBlock;
                result.PartitionBlock = partitionBlock;

                ApplyRigidDiskBlockCylinders(stream, rigidDiskBlock, options.RigidDiskBlockCylinders, result);

                var lowCyl = partitionBlock.LowCyl;
                var cylinders = partitionBlock.HighCyl - partitionBlock.LowCyl + 1;
                var newLowCyl = options.NewLowCyl;
                var newHighCyl = (long)newLowCyl + cylinders - 1;
                if (newHighCyl > uint.MaxValue)
                {
                    throw new PartitionOperationException($"New low cylinder {newLowCyl} is too large");
                }

                ValidateLocation(rigidDiskBlock, partitionBlock, newLowCyl, (uint)newHighCyl);
                ValidateRigidDiskBlockCylinders(stream, rigidDiskBlock, partitionBlock, newLowCyl, (uint)newHighCyl);

                await CheckFileSystem(stream, fileSystem, partitionBlock, "before move", cancellationToken);

                result.Messages.Add(
                    $"Move partition '{partitionBlock.DriveName}' from cylinders {lowCyl}-{partitionBlock.HighCyl} to {newLowCyl}-{newHighCyl}");

                if (options.DryRun)
                {
                    result.Success = true;
                    return result;
                }

                if (newLowCyl != lowCyl)
                {
                    await MoveData(stream, fileSystem, partitionBlock, newLowCyl, cancellationToken);
                }

                await RigidDiskBlockUpdater.Write(stream, rigidDiskBlock);

                await CheckAfter(stream, fileSystem, partitionBlock, result, cancellationToken);
            }
            catch (PartitionOperationException e)
            {
                result.Errors.Add(e.Message);
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        public static async Task<PartitionOperationResult> Resize(Stream stream, IPartitionFileSystem fileSystem,
            int partitionNumber, PartitionResizeOptions options, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var result = new PartitionOperationResult();

            try
            {
                var rigidDiskBlock = await ReadRigidDiskBlock(stream);
                var partitionBlock = GetPartitionBlock(rigidDiskBlock, partitionNumber, fileSystem);
                result.RigidDiskBlock = rigidDiskBlock;
                result.PartitionBlock = partitionBlock;

                ApplyRigidDiskBlockCylinders(stream, rigidDiskBlock, options.RigidDiskBlockCylinders, result);

                var lowCyl = partitionBlock.LowCyl;
                var highCyl = partitionBlock.HighCyl;
                var cylinders = highCyl - lowCyl + 1;
                var newLowCyl = options.NewLowCyl ?? lowCyl;
                var newHighCyl = options.NewHighCyl;

                if (newHighCyl < newLowCyl)
                {
                    throw new PartitionOperationException(
                        $"New high cylinder {newHighCyl} is before new low cylinder {newLowCyl}");
                }

                var newCylinders = newHighCyl - newLowCyl + 1;

                var available = PartitionGeometry.GetAvailableCylinders(rigidDiskBlock, partitionBlock);
                if (!available.Contains(newLowCyl, newHighCyl))
                {
                    throw new PartitionOperationException(
                        $"New cylinders {newLowCyl}-{newHighCyl} are outside cylinders {available} available for partition '{partitionBlock.DriveName}'");
                }

                ValidateRigidDiskBlockCylinders(stream, rigidDiskBlock, partitionBlock, newLowCyl, newHighCyl);

                await CheckFileSystem(stream, fileSystem, partitionBlock, "before resize", cancellationToken);

                result.Messages.Add(
                    $"Resize partition '{partitionBlock.DriveName}' from cylinders {lowCyl}-{highCyl} to {newLowCyl}-{newHighCyl}");

                if (options.DryRun)
                {
                    if (newCylinders != cylinders)
                    {
                        await fileSystem.ResizeInPlace(stream, partitionBlock, newCylinders, true, result.Messages,
                            cancellationToken);
                    }

                    result.Success = true;
                    return result;
                }

                if (newCylinders >= cylinders)
                {
                    // expand: move partition to new low cylinder first, then expand file system in place
                    if (newLowCyl != lowCyl)
                    {
                        await MoveData(stream, fileSystem, partitionBlock, newLowCyl, cancellationToken);
                    }

                    if (newCylinders != cylinders)
                    {
                        await fileSystem.ResizeInPlace(stream, partitionBlock, newCylinders, false, result.Messages,
                            cancellationToken);
                        PartitionGeometry.SetCylinders(partitionBlock, partitionBlock.LowCyl, newHighCyl);
                    }
                }
                else
                {
                    // shrink: shrink file system in place first, then move partition to new low cylinder
                    await fileSystem.ResizeInPlace(stream, partitionBlock, newCylinders, false, result.Messages,
                        cancellationToken);
                    PartitionGeometry.SetCylinders(partitionBlock, lowCyl, lowCyl + newCylinders - 1);

                    if (newLowCyl != lowCyl)
                    {
                        await MoveData(stream, fileSystem, partitionBlock, newLowCyl, cancellationToken);
                    }
                }

                await RigidDiskBlockUpdater.Write(stream, rigidDiskBlock);

                await CheckAfter(stream, fileSystem, partitionBlock, result, cancellationToken);
            }
            catch (PartitionOperationException e)
            {
                result.Errors.Add(e.Message);
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        private static async Task<RigidDiskBlock> ReadRigidDiskBlock(Stream stream)
        {
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
            if (rigidDiskBlock == null)
            {
                throw new PartitionOperationException("Rigid disk block not found");
            }

            return rigidDiskBlock;
        }

        private static PartitionBlock GetPartitionBlock(RigidDiskBlock rigidDiskBlock, int partitionNumber,
            IPartitionFileSystem fileSystem)
        {
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                throw new PartitionOperationException(
                    $"Partition number {partitionNumber} not found, rigid disk block has {partitionBlocks.Count} partitions");
            }

            var partitionBlock = partitionBlocks[partitionNumber - 1];
            if (partitionBlock.DosType == null || !fileSystem.IsSupported(partitionBlock.DosType))
            {
                throw new PartitionOperationException(
                    $"Partition '{partitionBlock.DriveName}' with dos type '{partitionBlock.DosType?.FormatDosType()}' is not {fileSystem.Name}");
            }

            if (PartitionGeometry.GetCylinderSize(partitionBlock) <= 0)
            {
                throw new PartitionOperationException($"Partition '{partitionBlock.DriveName}' has invalid geometry");
            }

            return partitionBlock;
        }

        private static void ApplyRigidDiskBlockCylinders(Stream stream, RigidDiskBlock rigidDiskBlock,
            uint? cylinders, PartitionOperationResult result)
        {
            if (!cylinders.HasValue || cylinders.Value == rigidDiskBlock.Cylinders)
            {
                return;
            }

            var maxCylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);
            if (cylinders.Value > maxCylinders)
            {
                throw new PartitionOperationException(
                    $"Rigid disk block cylinders {cylinders.Value} is larger than disk size {stream.Length} bytes, which holds {maxCylinders} cylinders");
            }

            if (cylinders.Value <= rigidDiskBlock.LoCylinder)
            {
                throw new PartitionOperationException(
                    $"Rigid disk block cylinders {cylinders.Value} must be larger than low cylinder {rigidDiskBlock.LoCylinder}");
            }

            result.Messages.Add(
                $"Resize rigid disk block from {rigidDiskBlock.Cylinders} to {cylinders.Value} cylinders");
            RigidDiskBlockResizer.Apply(rigidDiskBlock, cylinders.Value);
        }

        private static void ValidateLocation(RigidDiskBlock rigidDiskBlock, PartitionBlock partitionBlock,
            uint lowCyl, uint highCyl)
        {
            var rdbCylinders = PartitionGeometry.GetRigidDiskBlockCylinders(rigidDiskBlock, partitionBlock);
            if (!rdbCylinders.Contains(lowCyl, highCyl))
            {
                throw new PartitionOperationException(
                    $"New cylinders {lowCyl}-{highCyl} are outside cylinders {rdbCylinders} of rigid disk block");
            }

            var overlapping = PartitionGeometry.FindOverlappingPartition(rigidDiskBlock, partitionBlock, lowCyl,
                highCyl);
            if (overlapping != null)
            {
                throw new PartitionOperationException(
                    $"New cylinders {lowCyl}-{highCyl} overlap partition '{overlapping.DriveName}' at cylinders {overlapping.LowCyl}-{overlapping.HighCyl}");
            }
        }

        /// <summary>
        /// Validate partitions fit within rigid disk block cylinders, when partition is placed at new cylinders.
        /// </summary>
        private static void ValidateRigidDiskBlockCylinders(Stream stream, RigidDiskBlock rigidDiskBlock,
            PartitionBlock partitionBlock, uint lowCyl, uint highCyl)
        {
            var rdbEnd = (rigidDiskBlock.HiCylinder + 1L) * PartitionGeometry.GetCylinderSize(rigidDiskBlock);
            var cylinderSize = PartitionGeometry.GetCylinderSize(partitionBlock);

            if ((highCyl + 1L) * cylinderSize > rdbEnd)
            {
                throw new PartitionOperationException(
                    $"New cylinders {lowCyl}-{highCyl} are outside rigid disk block with {rigidDiskBlock.Cylinders} cylinders");
            }

            foreach (var other in rigidDiskBlock.PartitionBlocks.Where(x => !ReferenceEquals(x, partitionBlock)))
            {
                if (PartitionGeometry.GetEndOffset(other) > rdbEnd)
                {
                    throw new PartitionOperationException(
                        $"Partition '{other.DriveName}' at cylinders {other.LowCyl}-{other.HighCyl} is outside rigid disk block with {rigidDiskBlock.Cylinders} cylinders");
                }
            }

            if (rdbEnd > stream.Length)
            {
                throw new PartitionOperationException(
                    $"Rigid disk block with {rigidDiskBlock.Cylinders} cylinders is larger than disk size {stream.Length} bytes");
            }
        }

        private static async Task CheckFileSystem(Stream stream, IPartitionFileSystem fileSystem,
            PartitionBlock partitionBlock, string when, CancellationToken cancellationToken)
        {
            var errors = await fileSystem.Check(stream, partitionBlock, cancellationToken);
            if (errors.Count == 0)
            {
                return;
            }

            throw new PartitionOperationException(
                $"{fileSystem.Name} in partition '{partitionBlock.DriveName}' has errors {when}, run doctor to repair: {string.Join("; ", errors.Take(10))}");
        }

        private static async Task CheckAfter(Stream stream, IPartitionFileSystem fileSystem,
            PartitionBlock partitionBlock, PartitionOperationResult result, CancellationToken cancellationToken)
        {
            foreach (var error in await fileSystem.Check(stream, partitionBlock, cancellationToken))
            {
                result.Errors.Add($"{fileSystem.Name} check after operation: {error}");
            }
        }

        /// <summary>
        /// Move data of partition to new low cylinder and update partition block with new location.
        /// </summary>
        private static async Task MoveData(Stream stream, IPartitionFileSystem fileSystem,
            PartitionBlock partitionBlock, uint newLowCyl, CancellationToken cancellationToken)
        {
            var cylinders = partitionBlock.HighCyl - partitionBlock.LowCyl + 1;
            var sourceOffset = PartitionGeometry.GetStartOffset(partitionBlock);
            var size = PartitionGeometry.GetSize(partitionBlock);
            var destinationOffset = newLowCyl * PartitionGeometry.GetCylinderSize(partitionBlock);

            await BlockCopier.Copy(stream, sourceOffset, destinationOffset, size,
                cancellationToken: cancellationToken);

            PartitionGeometry.SetCylinders(partitionBlock, newLowCyl, newLowCyl + cylinders - 1);

            await fileSystem.AfterMove(stream, partitionBlock, cancellationToken);
        }
    }
}
