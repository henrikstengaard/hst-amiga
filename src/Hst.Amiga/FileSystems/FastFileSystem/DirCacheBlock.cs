﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class DirCacheBlock
    {
        public byte[] BlockBytes { get; set; }

        public int Type { get; }
        public int HeaderKey { get; set; }
        public int Parent { get; set; }
        public int RecordsNb { get; set; }
        public int NextDirC { get; set; }
        public int Checksum { get; set; }
        public byte[] Records { get; set; }

        public DirCacheBlock()
        {
            Type = Constants.T_DIRC;
            Records = new byte[488];
        }
    }
}