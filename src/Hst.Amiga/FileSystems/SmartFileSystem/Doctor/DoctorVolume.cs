namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.IO;
    using Blocks;
    using RigidDiskBlocks;
    using BlockHelper = Blocks.BlockHelper;

    /// <summary>
    /// Smart file system volume opened by the doctor. Geometry is calculated the same way as when mounting a smart
    /// file system volume. Blocks are read and written as raw bytes, so corrupt blocks never throw.
    /// </summary>
    public class DoctorVolume
    {
        /// <summary>
        /// Stream with volume.
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// Byte offset of first block of volume in stream.
        /// </summary>
        public long PartitionStartOffset { get; set; }

        /// <summary>
        /// Size of volume in bytes.
        /// </summary>
        public long PartitionSize { get; set; }

        /// <summary>
        /// Size of file system blocks in bytes.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Number of file system blocks in volume.
        /// </summary>
        public uint Blocks { get; set; }

        /// <summary>
        /// Number of reserved blocks at start of volume, which are marked used in bitmap. Minimum 1 for root block.
        /// </summary>
        public uint Reserved { get; set; }

        /// <summary>
        /// Number of reserved blocks at end of volume (pre alloc), which are marked used in bitmap. Minimum 1 for
        /// second root block.
        /// </summary>
        public uint PreAlloc { get; set; }

        /// <summary>
        /// Name of device or partition, used for messages.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// Show message delegate, messages are terminated with new line.
        /// </summary>
        public Action<string> ShowMsg { get; set; } = _ => { };

        /// <summary>
        /// Open volume from stream and partition block.
        /// </summary>
        public static DoctorVolume Open(Stream stream, PartitionBlock partitionBlock)
        {
            if (partitionBlock == null) throw new ArgumentNullException(nameof(partitionBlock));

            var geometry = SmartFileSystemHelper.GetGeometry(partitionBlock);
            var volume = Open(stream, geometry.FirstByte, geometry.LastByte - geometry.FirstByte, geometry.BlockSize,
                partitionBlock.Reserved, partitionBlock.PreAlloc);
            volume.DeviceName = partitionBlock.DriveName ?? string.Empty;
            return volume;
        }

        /// <summary>
        /// Open volume from stream, partition offset and size.
        /// </summary>
        /// <param name="stream">Stream with volume.</param>
        /// <param name="partitionOffset">Byte offset of partition in stream.</param>
        /// <param name="partitionSize">Size of partition in bytes.</param>
        /// <param name="blockSize">File system block size.</param>
        /// <param name="reserved">Reserved blocks at start of partition.</param>
        /// <param name="preAlloc">Reserved blocks at end of partition.</param>
        public static DoctorVolume Open(Stream stream, long partitionOffset, long partitionSize, int blockSize,
            uint reserved = 2, uint preAlloc = 0)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            BlockHelper.ValidateBlockSize(blockSize);

            return new DoctorVolume
            {
                Stream = stream,
                PartitionStartOffset = partitionOffset,
                PartitionSize = partitionSize,
                BlockSize = blockSize,
                Blocks = (uint)Math.Min(partitionSize / blockSize, uint.MaxValue),
                Reserved = Math.Max(reserved, 1),
                PreAlloc = Math.Max(preAlloc, 1),
                DeviceName = string.Empty
            };
        }
    }
}
