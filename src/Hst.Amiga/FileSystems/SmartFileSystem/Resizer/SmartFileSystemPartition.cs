namespace Hst.Amiga.FileSystems.SmartFileSystem.Resizer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using Doctor;
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;
    using Constants = SmartFileSystem.Constants;

    /// <summary>
    /// Smart File System part of moving and resizing a partition.
    /// </summary>
    internal class SmartFileSystemPartition : IPartitionFileSystem
    {
        public static readonly SmartFileSystemPartition Instance = new SmartFileSystemPartition();

        public string Name => "Smart File System";

        public bool IsSupported(byte[] dosType) =>
            dosType.Length == 4 && BigEndianConverter.ConvertBytesToUInt32(dosType) == Constants.DosTypeSfs0;

        public async Task<IList<string>> Check(Stream stream, PartitionBlock partitionBlock,
            CancellationToken cancellationToken)
        {
            var doctor = await SmartFileSystemDoctor.Run(DoctorVolume.Open(stream, partitionBlock), Mode.Check,
                cancellationToken);

            var errors = doctor.Issues.Where(x => !x.Warning).Select(x => x.ToString()).ToList();
            if (doctor.Aborted && errors.Count == 0)
            {
                errors.Add("Doctor aborted check");
            }

            return errors;
        }

        /// <summary>
        /// Update first and last byte of partition in both root blocks.
        /// </summary>
        public async Task AfterMove(Stream stream, PartitionBlock partitionBlock,
            CancellationToken cancellationToken)
        {
            var geometry = SmartFileSystemHelper.GetGeometry(partitionBlock);
            var io = new PartitionBlockIo(stream, geometry.FirstByte, geometry.BlockSize, cancellationToken);
            var totalBlocks = (uint)((geometry.LastByte - geometry.FirstByte) / geometry.BlockSize);

            foreach (var block in new[] { 0U, totalBlocks - 1 })
            {
                var data = await io.Read(block);
                if (BigEndianConverter.ConvertBytesToUInt32(data, 0) != Constants.RootBlockId)
                {
                    continue;
                }

                SetRootPosition(data, geometry.FirstByte, geometry.LastByte);
                await io.Write(block, data);
            }

            await io.Flush();
        }

        public Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders, bool dryRun,
            IList<string> messages, CancellationToken cancellationToken) =>
            SmartFileSystemResizer.ResizeInPlace(stream, partitionBlock, newCylinders, dryRun, messages,
                cancellationToken);

        /// <summary>
        /// Set first and last byte in root block and update checksum.
        /// </summary>
        internal static void SetRootPosition(byte[] data, long firstByte, long lastByte)
        {
            BigEndianConverter.ConvertUInt32ToBytes((uint)((ulong)firstByte >> 32), data, 0x20);
            BigEndianConverter.ConvertUInt32ToBytes((uint)firstByte, data, 0x24);
            BigEndianConverter.ConvertUInt32ToBytes((uint)((ulong)lastByte >> 32), data, 0x28);
            BigEndianConverter.ConvertUInt32ToBytes((uint)lastByte, data, 0x2c);
            ChecksumHelper.UpdateChecksum(data);
        }
    }
}
