namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;

    public class BitmapExtensionBlock : IBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        
        public uint[] BitmapBlockOffsets { get; set; }
        public uint NextBitmapExtensionBlockPointer { get; set; }
        public IEnumerable<BitmapBlock> BitmapBlocks { get; set; }
    }
}