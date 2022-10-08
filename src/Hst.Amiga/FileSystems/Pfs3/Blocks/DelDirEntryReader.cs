namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using Core.Converters;

    public static class DelDirEntryReader
    {
        public static deldirentry Read(byte[] bytes, int offset)
        {
            return new deldirentry
            {
                Offset = offset,
                anodenr = BigEndianConverter.ConvertBytesToUInt32(bytes, offset),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 4),
                CreationDate = DateHelper.ReadDate(bytes, offset + 8),
                filename = AmigaTextHelper.GetString(bytes, offset + 14, 16),
                fsizex = BigEndianConverter.ConvertBytesToUInt16(bytes, offset + 30),
            };
        }
    }
}