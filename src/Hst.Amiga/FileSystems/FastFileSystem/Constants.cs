﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public static class Constants
    {
        public const int BitmapsPerULong = 8 * SizeOf.ULong;
        public const int MaxBitmapBlockPointersInRootBlock = 25;
        
/* ----- FILE SYSTEM ----- */

        public const int FSMASK_FFS = 1;
        public const int FSMASK_INTL = 2;
        public const int FSMASK_DIRCACHE = 4;


/* ----- ENTRIES ----- */

/* access constants */

        public const int ACCMASK_D = 1 << 0;
        public const int ACCMASK_E = 1 << 1;
        public const int ACCMASK_W = 1 << 2;
        public const int ACCMASK_R = 1 << 3;
        public const int ACCMASK_A = 1 << 4;
        public const int ACCMASK_P = 1 << 5;
        public const int ACCMASK_S = 1 << 6;
        public const int ACCMASK_H = 1 << 7;

        /* block constants */

        public const uint BM_VALID = uint.MaxValue; //-1;
        public const uint BM_INVALID = 0;

        //public const int INDEX_SIZE = 72;
        //public const int HT_SIZE = INDEX_SIZE;
        public const int BM_SIZE = 25;
        //public const int MAX_DATABLK = INDEX_SIZE;

        public const int MAXNAMELEN = 30;
        public const int LNFSMAXNAMELEN = 107;
        public const int MAXCMMTLEN = 79;
        public const int LNFSNAMECMMTLEN = 112; /* Merged name and comment */

        /* block primary and secondary types */

        public const int T_HEADER = 2;
        public const int ST_ROOT = 1;
        public const int ST_DIR = 2;
        public const int ST_FILE = -3;
        public const int ST_LFILE = -4;
        public const int ST_LDIR = 4;
        public const int ST_LSOFT = 3;
        public const int T_LIST = 16;
        public const int T_DATA = 8;
        public const int T_DIRC = 33;
        public const int TYPE_COMMENT = 64;
    }
}