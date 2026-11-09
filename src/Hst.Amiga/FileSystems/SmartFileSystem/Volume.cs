namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System.Collections.Generic;
    using System.IO;
    using Blocks;

    /// <summary>
    /// Mounted smart file system volume state.
    /// </summary>
    public class Volume
    {
        public Stream Stream { get; set; }

        /// <summary>
        /// Byte offset of partition start in stream.
        /// </summary>
        public long PartitionOffset { get; set; }

        public int BlockSize { get; set; }
        public uint TotalBlocks { get; set; }

        /// <summary>
        /// Shift to convert between block number and 32 byte offsets used by node index containers.
        /// </summary>
        public int ShiftsBlock32 { get; set; }

        public RootBlock RootBlock { get; set; }
        public bool CaseSensitive { get; set; }

        public BitmapBlock[] BitmapBlocks { get; set; }
        public int BlocksInBitmap { get; set; }
        public uint FreeBlocks { get; set; }

        /// <summary>
        /// Block where search for free data blocks starts.
        /// </summary>
        public uint RovingPointer { get; set; }

        /// <summary>
        /// Blocks overriding blocks in stream, used when a pending transaction is replayed on a read-only stream.
        /// </summary>
        public IDictionary<uint, byte[]> Overlay { get; }

        public IList<string> Logs { get; }

        public Volume()
        {
            Overlay = new Dictionary<uint, byte[]>();
            Logs = new List<string>();
            BitmapBlocks = System.Array.Empty<BitmapBlock>();
        }

        public bool IsReadOnly => !Stream.CanWrite;
    }
}
