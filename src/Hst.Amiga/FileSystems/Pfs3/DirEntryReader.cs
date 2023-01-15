namespace Hst.Amiga.FileSystems.Pfs3
{
    using Blocks;
    using Core.Converters;

    public static class DirEntryReader
    {
        public static direntry Read(byte[] bytes, int offset)
        {
            // return empty dir entry, if offset is outside of bounds
            if (offset >= bytes.Length)
            {
                return new direntry();
            }

            // next indicates length of dir entry and number of bytes to skip where next entry starts
            var next = bytes[offset]; 
            
            // return empty dir entry, if next is zero (end of entries) or offset + next is outside of bounds
            if (next == 0 || offset + next >= bytes.Length)
            {
                return new direntry();
            }
            
            var nameLength = bytes[offset + 17];
            var dirEntry = new direntry
            {
                Offset = offset,
                next = next,
                type = (sbyte)bytes[offset + 1],
                anode = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 2),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 6),
                CreationDate = DateHelper.ReadDate(bytes, offset + 10),
                protection = bytes[offset + 16],
                nlength = nameLength,
                Name = nameLength ==0 ? string.Empty : AmigaTextHelper.GetString(bytes, offset + 18, nameLength),
                startofname = 18,
                pad = 0
            };

            if (dirEntry.startofname + nameLength < dirEntry.next)
            {
                var commentLength = bytes[offset + dirEntry.startofname + nameLength];
                dirEntry.comment = AmigaTextHelper.GetString(bytes, offset + dirEntry.startofname + nameLength + 1, commentLength);
            }
            
            return dirEntry;
        }
    }
}