namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Bitmap block view (bitmapblock_t in pfs3.h). Structure for both normal as reserved bitmap.
    /// </summary>
    public class BitmapBlock : StructView
    {
        public BitmapBlock(byte[] data, int offset = 0) : base(data, offset)
        {
        }

        /// <summary>
        /// 'BM' (bitmap block)
        /// </summary>
        public ushort Id
        {
            get => GetUInt16(0);
            set => SetUInt16(0, value);
        }

        public uint Datestamp
        {
            get => GetUInt32(4);
            set => SetUInt32(4, value);
        }

        public uint SeqNr
        {
            get => GetUInt32(8);
            set => SetUInt32(8, value);
        }

        /// <summary>
        /// Pointer to bitmap[i], the bitmap.
        /// </summary>
        public UInt32Pointer Bitmap(uint i) => PointerTo(12 + (int)i * 4);
    }
}
