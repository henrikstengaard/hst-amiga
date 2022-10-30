namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    /// <summary>
    /// A bitmap block contain information about free and allocated blocks.
    /// One bit is used per block. If the bit is set, the block is free, a cleared bit means an allocated block.
    /// </summary>
    public class BitmapBlock : IBlock
    {
        public enum BlockState
        {
            Used,
            Free
        }
        
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        
        public int Checksum { get; set; }
        public uint[] Map { get; set; }
        
        public BitmapBlock(int fileSystemBlockSize)
        {
            Map = new uint[(fileSystemBlockSize - SizeOf.ULong) / SizeOf.Long];
            for (var i = 0; i < Map.Length; i++)
            {
                Map[i] = uint.MaxValue;
            }
        }
    }
}