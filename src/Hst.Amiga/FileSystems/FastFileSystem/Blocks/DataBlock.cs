namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class DataBlock : IBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        
        public int Type { get; }
        public uint HeaderKey { get; set; }
        public uint SeqNum { get; set; }
        public uint DataSize { get; set; }
        public uint NextData { get; set; }
        public int Checksum { get; set; }
        public byte[] Data { get; set; }

        public DataBlock()
        {
            Type = Constants.T_DATA;
        }
    }
}