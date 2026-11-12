namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using Core.Converters;

    /// <summary>
    /// Pointer to a big endian uint32 in a block (uint32 * in original code), e.g. pointer to a block number
    /// in an index block, which is updated by repair functions.
    /// </summary>
    public class UInt32Pointer
    {
        public byte[] Data { get; }
        public int Offset { get; }

        public UInt32Pointer(byte[] data, int offset)
        {
            Data = data;
            Offset = offset;
        }

        public uint Value
        {
            get => BigEndianConverter.ConvertBytesToUInt32(Data, Offset);
            set => BigEndianConverter.ConvertUInt32ToBytes(value, Data, Offset);
        }
    }
}
