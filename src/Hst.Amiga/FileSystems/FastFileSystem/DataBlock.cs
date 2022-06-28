namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class DataBlock : IDataBlock
    {
        public byte[] BlockBytes { get; set; }
        public byte[] Data { get; set; }
    }
}