namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class FileBlocks
    {
        public int header;
        public int nbExtens;
        public int[] extens;
        public int nbData;
        public int[] data;
    }
}