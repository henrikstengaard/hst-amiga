namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class DirBlock : EntryBlock
    {
        public DirBlock()
        {
            Type = Constants.T_HEADER;
            SecType = Constants.ST_DIR;
        }
    }
}