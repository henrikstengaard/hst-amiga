namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System.Collections.Generic;

    public class dirblock : IBlock
    {
        // struct dirblock 
        // {
        // 0    UWORD id;               /* 'DB'                             */
        // 2    UWORD not_used;
        // 4    ULONG datestamp;
        // 8    UWORD not_used_2[2];
        // 12    ULONG anodenr;          /* anodenr belonging to this directory (points to FIRST block of dir) */
        // 16    ULONG parent;           /* parent                           */
        // 20    UBYTE entries[0];       /* entries                          */
        // };        
        
        public byte[] BlockBytes { get; set; }
        
        public ushort id { get; set; }
        public ushort not_used_1 { get; set; }
        public uint datestamp { get; set; }
        public uint anodenr { get; set; }
        public uint parent { get; set; }
        //public byte[] entries { get; set; }
        public IList<direntry> DirEntries { get; set; }

        public dirblock(globaldata g)
        {
            id = Constants.DBLKID;
            //entries = new byte[SizeOf.DirBlock.Entries(g)];
            DirEntries = new List<direntry>();
        }
    }
}