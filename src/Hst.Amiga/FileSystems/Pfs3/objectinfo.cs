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
    }
}