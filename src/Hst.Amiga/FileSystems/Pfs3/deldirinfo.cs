﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    public class deldirinfo
    {
        public uint special;                  // 0 => volumeinfo; 1 => deldirinfo; 2 => delfile; >2 => fileinfo
        public volumedata volume;

        public deldirinfo()
        {
            special = 3; // file by default
        }
    }
}