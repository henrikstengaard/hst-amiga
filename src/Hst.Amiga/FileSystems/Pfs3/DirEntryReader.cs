namespace Hst.Amiga.FileSystems.Pfs3
{
    using Blocks;
    using Core.Converters;

    public static class DirEntryReader
    {
        public static direntry Read(byte[] bytes, int offset)
        {
            if (bytes[offset] == 0 || offset + 17 > bytes.Length)
            {
                return new direntry();
            }
            
            var nLength = bytes[offset + 17];

            var dirEntry = new direntry
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

            if (dirEntry.startofname + nLength < dirEntry.next)
            {
                // destcomment = (UBYTE *)&destentry->startofname + destentry->nlength;
                var cLength = bytes[offset + dirEntry.startofname + nLength];
                dirEntry.comment = AmigaTextHelper.GetString(bytes, offset + dirEntry.startofname + nLength + 1, cLength);
            }
            
            return dirEntry;
        }
    }
}