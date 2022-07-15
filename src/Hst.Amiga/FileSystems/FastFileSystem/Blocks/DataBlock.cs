namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class DataBlock : IBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        
        public int Type { get; }
        public int HeaderKey { get; set; }
        public int SeqNum { get; set; }
        public int DataSize { get; set; }
        public int NextData { get; set; }
        public int Checksum { get; set; }
        public byte[] Data { get; set; }

        public DataBlock()
        {
            Type = Constants.T_DATA;
        }
    }
}