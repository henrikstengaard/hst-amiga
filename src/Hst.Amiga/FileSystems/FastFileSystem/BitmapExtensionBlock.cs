namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;

    public class BitmapExtensionBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        public uint[] BitmapBlockOffsets { get; set; }
        public uint NextBitmapExtensionBlockPointer { get; set; }
        public IEnumerable<BitmapBlock> BitmapBlocks { get; set; }
    }
}