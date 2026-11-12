namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System;

    public class RecoveryOptions
    {
        public RecoveryMode Mode { get; set; } = RecoveryMode.Auto;

        /// <summary>
        /// Size of disk in bytes. Stream length is used, if 0.
        /// </summary>
        public long DiskSize { get; set; }

        /// <summary>
        /// Heads used for rigid disk block geometry. Geometry is resolved from existing blocks and file systems
        /// found on disk, if 0.
        /// </summary>
        public uint Heads { get; set; }

        /// <summary>
        /// Sectors per track used for rigid disk block geometry. Geometry is resolved from existing blocks and
        /// file systems found on disk, if 0.
        /// </summary>
        public uint Sectors { get; set; }

        /// <summary>
        /// Dos type used for partitions with professional file system 3 found, 3 characters followed by version
        /// digit, e.g. "PDS3" or "PDS\3".
        /// </summary>
        public string Pfs3DosType { get; set; } = "PDS3";

        /// <summary>
        /// Dos type used for partitions with smart file system found.
        /// </summary>
        public string SfsDosType { get; set; } = "SFS0";

        /// <summary>
        /// Prefix used for drive names of added partitions.
        /// </summary>
        public string DriveNamePrefix { get; set; } = "DH";

        /// <summary>
        /// Skip file systems found with size of double density (1760 sectors) or high density (3520 sectors)
        /// floppy disks, which are likely .adf floppy images stored as files within a partition.
        /// </summary>
        public bool SkipFloppyImages { get; set; }

        /// <summary>
        /// Size of buffer used to scan disk for file systems.
        /// </summary>
        public int ScanBufferSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Progress reporting number of bytes scanned.
        /// </summary>
        public IProgress<long> Progress { get; set; }
    }
}
