namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    public class BitmapBlock : ISeqBlock
    {
/* structure for both normal and reserved bitmap
 * normal: normal clustersize
 * reserved: directly behind rootblock. As long as necessary
 */
        // typedef struct bitmapblock
        // {
        //     UWORD id;               /* BM (bitmap block)                */
        //     UWORD not_used;
        //     ULONG datestamp;
        //     ULONG seqnr;
        //     ULONG bitmap[0];        /* the bitmap.                      */
        // } bitmapblock_t;

        public byte[] BlockBytes { get; set; }

        public ushort id { get; set; }
        public ushort not_used_1 { get; set; }
        public uint datestamp { get; set; }
        public uint seqnr { get; set; }
        public uint[] bitmap; /* the bitmap.                      */
        
        public BitmapBlock(long longsperbmb)
        {
            id = Constants.BMBLKID; /* BM (bitmap block)                */
            seqnr = 0;

            bitmap = new uint[longsperbmb];
            for (var i = 0; i < longsperbmb; i++)
            {
                bitmap[i] = 0xFFFFFFFF;
            }
        }
    }
}