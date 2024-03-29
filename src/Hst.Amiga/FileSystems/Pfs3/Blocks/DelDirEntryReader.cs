﻿namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using Core.Converters;

    public static class DelDirEntryReader
    {
        public static deldirentry Read(byte[] bytes, int offset)
        {
            int fileNameLength;
            for (fileNameLength = 0; fileNameLength < 16; fileNameLength++)
            {
                if (bytes[offset + 0xe + fileNameLength] == 0)
                {
                    break;
                }
            }
            return new deldirentry
            {
                Offset = offset,
                anodenr = BigEndianConverter.ConvertBytesToUInt32(bytes, offset),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 0x4),
                CreationDate = DateHelper.ReadDate(bytes, offset + 0x8),
                filename = AmigaTextHelper.GetString(bytes, offset + 0xe, fileNameLength),
                fsizex = BigEndianConverter.ConvertBytesToUInt16(bytes, offset + 0x1e),
            };
        }
    }
}