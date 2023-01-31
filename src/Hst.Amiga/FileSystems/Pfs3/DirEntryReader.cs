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
            
            var nameLength = bytes[offset + direntry.StartOfName];
            var dirEntry = new direntry(next)
            {
                type = (sbyte)bytes[offset + 1],
                anode = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 2),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 6),
                CreationDate = DateHelper.ReadDate(bytes, offset + 10),
                protection = bytes[offset + 16]
            };

            dirEntry.Name = nameLength == 0
                ? string.Empty
                : AmigaTextHelper.GetString(bytes, offset + direntry.StartOfName + 1, nameLength);
            
            //if (direntry.StartOfName + nameLength < next - (g.dirextension ? 2 : 0))
            //{
            var commentOffset = offset + direntry.StartOfName + 1 + nameLength;
            var commentLength = bytes[commentOffset];
            if (commentLength > 0)
            {
                dirEntry.comment = AmigaTextHelper.GetString(bytes, commentOffset + 1, commentLength);
                
            }
            //}

            if (g.dirextension)
            {
                dirEntry.ExtraFields = ReadExtraFields(bytes, offset, dirEntry);
            }
            
            return dirEntry;
        }

        private static extrafields ReadExtraFields(byte[] entries, int offset, direntry direntry)
        {
            // UWORD *extra = (UWORD *)extrafields;
            // UWORD *fields = (UWORD *)(((UBYTE *)direntry) + direntry->next);
            // ushort flags, i;
            //
            // flags = *(--fields);
            // for (i = 0; i < sizeof(struct extrafields) / 2; i++, flags >>= 1)
            // *(extra++) = (flags & 1) ? *(--fields) : 0;
            
            var extras = new ushort[SizeOf.ExtraFields.Struct / 2];
            var fields = offset + direntry.Next;
            fields -= 2;
            var flags = BigEndianConverter.ConvertBytesToUInt16(entries, fields);
            for (var i = 0; i < SizeOf.ExtraFields.Struct / 2; i++, flags >>= 1)
            {
                if ((flags & 1) == 0)
                {
                    extras[i] = 0;
                    continue;
                }
                fields -= 2;
                var extra = BigEndianConverter.ConvertBytesToUInt16(entries, fields);
                extras[i] = extra;
            }

            var extraFields = extrafields.ConvertToExtraFields(extras);
            
            return extraFields;
        }
    }
}