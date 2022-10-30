namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class DirBlock : EntryBlock
    {
        public override int Type => Constants.T_HEADER;
        public override int SecType => Constants.ST_DIR;

        public DirBlock(int fileSystemBlockSize) : base(fileSystemBlockSize)
        {
            ByteSize = 0;
        }
    }
}