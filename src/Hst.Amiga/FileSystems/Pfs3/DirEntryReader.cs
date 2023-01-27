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

        public static extrafields ReadExtraFields(byte[] entries, int offset, direntry direntry)
        {
            // UWORD *extra = (UWORD *)extrafields;
            // UWORD *fields = (UWORD *)(((UBYTE *)direntry) + direntry->next);
            // ushort flags, i;
            //
            // flags = *(--fields);
            // for (i = 0; i < sizeof(struct extrafields) / 2; i++, flags >>= 1)
            // *(extra++) = (flags & 1) ? *(--fields) : 0;

            var extras = new ushort[SizeOf.ExtraFields.Struct / 2];
            var fields = direntry.Offset + direntry.next;
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

            var extraFields = ConvertToExtraFields(extras);
            
            return extraFields;
        }
        
        /// <summary>
        /// Replicate c behavior of reading array of ushorts memory area as a extra fields struct. "UWORD *extra = (UWORD *)extrafields" and "*(extra++) = (flags & 1) ? *(--fields) : 0"
        /// </summary>
        /// <param name="extras"></param>
        /// <returns></returns>
        private static extrafields ConvertToExtraFields(ushort[] extras)
        {
            return new extrafields
            {
                link = ((uint)extras[0] << 16) | extras[1],
                uid = extras[2],
                gid = extras[3],
                prot = ((uint)extras[4] << 16) | extras[5],
                virtualsize = ((uint)extras[6] << 16) | extras[7],
                rollpointer = ((uint)extras[8] << 16) | extras[9],
                fsizex = extras[10],
            };
        }
    }
}