namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Directory block view (dirblock_t in pfs3.h).
    /// </summary>
    public class DirBlock : StructView
    {
        /// <summary>
        /// offsetof(dirblock_t, entries)
        /// </summary>
        public const int EntriesOffset = 20;

        public DirBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// 'DB'
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

        /// <summary>
        /// anodenr belonging to this directory (points to FIRST block of dir)
        /// </summary>
        public uint AnodeNr
        {
            get => GetUInt32(12);
            set => SetUInt32(12, value);
        }

        /// <summary>
        /// parent
        /// </summary>
        public uint Parent
        {
            get => GetUInt32(16);
            set => SetUInt32(16, value);
        }

        /// <summary>
        /// First directory entry (FIRSTENTRY(blok)).
        /// </summary>
        public DirEntry FirstEntry => new DirEntry(Data, Offset + EntriesOffset);
    }
}
