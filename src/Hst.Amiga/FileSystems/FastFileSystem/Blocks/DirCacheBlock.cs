namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class DirCacheBlock : IBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }

        public int Type { get; }
        public uint HeaderKey { get; set; }
        public uint Parent { get; set; }
        public uint RecordsNb { get; set; }
        public uint NextDirC { get; set; }
        public int Checksum { get; set; }
        public byte[] Records { get; set; }

        public DirCacheBlock(int fileSystemBlockSize)
        {
            Type = Constants.T_DIRC;
            Records = new byte[fileSystemBlockSize - (SizeOf.ULong * 4) - (SizeOf.Long * 2)];
        }
    }
}