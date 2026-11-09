namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Bitmap block (struct fsBitmap) with one bit per block, most significant bit first. Set bits are free blocks.
    /// </summary>
    public class BitmapBlock : Block
    {
        public uint[] Map { get; set; }

        public BitmapBlock(int blockSize) : base(Constants.BitmapId)
        {
            Map = new uint[(blockSize - Constants.BitmapHeaderSize) / 4];
        }

        /// <summary>
        /// Number of blocks covered by a bitmap block.
        /// </summary>
        public static int BlocksInBitmap(int blockSize) => (blockSize - Constants.BitmapHeaderSize) * 8;
    }
}
