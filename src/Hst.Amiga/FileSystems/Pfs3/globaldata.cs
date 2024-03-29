﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using Blocks;

    public class globaldata
    {
        public RootBlock RootBlock;
        public DosEnvec DosEnvec { get; set; }
        public uint NumBuffers;
        public lru_data_s glob_lrudata;

        public bool IgnoreProtectionBits { get; set; }
        
        /* LRU stuff */
        public bool uip;                           /* update in progress flag              */
        public ushort locknr;                       /* prevents blocks from being flushed   */
        
        // // ULONG de_SizeBlock;	     /* in longwords: Physical disk block size */
        // public uint SizeBlock;
        
        /// <summary>
        /// Physical disk block size (512) 
        /// </summary>
        public uint blocksize;                    /* g->dosenvec->de_SizeBlock << 2       */
        public ushort blockshift;                   /* 2 log van block size                 */
        public ushort fnsize;						/* filename size (18+)					*/
        public int directsize;                   /* number of blocks after which direct  */

        public uint firstblock;/* first and last block of partition    */
        public uint lastblock;

        /* disktype: ID_PFS_DISK/NO_DISK_PRESENT/UNREADABLE_DISK
         * (only valid if currentvolume==NULL) 
         */
        public uint disktype;

        /* state of currentvolume (ID_WRITE_PROTECTED/VALIDATED/VALIDATING) */
        public uint diskstate;
        
        /* 1 if 'ACTION_WRITE_PROTECTED'     	*/
        public bool softprotect;
        
        public volumedata currentvolume;
        public bool dirty;
        public long protectkey;
        public bool harddiskmode;
        public bool anodesplitmode;
        public bool dirextension;
        public bool largefile;
        public bool deldirenabled;
        public anode_data_s glob_anodedata;
        public allocation_data_s glob_allocdata;
        
        // stream for data io
        public Stream stream;
        
        public ushort infoblockshift;
        public bool updateok;

        public globaldata()
        {
            glob_lrudata = new lru_data_s();
            glob_anodedata = new anode_data_s();
            glob_allocdata = new allocation_data_s();
            dc = new diskcache();
            SearchInDirCache = new Dictionary<uint, SearchInDirCacheItem>();
        }

        public uint TotalSectors { get; set; }
        public bool SuperMode { get; set; }

        public diskcache dc;                /* cache to make '196 byte mode' faster */


        public readonly IDictionary<uint, SearchInDirCacheItem> SearchInDirCache;
    }

    public class SearchInDirCacheItem
    {
        public readonly uint dirnodenr;
        public readonly CachedBlock DirBlock;
        public readonly IDictionary<string, direntry> DirEntriesCache;

        public SearchInDirCacheItem(uint dirnodenr, CachedBlock dirBlock)
        {
            this.dirnodenr = dirnodenr;
            DirBlock = dirBlock;
            DirEntriesCache = new Dictionary<string, direntry>();
        }
    }
}