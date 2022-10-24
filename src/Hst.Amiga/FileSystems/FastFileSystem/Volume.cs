﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Blocks;

    public class Volume
    {
        public int BlockSize { get; set; }
        public Stream Stream { get; set; }
        public uint FirstBlock { get; set; }
        public uint LastBlock { get; set; }
        public uint Blocks { get; set; }
        public long PartitionStartOffset { get; set; }
        public uint Reserved { get; set; }
        public int DosType { get; set; }
        public uint DataBlockSize { get; set; }
        public uint RootBlockOffset { get; set; }
        public RootBlock RootBlock { get; set; }
        public EntryBlock CurrentDirectory { get; set; }
        public bool Mounted { get; set; }
        public bool ReadOnly { get; set; }
        
        public bool UsesDirCache { get; set; }
        
        public uint BitmapSize { get; set; }
        public BitmapBlock[] BitmapTable { get; set; }
        public uint[] BitmapBlocks { get; set; }
        public bool[] BitmapBlocksChg { get; set; }
        
        public bool IgnoreErrors { get; set; }
        public IList<string> Logs { get; set; }

        public Volume()
        {
            Logs = new List<string>();
            
            BitmapTable = Array.Empty<BitmapBlock>();
            BitmapBlocks = Array.Empty<uint>();
            BitmapBlocksChg = Array.Empty<bool>();
        }
    }
}