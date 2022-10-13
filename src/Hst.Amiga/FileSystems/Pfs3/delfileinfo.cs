namespace Hst.Amiga.FileSystems.Pfs3
{
    public class delfileinfo
    {
        public uint special;					// 2
        public uint slotnr;					// het slotnr voor deze deldirentry

        public delfileinfo()
        {
            special = 3; // file by default
        }
    }
}