namespace Hst.Amiga.Extensions
{
    using System;

    public static class ByteArrayExtensions
    {
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
            if (length <= 0)
            {
                return string.Empty;
            }
            
            var stringBytes = new byte[length];
            Array.Copy(bytes, offset, stringBytes, 0, length);
            
            return AmigaTextHelper.GetString(stringBytes);
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
    }
}