namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.IO;
    using RigidDiskBlocks;

    /// <summary>
    /// Fast File System volume opened by the doctor. Geometry is calculated the same way as when mounting a
    /// Fast File System volume. Blocks are read and written as raw bytes, so corrupt blocks never throw.
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
        /// Size of file system blocks in bytes.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Number of file system blocks in volume.
        /// </summary>
        public uint Blocks { get; set; }

        /// <summary>
        /// Number of reserved blocks at start of volume (boot blocks). Bitmap starts at first block after reserved
        /// blocks.
        /// </summary>
        public uint Reserved { get; set; }

        /// <summary>
        /// Block number of root block.
        /// </summary>
        public uint RootBlock { get; set; }

        /// <summary>
        /// Dos type to use for volume, e.g. 0x444F5303 for DOS\3. If 0, dos type is read from boot block.
        /// </summary>
        public uint DosType { get; set; }

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

            var dosType = partitionBlock.DosType != null && partitionBlock.DosType.Length == 4
                ? Core.Converters.BigEndianConverter.ConvertBytesToUInt32(partitionBlock.DosType)
                : 0U;

            var volume = Open(stream, partitionBlock.LowCyl, partitionBlock.HighCyl, partitionBlock.Surfaces,
                partitionBlock.BlocksPerTrack, partitionBlock.Reserved, partitionBlock.BlockSize,
                partitionBlock.FileSystemBlockSize, dosType);
            volume.DeviceName = partitionBlock.DriveName ?? string.Empty;
            return volume;
        }

        /// <summary>
        /// Open volume from adf stream. Dos type is read from boot block.
        /// </summary>
        public static DoctorVolume OpenAdf(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            switch (stream.Length)
            {
                case FloppyDiskConstants.DoubleDensity.Size:
                    return Open(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
                        FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.Heads,
                        FloppyDiskConstants.DoubleDensity.Sectors, FloppyDiskConstants.DoubleDensity.ReservedBlocks);
                case FloppyDiskConstants.HighDensity.Size:
                    return Open(stream, FloppyDiskConstants.HighDensity.LowCyl,
                        FloppyDiskConstants.HighDensity.HighCyl, FloppyDiskConstants.HighDensity.Heads,
                        FloppyDiskConstants.HighDensity.Sectors, FloppyDiskConstants.HighDensity.ReservedBlocks);
                default:
                    throw new IOException($"Invalid adf size '{stream.Length}'");
            }
        }

        /// <summary>
        /// Open volume from stream and geometry.
        /// </summary>
        public static DoctorVolume Open(Stream stream, uint lowCyl, uint highCyl, uint surfaces,
            uint blocksPerTrack, uint reserved = 2, uint blockSize = 512, uint fileSystemBlockSize = 512,
            uint dosType = 0)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (blockSize == 0 || fileSystemBlockSize < blockSize || fileSystemBlockSize % blockSize != 0)
            {
                throw new ArgumentException($"Invalid block size {blockSize} and file system block size {fileSystemBlockSize}");
            }

            var cylinders = highCyl - lowCyl + 1;
            var blocksPerCylinder = (long)surfaces * blocksPerTrack;
            var blocks = cylinders * blocksPerCylinder / (fileSystemBlockSize / blockSize);

            return new DoctorVolume
            {
                Stream = stream,
                PartitionStartOffset = lowCyl * blocksPerCylinder * blockSize,
                BlockSize = (int)fileSystemBlockSize,
                Blocks = (uint)Math.Min(blocks, uint.MaxValue),
                Reserved = reserved,
                RootBlock = OffsetHelper.CalculateRootBlockOffset(lowCyl, highCyl, reserved, surfaces,
                    blocksPerTrack, fileSystemBlockSize),
                DosType = dosType,
                DeviceName = string.Empty
            };
        }
    }
}
