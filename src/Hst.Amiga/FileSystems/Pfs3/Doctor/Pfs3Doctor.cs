namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    /// <summary>
    /// Port of PFSDoctor (https://github.com/tonioni/pfsdoctor), the PFS3 check and repair tool.
    /// The class is split in partial files matching the original source files:
    /// Pfs3Doctor.cs (console.c), Pfs3Doctor.Access.cs (access.c), Pfs3Doctor.Device.cs (device.c),
    /// Pfs3Doctor.FullScan.cs (fullscan.c), Pfs3Doctor.StandardScan.cs (standardscan.c) and
    /// Pfs3Doctor.Stats.cs (stats.c). Globals in the original code are fields in this class.
    /// </summary>
    public partial class Pfs3Doctor
    {
        private readonly Volume volume;
        private Mode mode;
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// abort flag, filled by trap
        /// </summary>
        private bool aborting;

        private Stats stats;

        public Pfs3Doctor(Volume volume, Mode mode, CancellationToken cancellationToken = default)
        {
            this.volume = volume;
            this.mode = mode;
            this.cancellationToken = cancellationToken;
            stats = new Stats();
            ss = new ScanState();
            rext = new CachedBlock();
            tablk = new CachedBlock();
        }

        /// <summary>
        /// Statistics from last scan.
        /// </summary>
        public Stats Stats => stats;

        /// <summary>
        /// Open volume from stream and partition block.
        /// </summary>
        public static Volume OpenVolume(Stream stream, PartitionBlock partitionBlock)
        {
            return OpenVolume(stream, new DosEnvec
            {
                de_SizeBlock = partitionBlock.SizeBlock,
                de_Surfaces = partitionBlock.Surfaces,
                de_SectorPerBlock = partitionBlock.Sectors,
                de_BlocksPerTrack = partitionBlock.BlocksPerTrack,
                de_Reserved = partitionBlock.Reserved,
                de_PreAlloc = partitionBlock.PreAlloc,
                de_Interleave = partitionBlock.Interleave,
                de_LowCyl = partitionBlock.LowCyl,
                de_HighCyl = partitionBlock.HighCyl,
                de_NumBuffers = partitionBlock.NumBuffer,
                de_BufMemType = partitionBlock.BufMemType,
                de_MaxTransfer = partitionBlock.MaxTransfer,
                de_Mask = partitionBlock.Mask,
                de_BootPri = partitionBlock.BootPriority,
                de_DosType = partitionBlock.DosType == null || partitionBlock.DosType.Length < 4
                    ? 0
                    : Core.Converters.BigEndianConverter.ConvertBytesToUInt32(partitionBlock.DosType),
                de_Baud = partitionBlock.Baud,
                de_Control = partitionBlock.Control,
                de_BootBlocks = partitionBlock.BootBlocks
            }, partitionBlock.DriveName);
        }

        /// <summary>
        /// Open volume from stream and geometry.
        /// </summary>
        public static Volume OpenVolume(Stream stream, uint surfaces, uint blocksPerTrack, uint lowCyl,
            uint highCyl, uint sizeBlock, uint sectorPerBlock = 1)
        {
            return OpenVolume(stream, new DosEnvec
            {
                de_SizeBlock = sizeBlock,
                de_Surfaces = surfaces,
                de_SectorPerBlock = sectorPerBlock,
                de_BlocksPerTrack = blocksPerTrack,
                de_LowCyl = lowCyl,
                de_HighCyl = highCyl
            });
        }

        /// <summary>
        /// Open volume (OpenVolume in console.c). Initialises volume structure from dos environment vector.
        /// </summary>
        public static Volume OpenVolume(Stream stream, DosEnvec dosEnvec, string deviceName = null)
        {
            uint partitionend, b;
            int i;

            /* init volume structure */
            var volume = new Volume
            {
                Stream = stream,
                DosEnvec = dosEnvec,
                DeviceName = deviceName ?? string.Empty
            };
            var cylsectors = dosEnvec.de_Surfaces * dosEnvec.de_BlocksPerTrack;

            /* saturate instead of wrapping at 2^32 sectors (2TB with 512 byte
             * sectors): a wrapped partition end turns every derived value
             * (lastblock, partition size, the access mode test read) into
             * garbage. Losing the last partial block is the safe direction.
             */
            if (cylsectors != 0 && (dosEnvec.de_HighCyl + 1) > 0xFFFFFFFFUL / cylsectors)
                partitionend = 0xFFFFFFFF;
            else
                partitionend = (dosEnvec.de_HighCyl + 1) * cylsectors;

            volume.FirstBlock = dosEnvec.de_LowCyl * cylsectors;
            volume.LastBlock = partitionend;
            volume.FullDiskSizeNative = volume.LastBlock - volume.FirstBlock;

            b = dosEnvec.de_SectorPerBlock;
            volume.BlockLogShift = 0;
            while (b > 1)
            {
                volume.BlockLogShift++;
                b >>= 1;
            }

            volume.FirstBlock += (1U << volume.BlockLogShift) - 1;
            volume.FirstBlock &= ~((1U << volume.BlockLogShift) - 1);
            volume.LastBlock -= 1U << volume.BlockLogShift;
            volume.LastBlock &= ~((1U << volume.BlockLogShift) - 1);

            b = volume.BlockSize = (dosEnvec.de_SizeBlock << 2) << volume.BlockLogShift;
            for (i = -1; b != 0; i++)
                b >>= 1;
            volume.BlockShift = i;
            volume.ResCluster = 0;

            volume.DiskSizeNative = volume.LastBlock - volume.FirstBlock;
            volume.FirstBlockNative = volume.FirstBlock;
            volume.LastBlockNative = volume.LastBlock + (1U << volume.BlockLogShift) - 1;

            volume.FirstBlock >>= volume.BlockLogShift;
            volume.LastBlock >>= volume.BlockLogShift;

            /* usable blocks, derived from the aligned limits. Deriving it from
             * the raw partition size counts one block too many whenever the
             * partition start is not a multiple of the logical block size (with
             * 255x63 geometry and 4K blocks, 7 out of 8 LowCyl values): that
             * phantom block past lastblock can never be read or written.
             */
            volume.DiskSize = volume.LastBlock - volume.FirstBlock + 1;

            volume.LastReserved = volume.DiskSize - 256; /* temp value, calculated later */

            volume.Cache.LineSize = 64;
            volume.Cache.NoLines = 32;
            volume.Cache.BlockSize = volume.BlockSize;

            volume.GetRawBlocks = (data, blocks, blocknr) => DevGetBlocks(volume, data, blocks, blocknr);
            volume.WriteRawBlocks = (data, blocks, blocknr) => DevWriteBlocks(volume, data, blocks, blocknr);

            return volume;
        }

        /// <summary>
        /// Run doctor on volume (main in console.c). Mode check only reports errors and doesn't write
        /// any changes, mode repair fixes errors, mode unformat undo a fast format and mode info shows
        /// information about the volume.
        /// </summary>
        public static async Task<Pfs3Doctor> Run(Volume volume, Mode mode, bool verbose = false,
            CancellationToken cancellationToken = default)
        {
            StandardScanFlags opties;

            if (mode == Mode.Repair)
                opties = StandardScanFlags.Fix | StandardScanFlags.Analyse | StandardScanFlags.GenBmMask;
            else if (mode == Mode.Unformat)
                opties = StandardScanFlags.Unformat | StandardScanFlags.Fix | StandardScanFlags.Analyse |
                         StandardScanFlags.GenBmMask;
            else if (mode == Mode.Info)
                opties = StandardScanFlags.Info;
            else
                opties = StandardScanFlags.Check | StandardScanFlags.Analyse | StandardScanFlags.GenBmMask;

            if (verbose)
                opties |= StandardScanFlags.Verbose;

            /* check mode doesn't write any changes */
            if (mode == Mode.Check)
                volume.WriteRawBlocks = (data, blocks, blocknr) => DevWriteBlocksDummy(volume, data, blocks, blocknr);
            else
                volume.WriteRawBlocks = (data, blocks, blocknr) => DevWriteBlocks(volume, data, blocks, blocknr);

            var doctor = new Pfs3Doctor(volume, mode, cancellationToken);
            await doctor.InitCache(volume.Cache.LineSize, volume.Cache.NoLines, volume.BlockSize); /* make this configurable ? */
            doctor.Result = await doctor.StandardScan(opties);
            await doctor.CloseVolume();

            return doctor;
        }

        /// <summary>
        /// Result of last standard scan.
        /// </summary>
        public Error Result { get; private set; }

        /// <summary>
        /// Close volume (CloseVolume in console.c). Writes dirty cache lines.
        /// </summary>
        public async Task CloseVolume()
        {
            await FreeCache();
            await volume.Stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// size of reserved block (SIZEOF_RESBLOCK)
        /// </summary>
        private uint SizeOfResBlock => rbl.ReservedBlksize;

        /// <summary>
        /// longs per bitmap block (LONGS_PER_BMB)
        /// </summary>
        private uint LongsPerBmb => ((uint)rbl.ReservedBlksize / 4) - 3;

        /// <summary>
        /// indexes per block (INDEX_PER_BLOCK)
        /// </summary>
        private uint IndexPerBlock => ((uint)rbl.ReservedBlksize - 3 * 4) / 4;

        /// <summary>
        /// anodes per block (ANODES_PER_BLOCK)
        /// </summary>
        private uint AnodesPerBlock => ((uint)rbl.ReservedBlksize - 4 * 4) / Anode.SizeOf;
    }
}
