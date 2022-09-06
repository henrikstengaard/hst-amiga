namespace Hst.Amiga
{
    using System;
    using System.Text;

    public static class AmigaTextHelper
    {
        private static readonly Encoding Iso88591 = Encoding.GetEncoding("ISO-8859-1");

        public static string GetString(byte[] bytes)
        {
            return bytes.Length == 0
                ? string.Empty
                : Iso88591.GetString(bytes, 0, bytes.Length);
        }
        
        public static string GetString(byte[] bytes, int index, int count)
        {
            if (index >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (bytes.Length == 0 || count == 0)
            {
                return string.Empty;
            }

            return bytes.Length == 0
                ? string.Empty
                : Iso88591.GetString(bytes, index, index + count >= bytes.Length ? bytes.Length - index : count);
        }

        public static string GetNullTerminatedString(byte[] bytes, int index, int maxLength)
        {
            if (index >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (bytes.Length == 0 || maxLength == 0)
            {
                return string.Empty;
            }
            
            int stringLength;
            for (stringLength = 0; stringLength < Math.Min(bytes.Length, index + maxLength); stringLength++)
            {
                if (bytes[index + stringLength] == 0)
                {
                    break;
                }
            }

            return Iso88591.GetString(bytes, index, stringLength);
        }
        
        public static byte[] GetBytes(string value)
        {
            return Iso88591.GetBytes(value);
        }
    }
}