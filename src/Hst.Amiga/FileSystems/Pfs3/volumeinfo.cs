namespace Hst.Amiga.FileSystems.Pfs3
{
    public class volumeinfo
    {
        /*
struct volumeinfo
{
	ULONG   root;                   // 0 =>it's a volumeinfo; <>0 => it's a fileinfo
	struct volumedata *volume;
};
         */
        
        public uint root;                   // 0 =>it's a volumeinfo; <>0 => it's a fileinfo
        public volumedata volume;
    }
}