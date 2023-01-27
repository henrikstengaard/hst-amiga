namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Blocks;
    using Core.Converters;

    public static class DirEntryWriter
    {
        public static void Write(byte[] data, int offset, direntry dirEntry)
        {
            if (dirEntry.ExtraFields != null)
            {
                WriteExtraFields(data, offset, dirEntry);
            }
            
            data[offset] = dirEntry.next;
            data[offset + 1] = (byte)dirEntry.type;
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.anode, data, offset + 2);
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.fsize, data, offset + 6);
            DateHelper.WriteDate(dirEntry.CreationDate, data, offset + 10);
            data[offset + 16] = dirEntry.protection;
            data[offset + 17] = dirEntry.nlength;
            var nameBytes = AmigaTextHelper.GetBytes(dirEntry.Name ?? string.Empty);
            Array.Copy(nameBytes, 0, data, offset + 18, nameBytes.Length);

            if (!string.IsNullOrEmpty(dirEntry.comment))
            {
                var commentBytes = AmigaTextHelper.GetBytes(dirEntry.comment);
                data[offset + 18 + nameBytes.Length] = (byte)dirEntry.comment.Length;
                Array.Copy(commentBytes, 0, data, offset + 18 + nameBytes.Length + 1, commentBytes.Length);
            }
            
            // update offset in entries for later use
            dirEntry.Offset = offset;
        }

        public static void WriteExtraFields(byte[] data, int offset, direntry dirEntry)
        {
            // UWORD offset, *dirext;
            // UWORD array[16], i = 0, j = 0;
            // UWORD flags = 0, orvalue;
            // UWORD *fields = (UWORD *)extra;
            
            /* patch protection lower 8 bits */
            dirEntry.ExtraFields.prot &= 0xffffff00;
            
            ushort flags = 0;
            var fields = 0;
            var array = ConvertToUShortArray(dirEntry.ExtraFields);

            // offset = (sizeof(struct direntry) + (direntry->nlength) + *COMMENT(direntry)) & 0xfffe;
            // dirext = (UWORD *)((UBYTE *)(direntry) + (UBYTE)offset);
            var commentLength = dirEntry.comment.Length > 0 ? dirEntry.comment.Length + 1 : 0;
            var extraFieldsOffset = (SizeOf.DirEntry.Struct + dirEntry.nlength + commentLength) & 0xfffe;
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
            dirEntry.next = (byte)(extraFieldsOffset + 2 * j + 2);
        }

        /// <summary>
        /// Replicate c behavior of reading extra fields struct memory area as an array of ushorts: "UWORD *fields = (UWORD *)extra" and "array[j++] = *fields++"
        /// </summary>
        /// <param name="extraFields"></param>
        /// <returns></returns>
        private static ushort[] ConvertToUShortArray(extrafields extraFields)
        {
            var extraArray = new List<ushort>();

            // add link split into 2 ushorts
            extraArray.Add((ushort)(extraFields.link >> 16));
            extraArray.Add((ushort)(extraFields.link & 0xffff));

            extraArray.Add(extraFields.uid);
            extraArray.Add(extraFields.gid);
            
            // add prot split into 2 ushorts
            extraArray.Add((ushort)(extraFields.prot >> 16));
            extraArray.Add((ushort)(extraFields.prot & 0xffff));

            // add virtual size split into 2 ushorts
            extraArray.Add((ushort)(extraFields.virtualsize >> 16));
            extraArray.Add((ushort)(extraFields.virtualsize & 0xffff));
            
            // add roll pointer split into 2 ushorts
            extraArray.Add((ushort)(extraFields.rollpointer >> 16));
            extraArray.Add((ushort)(extraFields.rollpointer & 0xffff));

            extraArray.Add(extraFields.fsizex);

            return extraArray.ToArray();
        }
    }
}