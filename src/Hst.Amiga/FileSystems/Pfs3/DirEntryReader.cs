namespace Hst.Amiga.FileSystems.Pfs3
{
    using Blocks;
    using Core.Converters;

    public static class DirEntryReader
    {
        public static direntry Read(byte[] bytes, int offset)
        {
            var nLength = bytes[offset + 17];

            return new direntry
            {
                Offset = offset,
                next = bytes[offset],
                type = (sbyte)bytes[offset + 1],
                anode = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 2),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 6),
                CreationDate = DateHelper.ReadDate(bytes, offset + 10),
                protection = bytes[offset + 16],
                nlength = nLength,
                Name = AmigaTextHelper.GetString(bytes, offset + 18, nLength),
                startofname = 18,
                pad = 0
            };
        }
    }
}