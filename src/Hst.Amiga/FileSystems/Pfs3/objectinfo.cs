namespace Hst.Amiga.FileSystems.Pfs3
{
    public class objectinfo
    {
        /*
union objectinfo
{
	struct fileinfo file;
	struct volumeinfo volume;
#if DELDIR
	struct deldirinfo deldir;
	struct delfileinfo delfile;
#endif
};
         */
        
        public fileinfo file;
        public volumeinfo volume;
        
        // deldir
        public deldirinfo deldir;
        public delfileinfo delfile;

        public objectinfo()
        {
	        file = new fileinfo();
	        volume = new volumeinfo();
	        deldir = new deldirinfo();
	        delfile = new delfileinfo();
        }

        public objectinfo Clone()
        {
	        return new objectinfo
	        {
		        file = new fileinfo
		        {
			        dirblock = file.dirblock,
			        direntry = file.direntry
		        },
		        volume = new volumeinfo
		        {
			        root = volume.root,
			        volume = volume.volume
		        },
		        deldir = new deldirinfo
		        {
			        special = deldir.special,
			        volume = deldir.volume
		        },
		        delfile = new delfileinfo
		        {
			        slotnr = delfile.slotnr,
			        special = delfile.special
		        }
	        };
        }
    }
}