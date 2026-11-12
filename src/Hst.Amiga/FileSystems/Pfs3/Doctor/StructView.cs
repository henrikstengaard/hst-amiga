namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using Core.Converters;

    /// <summary>
    /// View of a big endian struct in a byte buffer. Replaces casting pointers to structs in the original code,
    /// so blocks can be read, checked and edited in place even when corrupt.
    /// </summary>
    public abstract class StructView
    {
        public byte[] Data { get; }
        public int Offset { get; }

        protected StructView(byte[] data, int offset)
        {
            Data = data;
            Offset = offset;
        }

        protected byte GetByte(int offset) => Data[Offset + offset];

        protected void SetByte(int offset, byte value) => Data[Offset + offset] = value;

        protected ushort GetUInt16(int offset) => BigEndianConverter.ConvertBytesToUInt16(Data, Offset + offset);

        protected void SetUInt16(int offset, ushort value) =>
            BigEndianConverter.ConvertUInt16ToBytes(value, Data, Offset + offset);

        protected uint GetUInt32(int offset) => BigEndianConverter.ConvertBytesToUInt32(Data, Offset + offset);

        protected void SetUInt32(int offset, uint value) =>
            BigEndianConverter.ConvertUInt32ToBytes(value, Data, Offset + offset);

        /// <summary>
        /// Pointer to uint32 at offset in struct.
        /// </summary>
        protected UInt32Pointer PointerTo(int offset) => new UInt32Pointer(Data, Offset + offset);
    }
}
