namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System;
    using Extensions;

    /// <summary>
    /// File system found on disk by scanning for file system boot and root blocks.
    /// </summary>
    public class FoundFileSystem
    {
        public FileSystemType Type { get; set; }

        /// <summary>
        /// Dos type read from file system boot or root block.
        /// </summary>
        public byte[] DosType { get; set; }

        public string DosTypeFormatted => DosType.FormatDosType();

        /// <summary>
        /// Start sector (512 bytes) of partition.
        /// </summary>
        public long StartSector { get; set; }

        /// <summary>
        /// Size of partition in sectors (512 bytes). For fast file system, size is estimated from root block
        /// location as minimum size until geometry is resolved.
        /// </summary>
        public long Sectors { get; set; }

        /// <summary>
        /// Maximum size of partition in sectors. Same as sectors when size is exact.
        /// </summary>
        public long MaxSectors { get; set; }

        /// <summary>
        /// End sector (exclusive) of partition.
        /// </summary>
        public long EndSector => StartSector + Sectors;

        /// <summary>
        /// Size of partition is exact, read from file system root block.
        /// </summary>
        public bool IsExactSize { get; set; }

        /// <summary>
        /// Sector root block was found at.
        /// </summary>
        public long RootBlockSector { get; set; }

        public uint FileSystemBlockSize { get; set; }

        /// <summary>
        /// Sector offset of professional file system 3 root block extension relative to volume start. 0, if not
        /// available.
        /// </summary>
        public long RootBlockExtensionSector { get; set; }

        /// <summary>
        /// Number of sectors boot and root blocks are displaced from volume start, e.g. a copy of boot and root
        /// blocks written at a wrong offset. Start sector is volume start. 0, if not displaced.
        /// </summary>
        public long DisplacedSectors { get; set; }

        /// <summary>
        /// Boot and root blocks are displaced from volume start and must be restored to mount volume.
        /// </summary>
        public bool IsDisplaced => DisplacedSectors != 0;

        /// <summary>
        /// Block offset of first bitmap block relative to partition start, read from fast file system root block.
        /// Format places first bitmap block after root block, which makes it a hint for the root block offset that
        /// doesn't depend on the boot block location. 0, if not available.
        /// </summary>
        public uint BitmapBlockOffset { get; set; }

        /// <summary>
        /// Number of reserved blocks at start of partition.
        /// </summary>
        public uint Reserved { get; set; }

        public string VolumeName { get; set; }

        /// <summary>
        /// Date file system was created, used to prefer newest file system when found file systems overlap.
        /// </summary>
        public DateTime CreationDate { get; set; }

        public override string ToString() =>
            $"{Type} '{VolumeName}' ({DosTypeFormatted}) at sector {StartSector}, {(IsExactSize ? string.Empty : "estimated ")}size {Sectors} sectors{(IsDisplaced ? $", boot and root blocks displaced {DisplacedSectors} sectors" : string.Empty)}";
    }
}
