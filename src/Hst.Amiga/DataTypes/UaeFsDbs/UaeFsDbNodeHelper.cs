﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hst.Amiga.FileSystems;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbNodeHelper
    {
        private static readonly char[] SpecialFilenameChars = 
            { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };
        private static readonly HashSet<char> SpecialFilenameCharSet = 
            new HashSet<char>(SpecialFilenameChars);
        
        public static bool HasSpecialFilenameChars(string filename)
        {
            return filename.EndsWith("") || filename.Any(c => SpecialFilenameCharSet.Contains(c));
        }
        
        public static string MakeSafeFilename(string filename)
        {
            var safeFilename = filename.ToCharArray();

            var isPrevDotChar = true;
            
            for (var i = safeFilename.Length - 1; i >= 0; i--)
            {
                var isLastChar = i == safeFilename.Length - 1;
                var isDotChar = safeFilename[i] == '.';

                if (isLastChar && isDotChar ||
                    isDotChar && isPrevDotChar || 
                    SpecialFilenameCharSet.Contains(safeFilename[i]))
                {
                    safeFilename[i] = '_';
                }

                if (isPrevDotChar)
                {
                    isPrevDotChar = isDotChar;
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

        private static string CreateUniqueFileNormalName(string path, string filename)
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
        
        private static string CreateUniqueDirectoryNormalName(string path, string filename)
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
    }
}