namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class FileHeaderBlock : EntryBlock
    {
        public FileHeaderBlock()
        {
            Type = Constants.T_HEADER;
            SecType = Constants.ST_FILE;
        }
    }
}