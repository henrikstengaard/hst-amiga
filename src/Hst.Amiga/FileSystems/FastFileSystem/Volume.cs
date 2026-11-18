namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Blocks;

    internal class Volume
    {
        public int BlockSize { get; set; }
        public int FileSystemBlockSize { get; set; }
        public Stream Stream { get; set; }
        public uint FirstBlock { get; set; }
        public uint LastBlock { get; set; }
        public uint Blocks { get; set; }
        public long PartitionStartOffset { get; set; }
        public uint Reserved { get; set; }
        public int DosType { get; set; }
        public uint DataBlockSize { get; set; }

        /// <summary>
        /// Size of hashtable and data blocks
        /// </summary>
        public uint IndexSize { get; set; }
        public uint RootBlockOffset { get; set; }
        public RootBlock RootBlock { get; set; }
        public bool Mounted { get; set; }
        public bool ReadOnly { get; set; }
        
        public bool UseOfs { get; set; }
        public bool UseFfs { get; set; }
        public bool UseIntl { get; set; }
        public bool UseDirCache { get; set; }
        public bool UseLnfs { get; set; }
        
        public uint BitmapSize { get; set; }
        public BitmapBlock[] BitmapTable { get; set; }
        public uint[] BitmapBlocks { get; set; }
        public bool[] BitmapBlocksChg { get; set; }

        /// <summary>
        /// Changed bitmap blocks are not written yet and root block bitmap flags are set invalid. Bitmap is
        /// updated, when volume is flushed or disposed
        /// </summary>
        public bool BitmapUpdatePending { get; set; }
        
        public bool IgnoreErrors { get; set; }
        public IList<string> Logs { get; set; }
        public int OffsetsPerBitmapBlock { get; set; }
        
        public uint NextFreeBlock { get; set; }

        /// <summary>
        /// Number of free blocks in bitmap, updated when blocks are set used or free
        /// </summary>
        public long FreeBlocks { get; set; }

        /// <summary>
        /// Length of largest run of free blocks found by last allocation, which didn't find a run large enough.
        /// -1, if unknown (blocks were freed since). Avoids searching the whole bitmap again for a large run
        /// </summary>
        public int MaxFreeRunLength { get; set; } = -1;
        public bool ResolveLinkPaths { get; set; }

        /// <summary>
        /// Write-back cache of metadata blocks
        /// </summary>
        public BlockCache BlockCache { get; }

        /// <summary>
        /// Index of entry names to entry block sectors per directory
        /// </summary>
        public DirectoryIndex DirectoryIndex { get; } = new DirectoryIndex();

        /// <summary>
        /// Number of metadata blocks cached
        /// </summary>
        public const int BlockCacheSize = 1024;

        /// <summary>
        /// Max number of dirty cached blocks before volume is flushed, when a file system operation completes
        /// </summary>
        public const int MaxDirtyBlocks = 512;

        /// <summary>
        /// Volume is being flushed
        /// </summary>
        public bool Flushing { get; set; }

        public Volume()
        {
            BlockCache = new BlockCache(BlockCacheSize);
            Logs = new List<string>();
            
            BitmapTable = Array.Empty<BitmapBlock>();
            BitmapBlocks = Array.Empty<uint>();
            BitmapBlocksChg = Array.Empty<bool>();
            ResolveLinkPaths = true;
        }
    }
}