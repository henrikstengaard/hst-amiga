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
        
        public static void WriteNullTerminatedString(string text, byte[] data, int offset, int maxLength)
        {
            var textBytes = Iso88591.GetBytes(text);
            var length = Math.Min(textBytes.Length, maxLength);
            Array.Copy(textBytes, 0, data, offset, length);
            data[offset + length] = 0;
        }

        public static byte[] GetBytes(string value)
        {
            return Iso88591.GetBytes(value);
        }

        public static char ToUpper(char c)
        {
            return (char)(c >= 'a' && c <= 'z' ? c - ('a' - 'A') : c);
        }

        public static char InternationalToUpper(char c)
        {
            return (char)((c >= 'a' && c <= 'z') || (c >= 224 && c <= 254 && c != 247) ? c - ('a' - 'A') : c);
        }
        
        public static string ToUpper(string text, bool international)
        {
            var upperCasedText = text.ToCharArray();
            for (var i = 0; i < text.Length; i++)
            {
                upperCasedText[i] = international ? InternationalToUpper(text[i]) : ToUpper(text[i]);
            }
            return new string(upperCasedText);
        }
    }
}