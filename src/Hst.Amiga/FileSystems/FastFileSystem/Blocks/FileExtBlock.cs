namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class FileExtBlock : IBlock, IHeaderBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }

        public int Type { get; }
        public uint HeaderKey { get; set; }
        public uint HighSeq { get; set; }
        public uint IndexSize { get; set; }
        public uint FirstData { get; set; }
        public int Checksum { get; set; }
        public uint[] Index { get; set; }
        public uint RealEntry { get; set; }
        public uint NextLink { get; set; }
        public uint Info { get; set; }
        public uint NextSameHash { get; set; }
        public uint Parent { get; set; }
        public uint Extension { get; set; }
        public int SecType { get; }

        public FileExtBlock(int fileSystemBlockSize)
        {
            var indexSize = FastFileSystemHelper.CalculateHashtableSize((uint)fileSystemBlockSize);
            Type = Constants.T_LIST;
            FirstData = 0;
            IndexSize = indexSize;
            Index = new uint[indexSize];
            Info = 0;
            NextSameHash = 0;
            SecType = Constants.ST_FILE;
        }
    }
}