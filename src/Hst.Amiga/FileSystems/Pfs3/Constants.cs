namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Linq;

    public static class Constants
    {
            public const uint MAX_FILE_SIZE = 0xffffffff;
        public const uint ULONG_MAX = UInt32.MaxValue;

        public static string deldirname = new string(new[] { (char)7 }.Concat(".DELDIR".ToArray()).ToArray());

        public const int OFFSET_BEGINNING = -1; /* relative to Begining Of File */
        public const int OFFSET_CURRENT = 0; /* relative to Current file position */
        public const int OFFSET_END = 1; /* relative to End Of File	  */

/* disk options */
        public const int MODE_HARDDISK = 1;
        public const int MODE_SPLITTED_ANODES = 2;
        public const int MODE_DIR_EXTENSION = 4;
        public const int MODE_DELDIR = 8;

        public const int MODE_SIZEFIELD = 16;

// rootblock extension
        public const int MODE_EXTENSION = 32;

// if enabled the datestamp was on at format time (!)
        public const int MODE_DATESTAMP = 64;
        public const int MODE_SUPERINDEX = 128;
        public const int MODE_SUPERDELDIR = 256;
        public const int MODE_EXTROVING = 512;
        public const int MODE_LONGFN = 1024;
        public const int MODE_LARGEFILE = 2048;

        public const int FIBB_HELDRESIDENT = 7; /* program is a script (execute) file */
        public const int FIBB_SCRIPT = 6; /* program is a script (execute) file */
        public const int FIBB_PURE = 5; /* program is reentrant and rexecutable */
        public const int FIBB_ARCHIVE = 4; /* cleared whenever file is changed */
        public const int FIBB_READ = 3; /* ignored by old filesystem */
        public const int FIBB_WRITE = 2; /* ignored by old filesystem */
        public const int FIBB_EXECUTE = 1; /* ignored by system, used by Shell */
        public const int FIBB_DELETE = 0; /* prevent file from being deleted */

        public const int FIBF_HELDRESIDENT = 1 << FIBB_HELDRESIDENT;
        public const int FIBF_SCRIPT = 1 << FIBB_SCRIPT;
        public const int FIBF_PURE = 1 << FIBB_PURE;
        public const int FIBF_ARCHIVE = 1 << FIBB_ARCHIVE;
        public const int FIBF_READ = 1 << FIBB_READ;
        public const int FIBF_WRITE = 1 << FIBB_WRITE;
        public const int FIBF_EXECUTE = 1 << FIBB_EXECUTE;
        public const int FIBF_DELETE = 1 << FIBB_DELETE;

        // https://wiki.amigaos.net/wiki/Migration_Guide
        // -------
        // uint8 = UBYTE,	8 bit unsigned integer
        // uint32 = ULONG, 32 bit unsigned integer
        // uint16 =	UWORD, 16 bit unsigned integer

        /*
// Cached blocks in general

        struct cachedblock
        {
            struct cachedblock	*next;
            struct cachedblock	*prev;
            struct volumedata	*volume;
            ULONG	blocknr;				// overeenkomstig diskblocknr
            ULONG	oldblocknr;				// blocknr before reallocation. NULL if not reallocated.
            UWORD	used;					// block locked if used == g->locknr
            UBYTE	changeflag;				// dirtyflag
            UBYTE	dummy;					// pad to make offset even
            UBYTE	data[0];				// the datablock;
        };
*/

/* size of reserved blocks in bytes and blocks
 * place you can find rootblock
 */
        //public const long SIZEOF_RESBLOCK = 1024;
        /* size of reserved blocks in bytes and blocks
 * place you can find rootblock
 */
        public static int SIZEOF_RESBLOCK(globaldata g) => g.RootBlock.ReservedBlksize;
        //public const long SIZEOF_CACHEDBLOCK = (sizeof(struct cachedblock) + SIZEOF_RESBLOCK);
        //public const long SIZEOF_LRUBLOCK = (sizeof(struct lru_cachedblock) + SIZEOF_RESBLOCK);
        //public const long RESCLUSTER (g->currentvolume->rescluster)

/* info id's: delfile, deldir and flushed reference */
        public const byte SPECIAL_DELDIR = 1;
        public const byte SPECIAL_DELFILE = 2;
        public const byte SPECIAL_FLUSHED = 3;

        public const byte VERNUM = 19;
        public const byte REVNUM = 2;


        public static uint RESCLUSTER(globaldata g) => g.currentvolume.rescluster;

        public const int CL_UNUSED = 1;
        public const uint BOOTBLOCK1 = 0;
        public const uint BOOTBLOCK2 = 1;
        public const uint ROOTBLOCK = 2;

/* number of reserved anodes per anodeblock */
        public const int RESERVEDANODES = 6;

        public const bool LARGE_FILE_SIZE = false;
        public const int DELENTRYFNSIZE = 18;

        /* limits */
        public const int MAXSMALLBITMAPINDEX = 4;

        public const int MAXBITMAPINDEX = 103;

        // was 28576. was 119837. Nu max reserved bitmap 256K.
        public const int MAXNUMRESERVED = 4096 + 255 * 1024 * 8;
        public const int MAXSUPER = 15;
        public const int MAXSMALLINDEXNR = 98;

        /* maximum disksize in sectors, limited by number of bitmapindexblocks
        * smalldisk = 10.241.440 blocks of 512 byte = 5G
        * normaldisk = 213.021.952 blocks of 512 byte = 104G
        * 2k reserved blocks = 104*509*509*32 blocks of 512 byte = 411G
        * 4k reserved blocks = 1,6T
        *  */
        public const short BITMAP_PAYLOAD_1K = 1024 / 4 - 3; // 253
        public const short BITMAP_PAYLOAD_2K = 2048 / 4 - 3; // 509
        public const short BITMAP_PAYLOAD_4K = 4096 / 4 - 3; // 1021

        public const long MAXSMALLDISK = (MAXSMALLBITMAPINDEX + 1) * BITMAP_PAYLOAD_1K * BITMAP_PAYLOAD_1K * 32;
        public const long MAXDISKSIZE1K = (MAXBITMAPINDEX + 1) * BITMAP_PAYLOAD_1K * BITMAP_PAYLOAD_1K * 32;
        public const long MAXDISKSIZE2K = (MAXBITMAPINDEX + 1) * BITMAP_PAYLOAD_2K * BITMAP_PAYLOAD_2K * 32;
        public const long MAXDISKSIZE4K = ((long)MAXBITMAPINDEX + 1) * BITMAP_PAYLOAD_4K * BITMAP_PAYLOAD_4K * 32;
        public const long MAXDISKSIZE = MAXDISKSIZE4K;


        /* max length of filename, diskname and comment
 * FNSIZE is 108 for compatibilty. Used for searching
 * files.
 */
        public const int FNSIZE = 108;

        public const int PATHSIZE = 256;

        //public const int  FILENAMESIZE (g->fnsize)
        public const int DNSIZE = 32;

        public const int CMSIZE = 80;
        //public const int  MAX_ENTRYSIZE (sizeof(struct direntry) + FNSIZE + CMSIZE + 34)

/* disk id 'PFS\1'  */
//#ifdef BETAVERSION
//#define ID_PFS_DISK		(0x42455441L)	/*	'BETA'	*/
//#else
        public const int ID_PFS_DISK = 0x50465301; /*  'PFS\1' */

//#endif
        public const int ID_BUSY = 0x42555359; /*	'BUSY'  */

        public const int ID_MUAF_DISK = 0x6d754146; /*	'muAF'	*/
        public const int ID_MUPFS_DISK = 0x6d755046; /*	'muPF'	*/
        public const int ID_AFS_DISK = 0x41465301; /*	'AFS\1' */
        public const int ID_PFS2_DISK = 0x50465302; /*	'PFS\2'	*/
        public const int ID_AFS_USER_TEST = 0x41465355; /*	'AFSU'	*/

        /// <summary>
        /// dir block id (DB)
        /// </summary>
        public const ushort DBLKID = 0x4442;

        /// <summary>
        /// anode block id (AB)
        /// </summary>
        public const ushort ABLKID = 0x4142;

        /// <summary>
        /// index block id (IB)
        /// </summary>
        public const ushort IBLKID = 0x4942;

        /// <summary>
        /// (BM)
        /// </summary>
        public const ushort BMBLKID = 0x424D;
        
        /// <summary>
        /// (MI)
        /// </summary>
        public const ushort BMIBLKID = 0x4D49;

        /// <summary>
        /// deldir block id (DD)
        /// </summary>
        public const ushort DELDIRID = 0x4444;

        public const ushort EXTENSIONID = 0x4558; // EX
        public const ushort SBLKID = 0x5342; // 'SB

        /* ID stands for InfoData Disk states */
        public const int ID_WRITE_PROTECTED = 80; /* Disk is write protected */
        public const int ID_VALIDATING = 81; /* Disk is currently being validated */
        public const int ID_VALIDATED = 82; /* Disk is consistent and writeable */

        // * ID stands for InfoData
        // *	     Disk states
        // ID_WRITE_PROTECTED	EQU	80	* Disk is write protected
        // ID_VALIDATING		EQU	81	* Disk is currently being validated
        // ID_VALIDATED		EQU	82	* Disk is consistent and writeable

        //	   Disk types
        // ID_INTER_* use international case comparison routines for hashing
        public const uint ID_NO_DISK_PRESENT = uint.MaxValue; // -1;
        public const uint ID_UNREADABLE_DISK = 'B' << 24 | 'A' << 16 | 'D' << 8;
        public const uint ID_NOT_REALLY_DOS = 'N' << 24 | 'D' << 16 | 'O' << 8 | 'S';
        public const uint ID_DOS_DISK = 'D' << 24 | 'O' << 16 | 'S' << 8;
        public const uint ID_FFS_DISK = 'D' << 24 | 'O' << 16 | 'S' << 8 | 1;
        public const uint ID_INTER_DOS_DISK = 'D' << 24 | 'O' << 16 | 'S' << 8 | 2;
        public const uint ID_INTER_FFS_DISK = 'D' << 24 | 'O' << 16 | 'S' << 8 | 3;
        public const uint ID_KICKSTART_DISK = 'K' << 24 | 'I' << 16 | 'C' << 8 | 'K';
        public const uint ID_MSDOS_DISK = 'M' << 24 | 'S' << 16 | 'D' << 8;

        /* Cache hashing table mask values for dir and anode */
        public const int HASHM_DIR = 0x1f;
        public const int HASHM_ANODE = 0x7;

        /* predefined anodes */
        public const int ANODE_EOF = 0;
        public const int ANODE_RESERVED_1 = 1; // not used by MODE_BIG
        public const int ANODE_RESERVED_2 = 2; // not used by MODE_BIG
        public const int ANODE_RESERVED_3 = 3; // not used by MODE_BIG
        public const int ANODE_BADBLOCKS = 4; // not used yet
        public const int ANODE_ROOTDIR = 5;
        public const int ANODE_USERFIRST = 6;

        public const char DELENTRY_SEP = '@';
        public const int DELENTRY_PROT = 0x0005;
        public const int DELENTRY_PROT_AND_MASK = 0xaa0f;
        public const int DELENTRY_PROT_OR_MASK = 0x0005;

        public const int ST_ROLLOVERFILE = -16;

        /* maximum number of entries per block, max deldirblock seqnr */
        public const int DELENTRIES_PER_BLOCK = 31;
        public const int MAXDELDIR = 31;

        public const int DATACACHELEN = 32;
        public const int DATACACHEMASK = DATACACHELEN - 1;

/* cache grootte */
        public const int RTBF_CACHE_SIZE = 512;
        public const int TBF_CACHE_SIZE = 256;

/* update thresholds */
        public const int RTBF_THRESHOLD = 256;
        public const int RTBF_CHECK_TH = 128;
        public const int RTBF_POSTPONED_TH = 48;
        public const int TBF_THRESHOLD = 252;
        public const int RESFREE_THRESHOLD = 10;

/* indices in tobefreed array */
        public const int TBF_BLOCKNR = 0;
        public const int TBF_SIZE = 1;

/* buffer for AllocReservedBlockSave */
        public const int RESERVED_BUFFER = 10;

        /* postponed operations operation_id's */
        public const int PP_FREEBLOCKS_FREE = 1;
        public const int PP_FREEBLOCKS_KEEP = 2;
        public const int PP_FREEANODECHAIN = 3;

/* entrytype's
** NB: ETF_VOLUME en ETF_LOCK zijn ALLEBIJ LOCKS!! TEST ON BOTH
*/
        public const int ET_VOLUME = 0x0004;
        public const int ET_FILEENTRY = 0x0008;
        public const int ET_LOCK = 0x000c;
        public const int ETF_VOLUME = 1;
        public const int ETF_FILEENTRY = 2;
        public const int ETF_LOCK = 3;
        public const int ET_SHAREDREAD = 0;
        public const int ET_SHAREDWRITE = 1;
        public const int ET_EXCLREAD = 2;
        public const int ET_EXCLWRITE = 3;

        // public static bool IsVolumeEntry(listentry e) => e.type.type = ListType.ListTypeType.Volume=;//onstants.ETF_VOLUME;
        //     public static bool IsFileEntry(listentry e) => e.type.flags.type == Constants.ETF_FILEENTRY;
        //     public static bool IsLockEntry(listentry e) => e.type.flags.type == Constants.ETF_LOCK;
        //     public static bool IsVolumeLock(listentry le) => le.type.flags.type == Constants.ETF_VOLUME;

/* Types for fib_DirEntryType.	NOTE that both USERDIR and ROOT are	 */
/* directories, and that directory/file checks should use <0 and >=0.	 */
/* This is not necessarily exhaustive!	Some handlers may use other	 */
/* values as needed, though <0 and >=0 should remain as supported as	 */
/* possible.								 */
        public const int ST_ROOT = 1;
        public const int ST_USERDIR = 2;
        public const int ST_SOFTLINK = 3; //	/* looks like dir, but may point to a file! */
        public const int ST_LINKDIR = 4; //	/* hard link to dir */
        public const int ST_FILE = -3; //	/* must be negative for FIB! */
        public const int ST_LINKFILE = -4; //	/* hard link to file */
        public const int ST_PIPEFILE = -5; //	/* for pipes that support ExamineFH */        
    }
}