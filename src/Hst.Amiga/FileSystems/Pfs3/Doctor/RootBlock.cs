namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;

    /// <summary>
    /// Root block view (rootblock_t in pfs3.h).
    /// </summary>
    public class RootBlock : StructView
    {
        private const int IdxOffset = 96;

        public RootBlock(byte[] data) : base(data, 0)
        {
        }

        public uint DiskType
        {
            get => GetUInt32(0);
            set => SetUInt32(0, value);
        }

        /// <summary>
        /// bit 0 is harddisk mode
        /// </summary>
        public uint Options
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
        /// days since Jan. 1, 1978 (like ADOS; WORD instead of LONG)
        /// </summary>
        public ushort CreationDay
        {
            get => GetUInt16(12);
            set => SetUInt16(12, value);
        }

        /// <summary>
        /// minutes past midnight
        /// </summary>
        public ushort CreationMinute
        {
            get => GetUInt16(14);
            set => SetUInt16(14, value);
        }

        /// <summary>
        /// ticks past minute
        /// </summary>
        public ushort CreationTick
        {
            get => GetUInt16(16);
            set => SetUInt16(16, value);
        }

        /// <summary>
        /// protection bits (ala ADOS)
        /// </summary>
        public ushort Protection
        {
            get => GetUInt16(18);
            set => SetUInt16(18, value);
        }

        /// <summary>
        /// Length of disk label (diskname[0], pascal string)
        /// </summary>
        public byte DiskNameLength
        {
            get => GetByte(20);
            set => SetByte(20, value);
        }

        /// <summary>
        /// disk label (pascal string)
        /// </summary>
        public string DiskName
        {
            get => AmigaTextHelper.GetString(Data, Offset + 21, Math.Min((int)DiskNameLength, 31));
            set
            {
                var bytes = AmigaTextHelper.GetBytes(value);
                var length = Math.Min(bytes.Length, 31);
                Array.Clear(Data, Offset + 20, 32);
                SetByte(20, (byte)length);
                Array.Copy(bytes, 0, Data, Offset + 21, length);
            }
        }

        /// <summary>
        /// reserved area. blocknumbers
        /// </summary>
        public uint LastReserved
        {
            get => GetUInt32(52);
            set => SetUInt32(52, value);
        }

        public uint FirstReserved
        {
            get => GetUInt32(56);
            set => SetUInt32(56, value);
        }

        /// <summary>
        /// number of reserved blocks (blksize blocks) free
        /// </summary>
        public uint ReservedFree
        {
            get => GetUInt32(60);
            set => SetUInt32(60, value);
        }

        /// <summary>
        /// size of reserved blocks in bytes
        /// </summary>
        public ushort ReservedBlksize
        {
            get => GetUInt16(64);
            set => SetUInt16(64, value);
        }

        /// <summary>
        /// number of blocks in rootblock, including bitmap
        /// </summary>
        public ushort RblkCluster
        {
            get => GetUInt16(66);
            set => SetUInt16(66, value);
        }

        /// <summary>
        /// blocks free
        /// </summary>
        public uint BlocksFree
        {
            get => GetUInt32(68);
            set => SetUInt32(68, value);
        }

        /// <summary>
        /// minimum number of blocks free
        /// </summary>
        public uint AlwaysFree
        {
            get => GetUInt32(72);
            set => SetUInt32(72, value);
        }

        /// <summary>
        /// current LONG bitmapfield nr for allocation
        /// </summary>
        public uint RovingPtr
        {
            get => GetUInt32(76);
            set => SetUInt32(76, value);
        }

        /// <summary>
        /// (before 4.3) deldir location
        /// </summary>
        public uint DelDir
        {
            get => GetUInt32(80);
            set => SetUInt32(80, value);
        }

        /// <summary>
        /// disksize in sectors
        /// </summary>
        public uint DiskSize
        {
            get => GetUInt32(84);
            set => SetUInt32(84, value);
        }

        /// <summary>
        /// rootblock extension
        /// </summary>
        public uint Extension
        {
            get => GetUInt32(88);
            set => SetUInt32(88, value);
        }

        /// <summary>
        /// Pointer to idx.small.bitmapindex[i], 5 bitmap indexblocks.
        /// </summary>
        public UInt32Pointer IdxSmallBitmapIndex(uint i) => PointerTo(IdxOffset + (int)i * 4);

        /// <summary>
        /// Pointer to idx.small.indexblocks[i], 99 indexblocks.
        /// </summary>
        public UInt32Pointer IdxSmallIndexBlocks(uint i) => PointerTo(IdxOffset + 5 * 4 + (int)i * 4);

        /// <summary>
        /// Pointer to idx.large.bitmapindex[i], 104 bitmap indexblock.
        /// </summary>
        public UInt32Pointer IdxLargeBitmapIndex(uint i) => PointerTo(IdxOffset + (int)i * 4);
    }
}
