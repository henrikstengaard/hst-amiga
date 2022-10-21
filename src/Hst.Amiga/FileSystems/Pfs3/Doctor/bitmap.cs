namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    public class bitmap
    {
        /*
typedef struct 
{
	bool	valid;		// fix only possible if valid
        uint32	errorsfound;
        uint32	errorsfixed;
        uint32	start;
        uint32	stop;
        uint32	step;
        uint32	lwsize;		// size in longwords

        uint32	*map;
    } bitmap_t;
         */
        public bool	valid;		// fix only possible if valid
        public uint	errorsfound;
        public uint	errorsfixed;
        public uint	start;
        public uint	stop;
        public uint	step;
        public uint	lwsize;		// size in longwords

        public uint[]	map;
    }
}