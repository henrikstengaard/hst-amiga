namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using Blocks;

    public class lru_data_s
    {
        /* the LRU global data */
        // struct lru_data_s
        // {
        //     struct MinList LRUqueue;
        //     struct MinList LRUpool;
        //     ULONG poolsize;
        //     struct lru_cachedblock **LRUarray;
        //     UWORD reserved_blksize;
        // };
        
        public LinkedList<LruCachedBlock> LRUqueue;
        public LinkedList<LruCachedBlock> LRUpool;

        /// <summary>
        /// pfs3 uses lru array together num buffers for partition
        /// and increases it, if lru pool is empty, then it adds
        /// 5 new entries to lru array which is added lru pool.
        /// it means that lru array only seems to be used as a way to allocate
        /// new lru cached blocks in Lru Alloc.Lru method.
        /// therefore use lry array is disabled by default as it will
        /// expand continuously up over time with new entries added
        /// every time lru pool is empty.
        /// </summary>
        public bool useLruArray { get; set; }
        public uint poolsize;
        public LruCachedBlock[] LRUarray;
        
        public ushort reservedBlksize;

        public lru_data_s()
        {
            useLruArray = false;
        }
    };
}