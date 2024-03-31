using System;
using System.Globalization;
using System.Text;

namespace Hst.Amiga.DataTypes.UaeMetafiles
{
    public class UaeMetafileWriter
    {
        private static readonly int ProtectionBitsSize = "hsparwed".Length;
        private static readonly int DateSize = "yyyy-mm-dd 00:00:00.00".Length;
        private const int DelimiterSize = 1;
        private const int NewlineSize = 1;
        
        public static byte[] Build(UaeMetafile uaeMetafile)
        {
            if (uaeMetafile.ProtectionBits.Length != 8)
            {
                throw new ArgumentException("Protection bits must have length of 8 characters", nameof(UaeMetafile.ProtectionBits));
            }
            
            var commentBytes = string.IsNullOrWhiteSpace(uaeMetafile.Comment)
                ? Array.Empty<byte>()
                : Encoding.ASCII.GetBytes(uaeMetafile.Comment);
            var size = ProtectionBitsSize + DelimiterSize + DateSize + DelimiterSize + commentBytes.Length +
                       NewlineSize;
            var uaeMetafileBytes = new byte[size];

            // protection bits
            var protectionBitsBytes = Encoding.ASCII.GetBytes(uaeMetafile.ProtectionBits);
            Array.Copy(protectionBitsBytes, 0, uaeMetafileBytes, 0, ProtectionBitsSize);

            // delimiter
            uaeMetafileBytes[ProtectionBitsSize] = 0x20;
            
            // date
            var dateFormatted = uaeMetafile.Date.ToString("yyyy-MM-dd HH:mm:ss.ff", CultureInfo.InvariantCulture);
            var dateFormattedBytes = Encoding.ASCII.GetBytes(dateFormatted);
            Array.Copy(dateFormattedBytes, 0, uaeMetafileBytes, ProtectionBitsSize + DelimiterSize, DateSize);

            // delimiter
            uaeMetafileBytes[ProtectionBitsSize + DelimiterSize + DateSize] = 0x20;

            // comment
            if (commentBytes.Length > 0)
            {
                Array.Copy(commentBytes, 0, uaeMetafileBytes, ProtectionBitsSize + DelimiterSize + DateSize + DelimiterSize, commentBytes.Length);
            }
            
            // newline
            uaeMetafileBytes[uaeMetafileBytes.Length - 1] = 0xa;
            
            return uaeMetafileBytes;
        }
    }
}