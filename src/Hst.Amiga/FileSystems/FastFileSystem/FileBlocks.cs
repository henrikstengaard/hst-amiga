namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class FileBlocks
    {
        public uint header;
        public uint nbExtens;
        public uint[] extens;
        public uint nbData;
        public uint[] data;
    }
}