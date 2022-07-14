namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class FileExtBlock : IBlock, IHeaderBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }

        public int Type { get; }
        public int HeaderKey { get; set; }
        public int HighSeq { get; set; }
        public int IndexSize { get; set; }
        public int FirstData { get; set; }
        public int Checksum { get; set; }
        public int[] Index { get; set; }
        public int RealEntry { get; set; }
        public int NextLink { get; set; }
        public int Info { get; set; }
        public int NextSameHash { get; set; }
        public int Parent { get; set; }
        public int Extension { get; set; }
        public int SecType { get; }

        public FileExtBlock()
        {
            Type = Constants.T_LIST;
            IndexSize = 0;
            FirstData = 0;
            Index = new int[Constants.MAX_DATABLK];
            Info = 0;
            NextSameHash = 0;
            SecType = Constants.ST_FILE;
        }
    }
}