namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using System.Collections.Generic;

    public class RootBlock : EntryBlock
    {
        public int BitmapFlags { get; set; }
        public int[] BitmapBlockOffsets { get; set; } // bmPages
        public uint BitmapBlocksOffset { get; set; }
        
        /// <summary>
        /// first bitmap extension block (when there's more than 25 bitmap blocks)
        /// </summary>
        public uint BitmapExtensionBlocksOffset { get; set; }

        public string DiskName => Name;
        public DateTime RootAlterationDate => Date;
        public DateTime DiskAlterationDate { get; set; }
        public DateTime FileSystemCreationDate { get; set; }
        
        public IEnumerable<BitmapBlock> BitmapBlocks { get; set; }
        public IEnumerable<BitmapExtensionBlock> BitmapExtensionBlocks { get; set; }

        public RootBlock()
        {
            Type = Constants.T_HEADER;
            HeaderKey = 0;
            HighSeq = 0;
            HashTableSize = Constants.HT_SIZE;
            FirstData = 0;
            Checksum = 0;
            HashTable = new int[Constants.HT_SIZE];
            
            BitmapFlags = -1;

            var now = DateTime.UtcNow;
            Date = now;
            DiskAlterationDate = now;
            FileSystemCreationDate = now;

            Extension = 0;
            SecType = Constants.ST_ROOT;

            BitmapBlockOffsets = Array.Empty<int>();
            BitmapBlocks = new List<BitmapBlock>();
            BitmapExtensionBlocks = new List<BitmapExtensionBlock>();
        }
    }
}