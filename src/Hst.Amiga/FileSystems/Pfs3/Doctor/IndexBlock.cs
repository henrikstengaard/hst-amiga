namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Index block view (indexblock_t in pfs3.h).
    /// </summary>
    public class IndexBlock : StructView
    {
        public IndexBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// 'AI', 'BI', or 'SB'
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
        /// Pointer to index[i], the indices.
        /// </summary>
        public UInt32Pointer Index(uint i) => PointerTo(12 + (int)i * 4);
    }
}
