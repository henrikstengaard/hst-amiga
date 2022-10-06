namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Threading.Tasks;

    public static class FileSystem
    {
        public static async Task CreateDirectory(string name)
        {
            // lockentry_t *parentle, *newdirle;
            // union objectinfo path, *parentfi;
            // UBYTE *dirname, pathname[PATHSIZE];
            // UBYTE *zonderpad;
            //
            // GetFileInfoFromLock(pkt->dp_Arg1, 1, parentle, parentfi);
            // BCPLtoCString(pathname, (DSTR)BARG2(pkt));
            // SkipColon(dirname, pathname);
            // zonderpad = GetFullPath(parentfi, dirname, &path, error, g);
            // if (!zonderpad)
            //     return DOSFALSE;
            //
            //
            // newdirle = NewDir(&path, zonderpad, error, g);
            //
            // if (newdirle)
            // {
            //     PFSDoNotify(&newdirle->le.info.file, TRUE, g);
            //     return MKBADDR(&newdirle->le.lock);
            // }
            // else
            //     return DOSFALSE;
            //
        }
    }
}