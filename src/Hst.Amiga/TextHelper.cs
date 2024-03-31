using System;
using System.Text;

namespace Hst.Amiga
{
    public static class TextHelper
    {
        public static string ReadNullTerminatedString(Encoding encoding, byte[] data, int offset, int maxLength)
        {
            if (offset >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (data.Length == 0 || maxLength == 0)
            {
                return string.Empty;
            }

            var zeroSize = encoding.GetByteCount("\0");
            var length = 0;
            var zeroCount = 0;
            for (var i = 0; i < maxLength && offset + i < data.Length; i++)
            {
                if (data[offset + i] == 0)
                {
                    zeroCount++;

                    if (zeroCount >= zeroSize)
                    {
                        break;
                    }
                }
                else
                {
                    zeroCount = 0;
                }

                length++;
            }

            return encoding.GetString(data, offset, length);
        }

        public static void WriteNullTerminatedString(Encoding encoding, string text, byte[] data, int offset, int maxLength)
        {
            var textBytes = encoding.GetBytes(text);
            var length = Math.Min(textBytes.Length, maxLength);
            
            if (offset + length + 1 >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"Offset {offset} and text length {length} in bytes is out of range for data size {data.Length}");
            }
            
            Array.Copy(textBytes, 0, data, offset, length);
            data[offset + length] = 0;
        }
    }
}