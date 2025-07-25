using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Hst.Amiga.DataTypes.UaeMetafiles
{
    public class UaeMetafileReader
    {
        private static Encoding Iso88591Encoding = Encoding.GetEncoding("ISO-8859-1");
        private const int MaxCommentLength = 80;

        public static UaeMetafile Read(byte[] data)
        {
            if (data.Length <= 8)
            {
                throw new ArgumentException($"Data bytes {data.Length} doesn't contain protection bits", nameof(data));
            }

            var protectionBits = Iso88591Encoding.GetString(data, 0, 8);
            
            if (data.Length < 30)
            {
                throw new ArgumentException($"Data bytes {data.Length} doesn't contain date", nameof(data));
            }
            
            var date = Iso88591Encoding.GetString(data, 9, 22);
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm:ss.ff",
                    Thread.CurrentThread.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                throw new ArgumentException($"Date '{date}' is invalid format", nameof(data));
            }
            
            var comment = data.Length > 31 && data.Length > 33
                ? Iso88591Encoding.GetString(data, 32, data.Length - 32 > MaxCommentLength ? MaxCommentLength : data.Length - 32)
                : string.Empty;

            if (comment.Length > 0 && comment[comment.Length - 1] == '\n')
            {
                comment = comment.Substring(0, comment.Length - 1);
            }

            if (comment.Length > 0 && comment[comment.Length - 1] == '\r')
            {
                comment = comment.Substring(0, comment.Length - 1);
            }
            
            return new UaeMetafile
            {
                ProtectionBits = protectionBits,
                Date = parsedDate,
                Comment = comment.Length > MaxCommentLength
                    ? comment.Substring(0, MaxCommentLength)
                    : comment
            };
        }
    }
}