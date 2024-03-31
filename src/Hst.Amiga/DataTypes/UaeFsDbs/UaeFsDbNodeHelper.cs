using System;
using System.IO;
using System.Linq;
using Hst.Amiga.FileSystems;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbNodeHelper
    {
        private static readonly char[] AmigaSpecialFilenameChars = { '\\', '*', '?', '"', '<', '>' };

        public static string MakeSafeFilename(string filename)
        {
            var safeFilename = filename.ToCharArray();
            
            for (var i = 0; i < filename.Length; i++)
            {
                if (safeFilename[i] == '.' || AmigaSpecialFilenameChars.Contains(safeFilename[i]))
                {
                    safeFilename[i] = '_';
                }
            }

            return new string(safeFilename);
        }

        public static string CreateUniqueNormalName(string path, string filename)
        {
            var uniquePath = Path.Combine(path, string.Concat("__uae___", filename));
            while (File.Exists(uniquePath))
            {
                uniquePath += CreateRandomName();
            }

            return uniquePath;
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
    }
}