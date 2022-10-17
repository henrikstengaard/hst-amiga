namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;
    using System.IO;

    public class Volume
    {
        public uint firstblock { get; set; } /* abs blocknr, first and last block */
        public uint lastblock { get; set; }
        public uint disksize { get; set; } /* disksize in blocks */
        public uint lastreserved { get; set; } /* rel blocknr, last reserved block */
        public uint blocksize { get; set; } /* physical blocksize in bytes */
        public short blockshift { get; set; }
        public uint rescluster { get; set; }

        public string diskname { get; set; }
        public int fnsize { get; set; }

        /* flags */
        public bool repartitioned;
        public int accessmode;
        public bool td64mode, nsdmode;
        public int standardscan; /* 0=not done/needed, 1=fixed, -1=not fixable */

        /* bitmaps */
        public bitmap mainbitmap;
        public bitmap anodebitmap;
        public bitmap resbitmap;

        /* full scan */
        public LinkedList<cachedblock> buildblocks; /* elements are of type buildblock */

        public long PartitionOffset;
        public Stream Stream;
        public cache cache;
    }
}