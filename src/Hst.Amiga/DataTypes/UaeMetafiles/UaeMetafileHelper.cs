using System;
using System.Linq;
using System.Text;

namespace Hst.Amiga.DataTypes.UaeMetafiles
{
    public static class UaeMetafileHelper
    {
        private static readonly char[] AmigaSpecialFilenameChars = { '\\', '*', '?', '"', '<', '>' };

        public static string EncodeFilename(string filename)
        {
            var encodedFilename = new StringBuilder();

            foreach (var chr in filename)
            {
                if (AmigaSpecialFilenameChars.Contains(chr))
                {
                    encodedFilename.Append($"%{(int)chr:x2}");
                    continue;
                }

                encodedFilename.Append(chr);
            }

            return encodedFilename.ToString();
        }
        
        public static string DecodeFilename(string filename)
        {
            var decodedFilename = new StringBuilder();

            for (var i = 0; i < filename.Length; i++)
            {
                var chr = filename[i];

                if (chr != '%')
                {
                    decodedFilename.Append(chr);
                    continue;
                }

                if (i + 2 >= filename.Length)
                {
                    throw new ArgumentOutOfRangeException();
                }
                
                var asciiCharValue = Convert.ToInt32("0x" + filename[i + 1] + filename[i + 2], 16);
                
                decodedFilename.Append((char)asciiCharValue);

                i += 2;
            }

            return decodedFilename.ToString();
        }
    }
}