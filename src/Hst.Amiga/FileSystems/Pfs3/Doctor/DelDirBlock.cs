namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Delete directory block view (deldirblock_t in pfs3.h).
    /// </summary>
    public class DelDirBlock : StructView
    {
        public DelDirBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// 'DD'
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
        /// user id
        /// </summary>
        public ushort Uid
        {
            get => GetUInt16(18);
            set => SetUInt16(18, value);
        }

        /// <summary>
        /// group id
        /// </summary>
        public ushort Gid
        {
            get => GetUInt16(20);
            set => SetUInt16(20, value);
        }

        public uint Protection
        {
            get => GetUInt32(22);
            set => SetUInt32(22, value);
        }

        /// <summary>
        /// entries[i], 31 entries
        /// </summary>
        public DelDirEntry Entries(int i) => new DelDirEntry(Data, Offset + 32 + i * DelDirEntry.SizeOf);
    }

    /// <summary>
    /// Delete directory entry view (struct deldirentry in pfs3.h).
    /// </summary>
    public class DelDirEntry : StructView
    {
        /// <summary>
        /// sizeof(struct deldirentry)
        /// </summary>
        public const int SizeOf = 32;

        public DelDirEntry(byte[] data, int offset) : base(data, offset)
        {
        }

        /// <summary>
        /// anodenr
        /// </summary>
        public uint AnodeNr
        {
            get => GetUInt32(0);
            set => SetUInt32(0, value);
        }

        /// <summary>
        /// size of file
        /// </summary>
        public uint FSize
        {
            get => GetUInt32(4);
            set => SetUInt32(4, value);
        }

        /// <summary>
        /// filename[0], length of filename
        /// </summary>
        public byte FilenameLength
        {
            get => GetByte(14);
            set => SetByte(14, value);
        }

        /// <summary>
        /// extended bits 32-47 of fsize
        /// </summary>
        public ushort FSizeX
        {
            get => GetUInt16(30);
            set => SetUInt16(30, value);
        }
    }
}
