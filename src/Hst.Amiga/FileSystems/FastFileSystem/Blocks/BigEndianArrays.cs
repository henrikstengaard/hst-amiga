namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;
    using System.Buffers.Binary;

    /// <summary>
    /// Read and write arrays of big endian unsigned 32-bit values in block bytes
    /// </summary>
    internal static class BigEndianArrays
    {
        /// <summary>
        /// Read count big endian unsigned 32-bit values from bytes at offset
        /// </summary>
        public static uint[] ReadUInt32Array(byte[] bytes, int offset, int count)
        {
            var values = new uint[count];
            var span = new ReadOnlySpan<byte>(bytes, offset, count * 4);
            for (var i = 0; i < count; i++)
            {
                values[i] = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(i * 4, 4));
            }

            return values;
        }

        /// <summary>
        /// Write count values as big endian unsigned 32-bit values to bytes at offset. Zero is written for
        /// values after length of values.
        /// </summary>
        public static void WriteUInt32Array(byte[] bytes, int offset, uint[] values, int count)
        {
            var span = new Span<byte>(bytes, offset, count * 4);
            for (var i = 0; i < count; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(span.Slice(i * 4, 4), i < values.Length ? values[i] : 0);
            }
        }
    }
}
