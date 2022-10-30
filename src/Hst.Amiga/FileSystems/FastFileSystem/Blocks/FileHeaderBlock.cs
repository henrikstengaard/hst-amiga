namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class FileHeaderBlock : EntryBlock
    {
        public override int Type => Constants.T_HEADER;
        public override int SecType => Constants.ST_FILE;

        public FileHeaderBlock(int fileSystemBlockSize) : base(fileSystemBlockSize)
        {
        }
    }
}