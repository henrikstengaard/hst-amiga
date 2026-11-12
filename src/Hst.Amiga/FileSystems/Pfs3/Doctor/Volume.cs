namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Volume (volume_t in doctor.h).
    /// </summary>
    public class Volume
    {
        /* initialise size */

        /// <summary>
        /// abs blocknr, first and last block
        /// </summary>
        public uint FirstBlock { get; set; }

        public uint LastBlock { get; set; }
        public uint FirstBlockNative { get; set; }
        public uint LastBlockNative { get; set; }

        /// <summary>
        /// disksize in blocks
        /// </summary>
        public uint DiskSize { get; set; }

        public uint DiskSizeNative { get; set; }
        public uint FullDiskSizeNative { get; set; }

        /// <summary>
        /// rel blocknr, last reserved block
        /// </summary>
        public uint LastReserved { get; set; }

        /// <summary>
        /// physical blocksize in bytes
        /// </summary>
        public uint BlockSize { get; set; }

        public int BlockShift { get; set; }
        public int BlockLogShift { get; set; }
        public uint ResCluster { get; set; }

        /* info */
        public string DiskName { get; set; }
        public int FnSize { get; set; }

        /* status
         * keep message if message==NULL
         * status 0 = diskname
         */

        /// <summary>
        /// Status (level, message, maxval).
        /// </summary>
        public Action<int, string, long> Status { get; set; }

        /// <summary>
        /// Progress (level, progress).
        /// </summary>
        public Action<int, long> Progress { get; set; }

        /// <summary>
        /// Update stats.
        /// </summary>
        public Action UpdateStats { get; set; }

        /// <summary>
        /// Show message.
        /// </summary>
        public Action<string> ShowMsg { get; set; }

        /// <summary>
        /// Ask user (message, okstr, cancelstr). Returns true for ok. Default answers ok (non-interactive).
        /// </summary>
        public Func<string, string, string, bool> AskUser { get; set; }

        /// <summary>
        /// Open stream to write mountfile to, used when partition is repartitioned (replaces ASL file requester).
        /// Returns null if no mountfile should be written.
        /// </summary>
        public Func<Stream> OpenMountFile { get; set; }

        /* flags */
        public bool Repartitioned { get; set; }

        /// <summary>
        /// 0=not done/needed, 1=fixed, -1=not fixable
        /// </summary>
        public int StandardScan { get; set; }

        /* bitmaps */
        public Bitmap MainBitmap { get; set; }
        public Bitmap AnodeBitmap { get; set; }
        public Bitmap ResBitmap { get; set; }

        /* full scan */

        /// <summary>
        /// elements are of type buildblock
        /// </summary>
        public LinkedList<CachedBlock> BuildBlocks { get; set; }

        /* private */
        public DosEnvec DosEnvec { get; set; }
        public string DeviceName { get; set; }
        public string ExecDevice { get; set; }
        public int ExecUnit { get; set; }
        public uint Flags { get; set; }

        /* device stuff */
        public Stream Stream { get; set; }
        public Cache Cache { get; }

        /// <summary>
        /// Get raw blocks (data, numblock, bloknr). Blocknrs are disk (not partition!) based.
        /// </summary>
        public Func<byte[], int, uint, Task<Error>> GetRawBlocks { get; set; }

        /// <summary>
        /// Write raw blocks (data, numblock, bloknr). Blocknrs are disk (not partition!) based.
        /// </summary>
        public Func<byte[], int, uint, Task<Error>> WriteRawBlocks { get; set; }

        public Volume()
        {
            DiskName = string.Empty;
            Status = (level, message, maxval) => { };
            Progress = (level, progress) => { };
            UpdateStats = () => { };
            ShowMsg = message => { };
            AskUser = (message, okstr, cancelstr) => true;
            OpenMountFile = () => null;
            BuildBlocks = new LinkedList<CachedBlock>();
            DeviceName = string.Empty;
            ExecDevice = string.Empty;
            Cache = new Cache();
        }
    }
}
