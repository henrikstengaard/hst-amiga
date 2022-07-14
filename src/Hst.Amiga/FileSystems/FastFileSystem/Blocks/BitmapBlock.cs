namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Linq;

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
        
        public BitmapBlock() : this(512)
        {
        }
        
        public BitmapBlock(int blockSize)
        {
            Map = Enumerable.Range(1, (blockSize - SizeOf.ULong) / SizeOf.Long).Select(_ => uint.MaxValue).ToArray();
        }
    }
}