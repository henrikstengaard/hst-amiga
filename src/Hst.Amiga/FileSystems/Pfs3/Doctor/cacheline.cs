namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    public class cacheline
    {
        // struct cacheline
        // {
        //     struct cacheline *next;
        //     struct cacheline *prev;
        //     uint32 blocknr;			/* 1 == unused */
        //     bool dirty;
        //     uint8 *data;
        // };
        
        // struct cacheline *next;
        // struct cacheline *prev;
        public uint blocknr;			/* 1 == unused */
        public bool dirty;
        public byte[] data;
        // };
    }
}