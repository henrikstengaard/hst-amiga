namespace Hst.Amiga.FileSystems.FastFileSystem.Extensions
{
    public static class EntryExtensions
    {
        public static bool IsRoot(this Entry entry) => entry.Type == Constants.ST_ROOT;

        public static bool IsFile(this Entry entry) => entry.Type == Constants.ST_FILE;

        public static bool IsDirectory(this Entry entry) => entry.Type == Constants.ST_DIR;
    }
}