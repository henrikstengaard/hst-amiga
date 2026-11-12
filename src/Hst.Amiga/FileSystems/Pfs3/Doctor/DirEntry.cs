namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;

    /// <summary>
    /// Directory entry view (struct direntry in pfs3.h) at an offset in a directory block.
    /// </summary>
    public class DirEntry : StructView
    {
        /// <summary>
        /// offsetof(struct direntry, nlength)
        /// </summary>
        public const int OffsetOfNLength = 17;

        /// <summary>
        /// offsetof(struct direntry, startofname)
        /// </summary>
        public const int OffsetOfStartOfName = 18;

        public DirEntry(byte[] data, int offset) : base(data, offset)
        {
        }

        /// <summary>
        /// sizeof direntry
        /// </summary>
        public byte Next
        {
            get => GetByte(0);
            set => SetByte(0, value);
        }

        /// <summary>
        /// dir, file, link etc
        /// </summary>
        public sbyte Type
        {
            get => (sbyte)GetByte(1);
            set => SetByte(1, (byte)value);
        }

        /// <summary>
        /// anode nummer
        /// </summary>
        public uint Anode
        {
            get => GetUInt32(2);
            set => SetUInt32(2, value);
        }

        /// <summary>
        /// sizeof file
        /// </summary>
        public uint FSize
        {
            get => GetUInt32(6);
            set => SetUInt32(6, value);
        }

        public ushort CreationDay
        {
            get => GetUInt16(10);
            set => SetUInt16(10, value);
        }

        public ushort CreationMinute
        {
            get => GetUInt16(12);
            set => SetUInt16(12, value);
        }

        public ushort CreationTick
        {
            get => GetUInt16(14);
            set => SetUInt16(14, value);
        }

        /// <summary>
        /// protection bits (like DOS)
        /// </summary>
        public byte Protection
        {
            get => GetByte(16);
            set => SetByte(16, value);
        }

        /// <summary>
        /// length of filename
        /// </summary>
        public byte NLength
        {
            get => GetByte(OffsetOfNLength);
            set => SetByte(OffsetOfNLength, value);
        }

        /// <summary>
        /// Filename (startofname), limited to buffer.
        /// </summary>
        public string Name
        {
            get
            {
                var start = Offset + OffsetOfStartOfName;
                var length = Math.Max(0, Math.Min(NLength, Data.Length - start));
                return AmigaTextHelper.GetString(Data, start, length);
            }
        }

        /// <summary>
        /// Filenote length (*FILENOTE(de)), 0 if outside buffer.
        /// </summary>
        public byte FileNoteLength
        {
            get
            {
                var offset = Offset + OffsetOfStartOfName + NLength;
                return offset < Data.Length ? Data[offset] : (byte)0;
            }
        }

        /// <summary>
        /// Read word at offset relative to directory entry, 0 if outside buffer.
        /// </summary>
        public ushort ReadWord(int offset)
        {
            var position = Offset + offset;
            return position >= 0 && position + 2 <= Data.Length ? GetUInt16(offset) : (ushort)0;
        }

        /// <summary>
        /// Write word at offset relative to directory entry.
        /// </summary>
        public void WriteWord(int offset, ushort value) => SetUInt16(offset, value);

        /// <summary>
        /// get next directory entry (NEXTENTRY(de))
        /// </summary>
        public DirEntry NextEntry() => new DirEntry(Data, Offset + Next);
    }
}
