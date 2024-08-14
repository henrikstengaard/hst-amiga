using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hst.Amiga.DataTypes.UaeMetafiles
{
    public static class UaeMetafileHelper
    {
        private static readonly char[] SpecialFilenameChars = 
            { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };
        private static readonly HashSet<char> SpecialFilenameCharSet = 
            new HashSet<char>(SpecialFilenameChars);

        public static bool HasSpecialFilenameChars(string filename)
        {
            return filename.Any(c => SpecialFilenameCharSet.Contains(c));
        }

        public static string EncodeFilenameSpecialChars(string filename)
        {
            var encodedFilename = new StringBuilder();

            for (var i = 0; i < filename.Length; i++)
            {
                var isLastChar = i == filename.Length - 1;
                var filenameChar = filename[i];
                var isDotChar = filenameChar == '.';

                if ((isLastChar && isDotChar) || 
                    SpecialFilenameCharSet.Contains(filenameChar))
                {
                    encodedFilename.Append($"%{(int)filenameChar:x2}");
                    continue;
                }
                
                encodedFilename.Append(filenameChar);
            }

            return encodedFilename.ToString();
        }

        public static string EncodeFilename(string filename)
        {
            var encodedFilename = new StringBuilder();
            
            foreach (var chr in filename)
            {
                encodedFilename.Append($"%{(int)chr:x2}");
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