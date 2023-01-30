namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class DirEntryWriter
    {
        public static void Write(byte[] data, int offset, int next, direntry dirEntry, globaldata g)
        {
            data[offset] = (byte)next;
            data[offset + 1] = (byte)dirEntry.type;
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.anode, data, offset + 2);
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.fsize, data, offset + 6);
            DateHelper.WriteDate(dirEntry.CreationDate, data, offset + 10);
            data[offset + 16] = dirEntry.protection;
            data[offset + 17] = (byte)dirEntry.Name.Length;
            var nameBytes = AmigaTextHelper.GetBytes(dirEntry.Name);
            Array.Copy(nameBytes, 0, data, offset + 18, nameBytes.Length);

            if (!string.IsNullOrEmpty(dirEntry.comment))
            {
                var commentBytes = AmigaTextHelper.GetBytes(dirEntry.comment);
                data[offset + 18 + nameBytes.Length] = (byte)dirEntry.comment.Length;
                Array.Copy(commentBytes, 0, data, offset + 18 + nameBytes.Length + 1, commentBytes.Length);
            }

            if (g.dirextension)
            {
                WriteExtraFields(data, offset, dirEntry);
            }
            
            // update offset in entries for later use
            // dirEntry.Offset = offset;
        }

        private static void WriteExtraFields(byte[] data, int offset, direntry dirEntry)
        {
            // UWORD offset, *dirext;
            // UWORD array[16], i = 0, j = 0;
            // UWORD flags = 0, orvalue;
            // UWORD *fields = (UWORD *)extra;
            
            /* patch protection lower 8 bits */
            dirEntry.ExtraFields.prot &= 0xffffff00;
            
            ushort flags = 0;
            var fields = 0;
            var array = extrafields.ConvertToUShortArray(dirEntry.ExtraFields);

            // offset = (sizeof(struct direntry) + (direntry->nlength) + *COMMENT(direntry)) & 0xfffe;
            // dirext = (UWORD *)((UBYTE *)(direntry) + (UBYTE)offset);
            var commentLength = dirEntry.comment.Length > 0 ? dirEntry.comment.Length + 1 : 0;
            var extraFieldsOffset = (SizeOf.DirEntry.Struct + dirEntry.Name.Length + commentLength) & 0xfffe;
            var dirext = extraFieldsOffset;
            
            ushort orvalue = 1;
            // /* fill packed field array */
            int i;
            var j = 0;
            for (i = 0; i < SizeOf.ExtraFields.Struct / 2; i++)
            {
                if (array[fields] != 0)
                {
                    //array[j++] = *fields++;
                    fields++;
                    j++;
                    flags |= orvalue;
                }
                else
                {
                    fields++;
                }
            
                orvalue <<= 1;
            }

            // /* add fields to direntry */
            i = j;
            while (i > 0)
            {
                //     *dirext++ = array[--i];
                BigEndianConverter.ConvertUInt16ToBytes(array[--i], data, offset + dirext);
                dirext += 2; // 2 bytes increase for ushort/uword
            }
            // *dirext++ = flags;
            data[offset + dirext] = (byte)flags;

            // direntry.next = offset + 2 * j + 2;
            //dirEntry.next = (byte)(extraFieldsOffset + 2 * j + 2);
        }
    }
}