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
        
        /// <summary>
        /// Is root. 0 = root, 1 = not root
        /// pfs3aio: 0 =>it's a volumeinfo; <>0 => it's a fileinfo
        /// </summary>
        public uint root;
        public volumedata volume;
    }
}