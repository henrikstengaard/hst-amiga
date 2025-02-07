using System;
using System.Collections.Generic;
using System.IO;
using Hst.Amiga.FileSystems;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbNodeHelper
    {
        private static readonly char[] SpecialFilenameChars = 
            { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };
        private static readonly HashSet<char> SpecialFilenameCharSet = 
            new HashSet<char>(SpecialFilenameChars);

        private static bool IsPrintableChar(char chr)
        {
            var asciiValue = (int)chr;
            return asciiValue >= 32 && asciiValue <= 127;
        }

        public static bool HasSpecialFilenameChars(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }

            return !filename.Equals(MakeSafeFilename(filename));
        }

        public static string MakeSafeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return string.Empty;
            }

            var safeFilename = filename.ToCharArray();

            var isFirstCharIsDot = safeFilename[0] == '.';

            var hasHeadingSpecialChars = safeFilename[0] == '.' || safeFilename[0] == ' ';
            var hasTailingSpecialChars = safeFilename[safeFilename.Length - 1] == '.' || safeFilename[safeFilename.Length - 1] == ' ';

            var replaceSpecialChars = (hasHeadingSpecialChars || hasTailingSpecialChars) &&
                !(isFirstCharIsDot && !hasTailingSpecialChars);

            for (var i = 0; i < safeFilename.Length; i++)
            {
                var chr = safeFilename[i];
                var isDotChar = chr == '.';
                var isSpaceChar = chr == ' ';

                if ((replaceSpecialChars && (isDotChar || isSpaceChar)) ||
                    SpecialFilenameCharSet.Contains(safeFilename[i]) ||
                    !IsPrintableChar(chr))
                {
                    safeFilename[i] = '_';
                }
            }

            return new string(safeFilename);
        }

        public static string CreateUniqueNormalName(string path, string filename)
        {
            var uniqueFileName = string.Concat("__uae___", filename);
            var uniquePath = Path.Combine(path, uniqueFileName);

            var fileExists = File.Exists(uniquePath);
            var directoryExists = Directory.Exists(uniquePath);
            
            if (!fileExists && !directoryExists)
            {
                return uniqueFileName;
            }

            return fileExists
                ? CreateUniqueFileNormalName(path, filename)
                : CreateUniqueDirectoryNormalName(path, filename);
        }

        public static string CreateUniqueFileNormalName(string path, string filename)
        {
            string uniqueFileName;
            string uniquePath;
            
            do
            {
                uniqueFileName = string.Concat("__uae___", filename, CreateRandomName());
                uniquePath = Path.Combine(path, uniqueFileName);
            } while (File.Exists(uniquePath));

            return uniqueFileName;
        }
        
        public static string CreateUniqueDirectoryNormalName(string path, string filename)
        {
            string uniqueFileName;
            string uniquePath;
            
            do
            {
                uniqueFileName = string.Concat("__uae___", filename, CreateRandomName());
                uniquePath = Path.Combine(path, uniqueFileName);
            } while (Directory.Exists(uniquePath));

            return uniqueFileName;
        }
        
        private const string RandomChars = "_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        
        private static string CreateRandomName()
        {
            var random = new Random();
            
            var randomName = new char[8];
            for (var i = 0; i < randomName.Length; i++)
            {
                randomName[i] = RandomChars[random.Next() % RandomChars.Length];
            }

            return new string(randomName);
        }
        
        public static UaeFsDbNode Create(string amigaName, string normalName)
        {
            return new UaeFsDbNode
            {
                Valid = 1,
                Mode = 0U,
                AmigaName = amigaName,
                NormalName = normalName,
                Comment = string.Empty,
                WinMode = (uint)FileAttributes.Archive,
                AmigaNameUnicode = amigaName,
                NormalNameUnicode = normalName
            };
        }
        
        public static UaeFsDbNode CreateFromPath(string path)
        {
            var fileInfo = new FileInfo(path);

            return new UaeFsDbNode
            {
                Valid = 1,
                Mode = (uint)ProtectionBitsConverter.ReadProtectionBitsFromFile(fileInfo),
                AmigaName = fileInfo.Name,
                NormalName = fileInfo.Name,
                Comment = string.Empty,
                WinMode = (uint)fileInfo.Attributes,
                AmigaNameUnicode = fileInfo.Name,
                NormalNameUnicode = fileInfo.Name
            };
        }

        public static UaeFsDbNode.NodeVersion GetUaeFsDbNodeVersion(long fileSize)
        {
            if (fileSize % Constants.UaeFsDbNodeVersion2Size == 0)
            {
                return UaeFsDbNode.NodeVersion.Version2;
            }

            if (fileSize % Constants.UaeFsDbNodeVersion1Size == 0)
            {
                return UaeFsDbNode.NodeVersion.Version1;
            }

            throw new ArgumentException($"File size {fileSize} is not a multiple of UAEFSDB node sizes: {Constants.UaeFsDbNodeVersion1Size} bytes for version 1 or {Constants.UaeFsDbNodeVersion2Size} bytes for version 2", nameof(fileSize));
        }
    }
}