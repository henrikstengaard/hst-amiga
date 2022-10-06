namespace Hst.Amiga.FileSystems.Pfs3
{
    public class extrafields
    {
        /*
     * struct extrafields
{
	ULONG link;				// link anodenr						
    UWORD uid;				// user id							
    UWORD gid;				// group id							
    ULONG prot;				// byte 1-3 of protection			
    // rollover fields
    ULONG virtualsize;		// virtual rollover filesize in bytes (as shown by Examine()) 
    ULONG rollpointer;		// current start of file AND end of file pointer 
    // extended file size
    UWORD fsizex;           // extended bits 32-47 of direntry.fsize 
};

     */

        public uint link;
        public ushort uid;
        public ushort gid;
        public uint prot;
        public uint virtualsize;
        public uint rollpointer;
        public ushort fsizex;
    }
}