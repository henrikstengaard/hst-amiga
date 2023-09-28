namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using Blocks;
    using Core.Converters;

    public static class DirEntryReader
    {
        public static direntry Read(byte[] bytes, int offset, globaldata g)
        {
            // return empty dir entry, if offset is outside of bounds
            if (offset >= bytes.Length)
            {
                return new direntry();
            }

            // next indicates length of dir entry and number of bytes to skip where next entry starts
            var next = bytes[offset]; 
            
            // return empty dir entry, if next is zero (end of entries)
            if (next == 0)
            {
                return new direntry();
            }

            if (next < SizeOf.DirEntry.Struct)
            {
                throw new IOException($"Dir entry at offset {offset} with next {next} is smaller than struct size {SizeOf.DirEntry.Struct}");
            }

            if (offset + next >= bytes.Length)
            {
                throw new IOException($"Dir entry at offset {offset} with next {next} exceeds max {bytes.Length}");
            }

            var type = (sbyte)bytes[offset + 1];
            var anode = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 2);
            var fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 6);
            var creationDate = DateHelper.ReadDate(bytes, offset + 10);
            var protection = bytes[offset + 16];

            var nameLength = bytes[offset + direntry.StartOfName];
            var name = nameLength == 0
                ? string.Empty
                : AmigaTextHelper.GetString(bytes, offset + direntry.StartOfName + 1, nameLength);
            
            var commentOffset = offset + direntry.StartOfName + 1 + nameLength;
            var commentLength = bytes[commentOffset];
            var comment = commentLength > 0
                ? AmigaTextHelper.GetString(bytes, commentOffset + 1, commentLength)
                : string.Empty;

            var extraFields = g.dirextension ? ReadExtraFields(bytes, offset, next) : new extrafields();
            
            return new direntry(next, type, anode, fsize, protection, creationDate, name, comment, extraFields, g);
        }

        private static extrafields ReadExtraFields(byte[] data, int offset, int next)
        {
            // UWORD *extra = (UWORD *)extrafields;
            // UWORD *fields = (UWORD *)(((UBYTE *)direntry) + direntry->next);
            // ushort flags, i;
            //
            // flags = *(--fields);
            // for (i = 0; i < sizeof(struct extrafields) / 2; i++, flags >>= 1)
            // *(extra++) = (flags & 1) ? *(--fields) : 0;
            
            var extras = new ushort[SizeOf.ExtraFields.Struct / 2];
            var fields = offset + next;
            fields -= 2;
            var flags = BigEndianConverter.ConvertBytesToUInt16(data, fields);
            for (var i = 0; i < SizeOf.ExtraFields.Struct / 2; i++, flags >>= 1)
            {
                if ((flags & 1) == 0)
                {
                    extras[i] = 0;
                    continue;
                }
                fields -= 2;
                var extra = BigEndianConverter.ConvertBytesToUInt16(data, fields);
                extras[i] = extra;
            }

            var extraFields = extrafields.ConvertToExtraFields(extras);
            
            return extraFields;
        }
    }
}