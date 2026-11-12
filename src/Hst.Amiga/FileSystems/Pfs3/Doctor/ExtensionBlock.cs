namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Root block extension view (extensionblock_t in pfs3.h).
    /// </summary>
    public class ExtensionBlock : StructView
    {
        public ExtensionBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// id ('EX')
        /// </summary>
        public ushort Id
        {
            get => GetUInt16(0);
            set => SetUInt16(0, value);
        }

        public uint ExtOptions
        {
            get => GetUInt32(4);
            set => SetUInt32(4, value);
        }

        public uint Datestamp
        {
            get => GetUInt32(8);
            set => SetUInt32(8, value);
        }

        /// <summary>
        /// pfs2 format revision
        /// </summary>
        public uint Pfs2Version
        {
            get => GetUInt32(12);
            set => SetUInt32(12, value);
        }

        /// <summary>
        /// root directory datestamp (root_date[i], 3 words)
        /// </summary>
        public ushort GetRootDate(int i) => GetUInt16(16 + i * 2);

        public void SetRootDate(int i, ushort value) => SetUInt16(16 + i * 2, value);

        /// <summary>
        /// volume datestamp (volume_date[i], 3 words)
        /// </summary>
        public ushort GetVolumeDate(int i) => GetUInt16(22 + i * 2);

        public void SetVolumeDate(int i, ushort value) => SetUInt16(22 + i * 2, value);

        /// <summary>
        /// Pointer to postponed_op[i], postponed operation (private)
        /// </summary>
        public UInt32Pointer PostponedOp(int i) => PointerTo(28 + i * 4);

        /// <summary>
        /// reserved roving pointer
        /// </summary>
        public uint ReservedRoving
        {
            get => GetUInt32(44);
            set => SetUInt32(44, value);
        }

        /// <summary>
        /// bitnr in rootblock->roving_ptr field
        /// </summary>
        public ushort RovingBit
        {
            get => GetUInt16(48);
            set => SetUInt16(48, value);
        }

        /// <summary>
        /// anode allocation roving pointer
        /// </summary>
        public ushort CurrAnSeqNr
        {
            get => GetUInt16(50);
            set => SetUInt16(50, value);
        }

        /// <summary>
        /// (5.1) deldir roving pointer
        /// </summary>
        public ushort DelDirRoving
        {
            get => GetUInt16(52);
            set => SetUInt16(52, value);
        }

        /// <summary>
        /// (5.1) size of deldir in blocks, 0 is disabled
        /// </summary>
        public ushort DelDirSize
        {
            get => GetUInt16(54);
            set => SetUInt16(54, value);
        }

        /// <summary>
        /// (5.1) filename size
        /// </summary>
        public ushort FnSize
        {
            get => GetUInt16(56);
            set => SetUInt16(56, value);
        }

        /// <summary>
        /// Pointer to superindex[i], (4.2) MODE_SUPERINDEX only, 16 entries
        /// </summary>
        public UInt32Pointer SuperIndex(uint i) => PointerTo(64 + (int)i * 4);

        /// <summary>
        /// (5.1) deldir user id (17.9)
        /// </summary>
        public ushort DdUid
        {
            get => GetUInt16(128);
            set => SetUInt16(128, value);
        }

        /// <summary>
        /// (5.1) deldir group id
        /// </summary>
        public ushort DdGid
        {
            get => GetUInt16(130);
            set => SetUInt16(130, value);
        }

        /// <summary>
        /// (5.1) deldir protection
        /// </summary>
        public uint DdProtection
        {
            get => GetUInt32(132);
            set => SetUInt32(132, value);
        }

        /// <summary>
        /// (5.1) deldir datestamp
        /// </summary>
        public ushort DdCreationDay
        {
            get => GetUInt16(136);
            set => SetUInt16(136, value);
        }

        public ushort DdCreationMinute
        {
            get => GetUInt16(138);
            set => SetUInt16(138, value);
        }

        public ushort DdCreationTick
        {
            get => GetUInt16(140);
            set => SetUInt16(140, value);
        }

        /// <summary>
        /// Pointer to deldir[i], (5.1) 32 deldir blocks
        /// </summary>
        public UInt32Pointer DelDir(int i) => PointerTo(144 + i * 4);
    }
}
