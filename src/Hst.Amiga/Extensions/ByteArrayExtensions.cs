namespace Hst.Amiga.Extensions
{
    using System;

    public static class ByteArrayExtensions
    {
        public static string ReadStringWithLength(this byte[] bytes, int offset = 0, int maxLength = 0)
        {
            var length = bytes[offset];
            return AmigaTextHelper.GetString(bytes, offset + 1, maxLength == 0 ? length : Math.Min(length, maxLength));
        }

        public static string ReadStringWithNullTermination(this byte[] bytes, int offset = 0)
        {
            var index = 0;
            for (index = offset; index < bytes.Length; index++)
            {
                if (bytes[index] == 0)
                {
                    break;
                }
            }

            var length = index - offset;
            return length <= 0 ? string.Empty : AmigaTextHelper.GetString(bytes, offset, length);
        }

        public static string FormatDosType(this byte[] bytes)
        {
            if (bytes.Length != 4)
            {
                throw new ArgumentException("Invalid dos type");
            }

            var dosIdentifier = new byte[3];
            Array.Copy(bytes, 0, dosIdentifier, 0, 3);
            return $"{AmigaTextHelper.GetString(dosIdentifier)}\\{bytes[3]}";
        }

        public static void WriteString(this byte[] bytes, int offset, string text, int length, byte fillByte = 0)
        {
            var textBytes = AmigaTextHelper.GetBytes(text.Length > length
                ? text.Substring(0, length)
                : text);

            Array.Copy(textBytes, 0, bytes, offset, textBytes.Length);

            if (textBytes.Length >= length)
            {
                return;
            }

            var fillBytes = textBytes.Length - bytes.Length;
            for (var i = 0; i < fillBytes; i++)
            {
                bytes[textBytes.Length + i] = fillByte;
            }
        }

        public static void WriteStringWithLength(this byte[] bytes, int offset, string value, int maxLength)
        {
            bytes[offset] = (byte)Math.Min(value.Length, maxLength);
            bytes.WriteString(offset + 1, value, maxLength);
        }
    }
}