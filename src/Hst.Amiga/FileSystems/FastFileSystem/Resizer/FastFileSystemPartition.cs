namespace Hst.Amiga.FileSystems.FastFileSystem.Resizer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Doctor;
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;

    /// <summary>
    /// Fast File System part of moving and resizing a partition.
    /// </summary>
    internal class FastFileSystemPartition : IPartitionFileSystem
    {
        public static readonly FastFileSystemPartition Instance = new FastFileSystemPartition();

        public string Name => "Fast File System";

        public bool IsSupported(byte[] dosType) =>
            dosType.Length == 4 && dosType[0] == 'D' && dosType[1] == 'O' && dosType[2] == 'S' && dosType[3] <= 7;

        public async Task<IList<string>> Check(Stream stream, PartitionBlock partitionBlock,
            CancellationToken cancellationToken)
        {
            var doctor = await FastFileSystemDoctor.Run(DoctorVolume.Open(stream, partitionBlock), Mode.Check,
                cancellationToken);

            var errors = doctor.Issues.Where(x => !x.Warning).Select(x => x.ToString()).ToList();
            if (doctor.Aborted && errors.Count == 0)
            {
                errors.Add("Doctor aborted check");
            }

            return errors;
        }

        /// <summary>
        /// Fast File System blocks are relative to start of partition, nothing to update after move.
        /// </summary>
        public Task AfterMove(Stream stream, PartitionBlock partitionBlock, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders, bool dryRun,
            IList<string> messages, CancellationToken cancellationToken) =>
            FastFileSystemResizer.ResizeInPlace(stream, partitionBlock, newCylinders, dryRun, messages,
                cancellationToken);
    }
}
