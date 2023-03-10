namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;
    using Core.Converters;

    public static class DelDirEntryWriter
    {
        public static void Write(byte[] data, int offset, deldirentry delDirEntry)
        {
            BigEndianConverter.ConvertUInt32ToBytes(delDirEntry.anodenr, data, offset);
            BigEndianConverter.ConvertUInt32ToBytes(delDirEntry.fsize, data, offset + 0x4);
            DateHelper.WriteDate(delDirEntry.CreationDate, data, offset + 0x8); 

            var fileName = delDirEntry.filename ?? string.Empty;
            var fileNameBytes = AmigaTextHelper.GetBytes(fileName.Length > 16 ? fileName.Substring(0, 16) : fileName);
            Array.Copy(fileNameBytes, 0, data, offset + 0xe, fileNameBytes.Length);
            for (var i = fileNameBytes.Length; i < 16; i++)
            {
                data[offset + 0xe + i] = 0;
            }

            BigEndianConverter.ConvertUInt16ToBytes(delDirEntry.fsizex, data, offset + 0x1e);
        }
    }
}