namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    public class indexblock : ISeqBlock
    {
        // typedef struct indexblock
        // {
        //     UWORD id;               /* AI or BI (anode- bitmap index)   */
        //     UWORD not_used;
        //     ULONG datestamp;
        //     ULONG seqnr;
        //     LONG index[0];          /* the indices                      */
        // } indexblock_t;        

        public byte[] BlockBytes { get; set; }

        public ushort id { get; set; }
        public ushort not_used_1 { get; set; }
        public uint datestamp { get; set; }
        public uint seqnr { get; set; }
        public int[] index; /* the indices                      */

        public indexblock(globaldata g)
        {
            index = new int[(g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) /
                            Amiga.SizeOf.Long];
        }
    }
}