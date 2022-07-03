namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;

    public class RootBlock : EntryBlock
    {
        public uint Offset { get; set; }
        
        public int BitmapFlags { get; set; }
        public int[] BitmapBlockOffsets { get; set; } // bmPages
        public uint BitmapBlocksOffset { get; set; }
        
        /// <summary>
        /// first bitmap extension block (when there's more than 25 bitmap blocks)
        /// </summary>
        public uint BitmapExtensionBlocksOffset { get; set; }
        
        public string DiskName { get; set; }
        public DateTime RootAlterationDate { get; set; }
        public DateTime DiskAlterationDate { get; set; }
        public DateTime FileSystemCreationDate { get; set; }
        
        public IEnumerable<BitmapBlock> BitmapBlocks { get; set; }
        public IEnumerable<BitmapExtensionBlock> BitmapExtensionBlocks { get; set; }

        public int[] bmPages => BitmapBlockOffsets;

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
            RootAlterationDate = now;
            DiskAlterationDate = now;
            FileSystemCreationDate = now;

            Extension = 0;
            SecType = Constants.ST_ROOT;
            
            BitmapBlocks = new List<BitmapBlock>();
            BitmapExtensionBlocks = new List<BitmapExtensionBlock>();

            //bmPages = new int[Constants.BM_SIZE];
        }
    }
}