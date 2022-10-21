namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    public class cachedblock
    {
        /*
typedef struct {
	uint32 blocknr;
	enum mode mode;
	bitmapblock_t *data;
} c_bitmapblock_t;
        */
        public uint blocknr;
        public int mode;
        public object data;
    }
}